using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.BusinessObjects;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class CryptoDriverSmokeTests
{
    [Fact]
    public void Default_registry_contains_vef_fallback_driver()
    {
        SignatureProviderRegistry registry = SignatureProviderRegistry.CreateDefault();
        ISignatureProvider provider = registry.Get(SignatureAlgorithmId.VefSecp256K1Sha256);

        Assert.True(provider.IsSupported);
    }

    [Fact]
    public void EcdsaP256_provider_signs_and_verifies_payload()
    {
        (byte[] publicKey, byte[] privateKey) = EcdsaP256KeyMaterial.CreateKeyPair();
        ISignatureProvider provider = new EcdsaP256SignatureProvider();
        byte[] payload = Hex.Utf8("payload");

        byte[] signature = provider.Sign(payload, privateKey);

        Assert.True(provider.Verify(payload, signature, publicKey));
        Assert.False(provider.Verify(Hex.Utf8("tampered"), signature, publicKey));
    }

    [Fact]
    public async Task Account_message_security_signs_verifies_encrypts_and_decrypts()
    {
        (byte[] alicePublic, byte[] alicePrivate) = EcdsaP256KeyMaterial.CreateKeyPair();
        (byte[] bobPublic, byte[] bobPrivate) = EcdsaP256KeyMaterial.CreateKeyPair();
        var alice = new InMemoryAccountKeySource(SignatureAlgorithmId.EcdsaP256Sha256, "alice", alicePublic, alicePrivate);
        var bob = new InMemoryAccountKeySource(SignatureAlgorithmId.EcdsaP256Sha256, "bob", bobPublic, bobPrivate);

        LedgerMessageSignature signature = await AccountMessageSecurity.SignMessageAsync(alice, "hello bob", CancellationToken.None);
        Assert.True(AccountMessageSecurity.VerifyMessage("hello bob", signature));
        Assert.False(AccountMessageSecurity.VerifyMessage("hello mallory", signature));

        HashHex aliceShared = await AccountMessageSecurity.DeriveSharedSecretHashAsync(alice, bobPublic, CancellationToken.None);
        HashHex bobShared = await AccountMessageSecurity.DeriveSharedSecretHashAsync(bob, alicePublic, CancellationToken.None);
        Assert.Equal(aliceShared, bobShared);

        LedgerEncryptedMessage encrypted = await AccountMessageSecurity.EncryptMessageAsync(
            alice,
            "private hello",
            bob.Address,
            bobPublic,
            CancellationToken.None);

        string decrypted = await AccountMessageSecurity.DecryptMessageAsync(bob, encrypted, CancellationToken.None);
        Assert.Equal("private hello", decrypted);
    }
}
