using System.Text.Json;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Tests;

public sealed class LedgerTransactionJsonContractTests
{
    [Fact]
    public void LedgerTransaction_WithBusinessObjectReference_RoundTripsOverWebJson()
    {
        var permissionReference = new BusinessObjectRef(
            new BusinessObjectLineageKey("permission:mint_new_tok_type:xray:grant"),
            2,
            BusinessObjectOperation.Update,
            new HashHex("e8452d51941114e2c4291999d208fc77bf4848f224593cee3e16391ba99af220"));
        var transaction = new LedgerTransaction(
            new LedgerId("default"),
            ScenarioId: null,
            Version: 1,
            TransactionKind.Mint,
            SigningMode.Independent,
            DateTimeOffset.Parse("2026-07-14T23:27:35Z", System.Globalization.CultureInfo.InvariantCulture),
            Inputs:
            [
                new TransactionInput(
                    0,
                    new OutPoint(new TxId("b7a428ddfea8bf8c3f894e3d652b7f2a255c9c7ffec00cc728d6f3bff987c876"), 0),
                    new Address("cdia1Ngb3miLzYorvFSfKzLFwYPvZH6Kw6LPsSu"),
                    SignatureBundle.Empty)
            ],
            Outputs:
            [
                new TransactionOutput(
                    0,
                    new Address("cdia1Ngb3miLzYorvFSfKzLFwYPvZH6Kw6LPsSu"),
                    Amount.Zero,
                    new TokenTransfer(new TokenId("xray.image"), Amount.FromDecimal(1_000_000_000_000m))),
                new TransactionOutput(
                    1,
                    new Address("cdia1Ngb3miLzYorvFSfKzLFwYPvZH6Kw6LPsSu"),
                    Amount.Zero,
                    new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, Amount.FromInt64(1)),
                    permissionReference)
            ],
            Data: null);
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        string json = JsonSerializer.Serialize(transaction, jsonOptions);
        LedgerTransaction? roundTripped = JsonSerializer.Deserialize<LedgerTransaction>(json, jsonOptions);

        Assert.NotNull(roundTripped);
        Assert.Equal(1_000_000_000_000m, roundTripped.Outputs[0].Token!.Amount.Value);
        Assert.Equal(permissionReference, roundTripped.Outputs[1].BusinessObject);
        Assert.Contains("\"objectId\"", json, StringComparison.Ordinal);
    }
}
