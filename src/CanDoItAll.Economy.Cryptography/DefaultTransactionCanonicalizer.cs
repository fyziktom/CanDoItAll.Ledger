using System.Text.Json;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Cryptography;

public sealed class DefaultTransactionCanonicalizer : ITransactionCanonicalizer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false
    };

    public byte[] GetTransactionCoreBytes(LedgerTransaction transaction)
    {
        var dto = new
        {
            transaction.LedgerId.Value,
            ScenarioId = transaction.ScenarioId?.Value,
            transaction.Version,
            Kind = transaction.Kind.ToString(),
            SigningMode = transaction.SigningMode.ToString(),
            TimestampUnixMs = transaction.Timestamp.ToUnixTimeMilliseconds(),
            Inputs = transaction.Inputs
                .OrderBy(x => x.InputIndex)
                .Select(x => new
                {
                    x.InputIndex,
                    PreviousTxId = x.PreviousOutPoint.TxId.Value,
                    x.PreviousOutPoint.OutputIndex,
                    Address = x.Address.Value
                })
                .ToArray(),
            Outputs = transaction.Outputs
                .OrderBy(x => x.OutputIndex)
                .Select(x => new
                {
                    x.OutputIndex,
                    Address = x.Address.Value,
                    MainAmount = x.MainAmount.ToString(),
                    Token = x.Token is null ? null : new
                    {
                        TokenId = x.Token.TokenId.Value,
                        Amount = x.Token.Amount.ToString()
                    },
                    BusinessObject = x.BusinessObject is null ? null : new
                    {
                        ObjectId = x.BusinessObject.ObjectId.Value,
                        x.BusinessObject.Version,
                        Operation = x.BusinessObject.Operation.ToString(),
                        DataHash = x.BusinessObject.DataHash.Value
                    },
                    MetadataHash = x.MetadataHash?.Value
                })
                .ToArray(),
            Data = transaction.Data is null ? null : new
            {
                ItemType = transaction.Data.ItemType.ToString(),
                transaction.Data.ContentType,
                DataHash = transaction.Data.DataHash?.Value,
                transaction.Data.InlineJson,
                transaction.Data.ExternalRef
            }
        };

        return JsonSerializer.SerializeToUtf8Bytes(dto, Options);
    }

    public byte[] GetSubmittedTransactionBytes(LedgerTransaction transaction)
    {
        var dto = new
        {
            Core = Convert.ToBase64String(GetTransactionCoreBytes(transaction)),
            Signatures = transaction.Inputs
                .OrderBy(x => x.InputIndex)
                .Select(x => new
                {
                    x.InputIndex,
                    Signatures = x.SignatureBundle.Signatures
                        .OrderBy(s => s.Algorithm.ToString())
                        .ThenBy(s => s.KeyId, StringComparer.Ordinal)
                        .Select(s => new
                        {
                            Algorithm = s.Algorithm.ToString(),
                            s.KeyId,
                            PublicKeyHash = s.PublicKeyHash.Value,
                            PayloadHash = s.PayloadHash.Value,
                            Signature = Convert.ToBase64String(s.SignatureBytes),
                            PublicKeyMaterial = s.PublicKeyMaterial is null ? null : Convert.ToBase64String(s.PublicKeyMaterial)
                        })
                        .ToArray()
                })
                .ToArray()
        };

        return JsonSerializer.SerializeToUtf8Bytes(dto, Options);
    }
}
