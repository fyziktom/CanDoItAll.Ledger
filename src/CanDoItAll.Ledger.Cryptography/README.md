# CanDoItAll.Ledger.Cryptography

Canonical hashing, address derivation, signatures, key vaults, and
platform-neutral cryptographic drivers for CanDoItAll Ledger.

Use this package when a .NET host needs to create account keys, derive Ledger
addresses, sign or verify payloads, hash canonical transactions, encrypt
account vaults, or compose supported signature providers.

## Requirements

- .NET 10

## Install

```powershell
dotnet add package CanDoItAll.Ledger.Cryptography
```

## Create And Verify An Account Signature

```csharp
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Ledger.Cryptography;

var crypto = new DotNetLedgerCryptoDriver();
EcdsaP256KeyPair keys =
    await crypto.CreateEcdsaP256KeyPairAsync();

var address = await LedgerAddressCodec.FromPublicKeyAsync(
    keys.PublicKeyMaterial,
    crypto);

byte[] payload = Encoding.UTF8.GetBytes("expense-approval");
try
{
    byte[] signature = await crypto.SignEcdsaP256Sha256Async(
        payload,
        keys.PrivateKeyMaterial);

    bool valid = await crypto.VerifyEcdsaP256Sha256Async(
        payload,
        signature,
        keys.PublicKeyMaterial);
}
finally
{
    CryptographicOperations.ZeroMemory(keys.PrivateKeyMaterial);
}
```

The package has no broad service-registration extension. Compose
`DotNetLedgerCryptoDriver`, `DefaultTransactionCanonicalizer`,
`TransactionHasher`, `SignaturePayloadBuilder`, and
`SignatureProviderRegistry` directly, or use the registrations supplied by
`CanDoItAll.Ledger.Sdk`.

## Algorithm And Runtime Notes

- Account vault creation currently uses ECDSA P-256 account keys.
- `SignatureProviderRegistry.CreateDefault()` includes ECDSA P-256 plus VEF,
  ML-DSA, and SLH-DSA provider entries. Availability depends on the runtime and
  adapters; a registered entry is not a guarantee that the algorithm is
  usable.
- VEF secp256k1 requires an `IVefSecp256K1SignatureAdapter`.
- Use `PqcAvailabilityProbe` before selecting post-quantum algorithms.
- Browser applications should use
  `CanDoItAll.Ledger.Cryptography.Browser`.

## Security Notes

- Never log private keys, derived secrets, backup passwords, or decrypted vault
  contents.
- Clear mutable private-key buffers when the operation is complete and keep
  their lifetime as short as possible.
- `InMemoryAccountVaultStore` is disposable and not a durable custody system.
- Generate transaction IDs with `DefaultTransactionCanonicalizer` and
  `TransactionHasher`; ad hoc JSON hashing is not protocol-compatible.
- Key generation and verification do not replace host authorization or
  transaction-policy validation.

See the
[cryptography architecture](https://github.com/fyziktom/CanDoItAll.Ledger/tree/main/docs)
and [full source](https://github.com/fyziktom/CanDoItAll.Ledger).

This package is part of the [CanDoItAll ecosystem](https://aicandoitall.com)
and uses the repository's MIT License.
