using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class BusinessObjectUtxoIdentityTests
{
    [Fact]
    public void FromOutPoint_RoundTripsCanonicalBusinessObjectId()
    {
        var outPoint = new OutPoint(new TxId(new string('a', 64)), 7);

        BusinessObjectId objectId = BusinessObjectId.FromOutPoint(outPoint);

        Assert.Equal($"{new string('a', 64)}:7", objectId.Value);
        Assert.True(objectId.TryGetOutPoint(out OutPoint parsed));
        Assert.Equal(outPoint, parsed);
        Assert.Equal(outPoint, objectId.RequireOutPoint());
        Assert.True(objectId.IsCanonicalOutPointId());
    }

    [Theory]
    [InlineData("")]
    [InlineData("bo:post:user-name")]
    [InlineData("transaction-only")]
    [InlineData("transaction:-1")]
    [InlineData("transaction:not-a-number")]
    public void TryGetOutPoint_RejectsMetadataAndMalformedIds(string value)
    {
        var objectId = new BusinessObjectId(value);

        Assert.False(objectId.TryGetOutPoint(out _));
        Assert.Throws<FormatException>(() => objectId.RequireOutPoint());
    }

    [Fact]
    public void Canonicalizer_PreservesV1ObjectIdWireBytesForLineageKeyRename()
    {
        var lineage = new BusinessObjectLineageKey("bo:legacy:stable-lineage");
        var legacyRef = new BusinessObjectRef(
            new BusinessObjectId(lineage.Value),
            1,
            BusinessObjectOperation.Create,
            new HashHex(new string('b', 64)));
        var explicitLineageRef = new BusinessObjectRef(
            lineage,
            1,
            BusinessObjectOperation.Create,
            new HashHex(new string('b', 64)));
        LedgerTransaction LegacyTransaction(BusinessObjectRef reference) => new(
            new LedgerId("default"),
            null,
            1,
            TransactionKind.BusinessObject,
            SigningMode.Independent,
            DateTimeOffset.UnixEpoch,
            [],
            [new TransactionOutput(0, new Address("owner"), Amount.Zero, BusinessObject: reference)],
            null);
        var canonicalizer = new DefaultTransactionCanonicalizer();
        var hasher = new TransactionHasher(canonicalizer);

        byte[] legacyBytes = canonicalizer.GetTransactionCoreBytes(LegacyTransaction(legacyRef));
        byte[] renamedBytes = canonicalizer.GetTransactionCoreBytes(LegacyTransaction(explicitLineageRef));

        Assert.Equal(legacyBytes, renamedBytes);
        Assert.Equal(
            hasher.CalculateTxId(LegacyTransaction(legacyRef)),
            hasher.CalculateTxId(LegacyTransaction(explicitLineageRef)));
    }

    [Fact]
    public void RandomLineageFactory_ProducesOpaqueNonMetadataKeys()
    {
        BusinessObjectLineageKey first = RandomBusinessObjectLineageKeyFactory.Instance.Create();
        BusinessObjectLineageKey second = RandomBusinessObjectLineageKeyFactory.Instance.Create();

        Assert.StartsWith("lineage:", first.Value, StringComparison.Ordinal);
        Assert.StartsWith("lineage:", second.Value, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
        Assert.DoesNotContain("post", first.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name", first.Value, StringComparison.OrdinalIgnoreCase);
        Assert.True(LedgerValueValidation.IsValidIdentifier(
            first.Value,
            LedgerValidationLimits.MaxBusinessObjectIdLength));
    }
}
