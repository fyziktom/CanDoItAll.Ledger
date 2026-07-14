using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components;

internal static class TransactionRawJsonFormatter
{
    private static readonly JsonSerializerOptions RawJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public static string FormatTransaction(EconomyTransactionDetailsModel details)
        => JsonSerializer.Serialize(CreateRawTransaction(details), RawJsonOptions);

    public static string FormatInlineJson(string? value)
    {
        object? parsed = ParseInlineJson(value);
        return parsed switch
        {
            null => string.Empty,
            string text => text,
            _ => JsonSerializer.Serialize(parsed, RawJsonOptions)
        };
    }

    private static object CreateRawTransaction(EconomyTransactionDetailsModel details)
        => new
        {
            txid = details.TxId.Value,
            hash = details.TxHash?.Value,
            ledgerSequence = details.LedgerSequence,
            ledgerEntryHash = details.LedgerEntryHash?.Value,
            kind = details.Kind.ToString(),
            status = details.Status.ToString(),
            signingMode = details.SigningMode.ToString(),
            time = details.Timestamp,
            data = CreateRawData(details.Data),
            vin = details.Inputs
                .OrderBy(input => input.InputIndex)
                .Select(CreateRawInput)
                .ToArray(),
            vout = details.Outputs
                .OrderBy(output => output.OutputIndex)
                .Select(output => CreateRawOutput(details.TxId, output))
                .ToArray()
        };

    private static object? CreateRawData(TransactionData? data)
        => data is null
            ? null
            : new
            {
                itemType = data.ItemType.ToString(),
                contentType = data.ContentType,
                dataHash = data.DataHash?.Value,
                inlineJson = ParseInlineJson(data.InlineJson),
                externalRef = data.ExternalRef
            };

    private static object CreateRawInput(EconomyTransactionInputDetails input)
        => new
        {
            n = input.InputIndex,
            txid = input.PreviousOutPoint.TxId.Value,
            vout = input.PreviousOutPoint.OutputIndex,
            previousOutPoint = input.PreviousOutPoint.ToString(),
            address = input.Address.Value,
            outPointKey = input.OutPointKey?.Value.Value,
            amount = input.MainAmount?.Value,
            token = CreateRawToken(input.Token),
            businessObject = CreateRawBusinessObject(
                input.BusinessObject,
                BusinessObjectId.FromOutPoint(input.PreviousOutPoint)),
            signaturePayloadHash = input.SignaturePayloadHash?.Value,
            signatures = input.SignatureBundle.Signatures
                .Select(CreateRawSignature)
                .ToArray()
        };

    private static object CreateRawOutput(TxId transactionId, EconomyTransactionOutputDetails output)
        => new
        {
            n = output.OutputIndex,
            value = output.MainAmount.Value,
            address = output.Address.Value,
            token = CreateRawToken(output.Token),
            businessObject = CreateRawBusinessObject(
                output.BusinessObject,
                BusinessObjectId.FromOutPoint(transactionId, output.OutputIndex)),
            metadataHash = output.MetadataHash?.Value
        };

    private static object? CreateRawToken(TokenTransfer? token)
        => token is null
            ? null
            : new
            {
                tokenId = token.TokenId.Value,
                amount = token.Amount.Value
            };

    private static object? CreateRawBusinessObject(
        EconomyTransactionBusinessObjectDetails? businessObject,
        BusinessObjectId objectId)
        => businessObject is null
            ? null
            : new
            {
                objectId = objectId.Value,
                version = businessObject.Version,
                operation = businessObject.Operation.ToString(),
                dataHash = businessObject.DataHash.Value
            };

    private static object CreateRawSignature(SignatureEnvelope signature)
        => new
        {
            algorithm = signature.Algorithm.ToString(),
            keyId = signature.KeyId,
            publicKeyHash = signature.PublicKeyHash.Value,
            payloadHash = signature.PayloadHash.Value,
            signature = ToHex(signature.SignatureBytes),
            publicKey = signature.PublicKeyMaterial is null ? null : ToHex(signature.PublicKeyMaterial)
        };

    private static object? ParseInlineJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            JsonNode? node = JsonNode.Parse(value);
            if (node is not null)
            {
                ExpandNestedJsonStrings(node);
            }

            return node;
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static void ExpandNestedJsonStrings(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (KeyValuePair<string, JsonNode?> property in obj.ToArray())
            {
                if (TryParseJsonObjectOrArrayString(property.Value, out JsonNode? parsed))
                {
                    obj[property.Key] = parsed;
                    ExpandNestedJsonStrings(parsed!);
                    continue;
                }

                if (property.Value is not null)
                {
                    ExpandNestedJsonStrings(property.Value);
                }
            }
        }
        else if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                JsonNode? item = array[i];
                if (TryParseJsonObjectOrArrayString(item, out JsonNode? parsed))
                {
                    array[i] = parsed;
                    ExpandNestedJsonStrings(parsed!);
                    continue;
                }

                if (item is not null)
                {
                    ExpandNestedJsonStrings(item);
                }
            }
        }
    }

    private static bool TryParseJsonObjectOrArrayString(JsonNode? node, out JsonNode? parsed)
    {
        parsed = null;
        if (node is not JsonValue value || !value.TryGetValue(out string? text))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string trimmed = text.TrimStart();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal) && !trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            parsed = JsonNode.Parse(text);
            return parsed is JsonObject or JsonArray;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ToHex(byte[] bytes)
        => Convert.ToHexString(bytes).ToLowerInvariant();
}
