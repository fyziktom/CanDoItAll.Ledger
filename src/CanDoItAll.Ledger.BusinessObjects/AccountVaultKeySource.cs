using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;

namespace CanDoItAll.Ledger.BusinessObjects;

public sealed class AccountVaultKeySource : IAccountKeySource
{
    private readonly InMemoryAccountKeySource inner;

    public AccountVaultKeySource(AccountVaultUnlockedKey unlockedKey)
    {
        ArgumentNullException.ThrowIfNull(unlockedKey);
        VaultId = unlockedKey.VaultId;
        DisplayName = unlockedKey.DisplayName;
        inner = new InMemoryAccountKeySource(
            unlockedKey.Address,
            unlockedKey.Algorithm,
            unlockedKey.KeyId,
            unlockedKey.PublicKeyHash,
            unlockedKey.PublicKeyMaterial.ToArray(),
            unlockedKey.PrivateKeyMaterial.ToArray());
    }

    public string VaultId { get; }

    public string? DisplayName { get; }

    public Address Address => inner.Address;

    public SignatureAlgorithmId Algorithm => inner.Algorithm;

    public string KeyId => inner.KeyId;

    public HashHex PublicKeyHash => inner.PublicKeyHash;

    public ReadOnlyMemory<byte> PublicKeyMaterial => inner.PublicKeyMaterial;

    public ValueTask<ReadOnlyMemory<byte>> GetPrivateKeyMaterialAsync(CancellationToken cancellationToken)
        => inner.GetPrivateKeyMaterialAsync(cancellationToken);
}
