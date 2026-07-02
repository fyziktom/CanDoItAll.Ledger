using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BrowserCryptoVaultTests
{
    private const string VaultId = "primary";
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Browser_safe_address_codec_matches_core_address_derivation()
    {
        IEconomyCryptoDriver driver = new DotNetEconomyCryptoDriver();
        EcdsaP256KeyPair keyPair = await driver.CreateEcdsaP256KeyPairAsync(CancellationToken.None);

        Address address = await EconomyAddressCodec.FromPublicKeyAsync(keyPair.PublicKeyMaterial, driver, CancellationToken.None);
        HashHex publicKeyHash = await EconomyAddressCodec.CalculatePublicKeyHashAsync(keyPair.PublicKeyMaterial, driver, CancellationToken.None);

        Assert.Equal(Address.FromPublicKey(keyPair.PublicKeyMaterial), address);
        Assert.Equal(Address.CalculatePublicKeyHash(keyPair.PublicKeyMaterial), publicKeyHash);
    }

    [Fact]
    public async Task Dotnet_driver_uses_webcrypto_compatible_p256_signatures()
    {
        IEconomyCryptoDriver driver = new DotNetEconomyCryptoDriver();
        EcdsaP256KeyPair keyPair = await driver.CreateEcdsaP256KeyPairAsync(CancellationToken.None);
        byte[] payload = Encoding.UTF8.GetBytes("transaction payload hash");

        byte[] signature = await driver.SignEcdsaP256Sha256Async(payload, keyPair.PrivateKeyMaterial, CancellationToken.None);

        Assert.Equal(64, signature.Length);
        Assert.True(await driver.VerifyEcdsaP256Sha256Async(payload, signature, keyPair.PublicKeyMaterial, CancellationToken.None));
        Assert.True(new EcdsaP256SignatureProvider().Verify(payload, signature, keyPair.PublicKeyMaterial));
    }

    [Fact]
    public async Task Dotnet_driver_encrypts_and_decrypts_aes_gcm_payloads()
    {
        IEconomyCryptoDriver driver = new DotNetEconomyCryptoDriver();
        byte[] salt = await driver.GetRandomBytesAsync(16, CancellationToken.None);
        byte[] nonce = await driver.GetRandomBytesAsync(12, CancellationToken.None);
        byte[] key = await driver.Pbkdf2Sha256Async(Encoding.UTF8.GetBytes(Password), salt, 10_000, 32, CancellationToken.None);
        byte[] plaintext = Encoding.UTF8.GetBytes("private key bytes");
        byte[] associatedData = Encoding.UTF8.GetBytes("vault metadata");

        byte[] ciphertextWithTag = await driver.AesGcmEncryptAsync(key, nonce, plaintext, associatedData, CancellationToken.None);
        byte[] decrypted = await driver.AesGcmDecryptAsync(key, nonce, ciphertextWithTag, associatedData, CancellationToken.None);

        Assert.NotEqual(plaintext, ciphertextWithTag);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public async Task Dotnet_driver_derives_ecdh_p256_shared_key_compatible_with_message_cipher()
    {
        IEconomyCryptoDriver driver = new DotNetEconomyCryptoDriver();
        EcdsaP256KeyPair alice = await driver.CreateEcdsaP256KeyPairAsync(CancellationToken.None);
        EcdsaP256KeyPair bob = await driver.CreateEcdsaP256KeyPairAsync(CancellationToken.None);

        byte[] aliceSharedKey = await driver.DeriveEcdhP256Sha256KeyAsync(
            alice.PrivateKeyMaterial,
            bob.PublicKeyMaterial,
            EcdhP256MessageCipher.SharedSecretLabelBytes,
            CancellationToken.None);
        byte[] bobSharedKey = await driver.DeriveEcdhP256Sha256KeyAsync(
            bob.PrivateKeyMaterial,
            alice.PublicKeyMaterial,
            EcdhP256MessageCipher.SharedSecretLabelBytes,
            CancellationToken.None);

        Assert.Equal(32, aliceSharedKey.Length);
        Assert.Equal(aliceSharedKey, bobSharedKey);
        Assert.Equal(
            EcdhP256MessageCipher.DeriveSharedSecretHash(alice.PrivateKeyMaterial, bob.PublicKeyMaterial),
            Hex.Sha256Hex(aliceSharedKey));
    }

    [Fact]
    public async Task Account_message_security_driver_path_encrypts_and_decrypts()
    {
        IEconomyCryptoDriver driver = new DotNetEconomyCryptoDriver();
        EcdsaP256KeyPair aliceKey = await driver.CreateEcdsaP256KeyPairAsync(CancellationToken.None);
        EcdsaP256KeyPair bobKey = await driver.CreateEcdsaP256KeyPairAsync(CancellationToken.None);
        var alice = new InMemoryAccountKeySource(SignatureAlgorithmId.EcdsaP256Sha256, "alice", aliceKey.PublicKeyMaterial, aliceKey.PrivateKeyMaterial);
        var bob = new InMemoryAccountKeySource(SignatureAlgorithmId.EcdsaP256Sha256, "bob", bobKey.PublicKeyMaterial, bobKey.PrivateKeyMaterial);

        EconomyEncryptedMessage encrypted = await AccountMessageSecurity.EncryptMessageAsync(
            alice,
            "private browser-compatible hello",
            bob.Address,
            bob.PublicKeyMaterial.ToArray(),
            driver,
            CancellationToken.None);
        string decrypted = await AccountMessageSecurity.DecryptMessageAsync(
            bob,
            encrypted,
            encrypted.SenderPublicKeyMaterial,
            driver,
            CancellationToken.None);

        Assert.NotEqual("private browser-compatible hello", Encoding.UTF8.GetString(encrypted.CipherText));
        Assert.Equal("private browser-compatible hello", decrypted);
    }

    [Fact]
    public async Task Account_vault_creates_encrypted_record_and_unlocks_private_key()
    {
        IEconomyCryptoDriver driver = new DotNetEconomyCryptoDriver();
        var store = new InMemoryAccountVaultStore();
        var vault = new AccountVaultService(driver, store);

        AccountVaultCreationResult created = await vault.CreateAsync(new AccountVaultCreateRequest(VaultId, Password), CancellationToken.None);
        AccountVaultRecord persisted = await store.GetAsync(VaultId, CancellationToken.None) ?? throw new InvalidOperationException("Vault record was not persisted.");
        AccountVaultUnlockedKey unlocked = await vault.UnlockAsync(VaultId, Password, CancellationToken.None);

        Assert.Equal(SignatureAlgorithmId.EcdsaP256Sha256, persisted.Algorithm);
        Assert.Equal(created.UnlockedKey.Address, persisted.Address);
        Assert.Equal(created.UnlockedKey.Address, unlocked.Address);
        Assert.Equal(created.UnlockedKey.PublicKeyMaterial, unlocked.PublicKeyMaterial);
        Assert.Equal(created.UnlockedKey.PrivateKeyMaterial, unlocked.PrivateKeyMaterial);
        Assert.DoesNotContain(Convert.ToBase64String(unlocked.PrivateKeyMaterial), persisted.EncryptedPrivateKeyBase64, StringComparison.Ordinal);
        Assert.Equal(Address.FromPublicKey(unlocked.PublicKeyMaterial), unlocked.Address);
    }

    [Fact]
    public async Task Account_vault_rejects_wrong_password()
    {
        IEconomyCryptoDriver driver = new DotNetEconomyCryptoDriver();
        var store = new InMemoryAccountVaultStore();
        var vault = new AccountVaultService(driver, store);
        await vault.CreateAsync(new AccountVaultCreateRequest(VaultId, Password), CancellationToken.None);

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await vault.UnlockAsync(VaultId, "wrong password", CancellationToken.None));
    }

    [Fact]
    public async Task Account_vault_rejects_tampered_ciphertext()
    {
        IEconomyCryptoDriver driver = new DotNetEconomyCryptoDriver();
        var store = new InMemoryAccountVaultStore();
        var vault = new AccountVaultService(driver, store);
        await vault.CreateAsync(new AccountVaultCreateRequest(VaultId, Password), CancellationToken.None);
        AccountVaultRecord persisted = await store.GetAsync(VaultId, CancellationToken.None) ?? throw new InvalidOperationException("Vault record was not persisted.");
        byte[] ciphertext = Convert.FromBase64String(persisted.EncryptedPrivateKeyBase64);
        ciphertext[0] ^= 0x01;
        await store.SaveAsync(persisted with { EncryptedPrivateKeyBase64 = Convert.ToBase64String(ciphertext) }, CancellationToken.None);

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await vault.UnlockAsync(VaultId, Password, CancellationToken.None));
    }
}
