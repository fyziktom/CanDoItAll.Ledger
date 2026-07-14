using System.Text;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;

namespace CanDoItAll.Economy.BusinessObjects;

public enum BusinessObjectTicketHistoryStatus
{
    Valid = 0,
    NotTicket = 1,
    MissingOrigin = 2,
    Used = 3
}

public sealed record BusinessObjectTicketHistoryEntry(
    TxId TxId,
    DateTimeOffset Timestamp,
    int Version,
    bool Used,
    string? StateJson);

public sealed record BusinessObjectTicketHistoryValidation(
    BusinessObjectTicketHistoryStatus Status,
    TxId? OriginTxId,
    TxId? UsedTxId,
    IReadOnlyList<BusinessObjectTicketHistoryEntry> Entries)
{
    public bool IsValid => Status == BusinessObjectTicketHistoryStatus.Valid;

    public bool IsUsed => Status == BusinessObjectTicketHistoryStatus.Used;

    public string Summary => Status switch
    {
        BusinessObjectTicketHistoryStatus.Valid => "Ticket history is valid and no Used flag was found.",
        BusinessObjectTicketHistoryStatus.NotTicket => "BusinessObject is not a ticket.",
        BusinessObjectTicketHistoryStatus.MissingOrigin => "Ticket origin transaction was not found in history.",
        BusinessObjectTicketHistoryStatus.Used => $"Ticket was already used in {UsedTxId?.Value ?? "history"}.",
        _ => "Ticket history could not be validated."
    };
}

public sealed record BusinessObjectTicketVerificationMessage(
    string Message,
    HashHex MessageHash,
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public static class BusinessObjectTicketVerification
{
    public const string UsedField = "used";
    public const string UsedAtField = "usedAt";
    public const string VerificationField = "lastVerificationHash";

    public static bool IsTicketObject(string? objectType)
        => string.Equals(objectType, "ticket", StringComparison.OrdinalIgnoreCase)
            || string.Equals(objectType, "bo-ticket", StringComparison.OrdinalIgnoreCase)
            || string.Equals(objectType, "tickets", StringComparison.OrdinalIgnoreCase);

    public static BusinessObjectTicketHistoryValidation ValidateHistory(
        string? objectType,
        BusinessObjectId objectId,
        IReadOnlyList<LedgerTransactionSummary> history)
    {
        if (!IsTicketObject(objectType))
        {
            return new BusinessObjectTicketHistoryValidation(
                BusinessObjectTicketHistoryStatus.NotTicket,
                OriginTxId: null,
                UsedTxId: null,
                Array.Empty<BusinessObjectTicketHistoryEntry>());
        }

        BusinessObjectTicketHistoryEntry[] entries = BusinessObjectTransactionHistory
            .SelectVersions(objectId, history, transactionsAreLineageFiltered: true)
            .Select(version => ToEntry(version.Transaction, version.Output))
            .ToArray();

        BusinessObjectTicketHistoryEntry? origin = entries.FirstOrDefault(entry => entry.Version == 1);
        if (origin is null)
        {
            return new BusinessObjectTicketHistoryValidation(
                BusinessObjectTicketHistoryStatus.MissingOrigin,
                OriginTxId: null,
                UsedTxId: null,
                entries);
        }

        BusinessObjectTicketHistoryEntry? used = entries.FirstOrDefault(entry => entry.Used);
        if (used is not null)
        {
            return new BusinessObjectTicketHistoryValidation(
                BusinessObjectTicketHistoryStatus.Used,
                origin.TxId,
                used.TxId,
                entries);
        }

        return new BusinessObjectTicketHistoryValidation(
            BusinessObjectTicketHistoryStatus.Valid,
            origin.TxId,
            UsedTxId: null,
            entries);
    }

    public static bool StateHasUsedFlag(string? stateJson)
    {
        if (!BusinessObjectPayloadBuilder.TryParseMediaPayload(stateJson, out BusinessObjectMediaPayload? payload))
        {
            return false;
        }

        return payload is not null && StateHasUsedFlag(payload);
    }

    public static bool StateHasUsedFlag(BusinessObjectMediaPayload payload)
    {
        if (payload.Fields is not null
            && payload.Fields.TryGetValue(UsedField, out string? value)
            && IsTruthy(value))
        {
            return true;
        }

        return payload.TagList.Any(tag => string.Equals(tag, "Used", StringComparison.OrdinalIgnoreCase));
    }

    public static BusinessObjectTicketVerificationMessage CreateVerificationMessage(
        BusinessObjectId objectId,
        OutPoint outPoint,
        Address ownerAddress,
        DateTimeOffset utcNow)
    {
        DateTimeOffset generatedAt = RoundToMinute(utcNow);
        DateTimeOffset expiresAt = generatedAt.AddMinutes(2);
        string material = string.Join(
            '|',
            "bo-ticket-ownership-v1",
            objectId.Value,
            outPoint.ToString(),
            ownerAddress.Value,
            generatedAt.ToString("O"));
        HashHex hash = Hex.Sha256Hex(Encoding.UTF8.GetBytes(material));
        string message = string.Join(
            Environment.NewLine,
            "CanDoItAll Ledger ticket ownership verification",
            $"ObjectId: {objectId.Value}",
            $"OutPoint: {outPoint}",
            $"Owner: {ownerAddress.Value}",
            $"GeneratedUtc: {generatedAt:O}",
            $"ExpiresUtc: {expiresAt:O}",
            $"Hash: {hash.Value}");
        return new BusinessObjectTicketVerificationMessage(message, hash, generatedAt, expiresAt);
    }

    public static string BuildUsedStateJson(
        string? stateJson,
        BusinessObjectId objectId,
        DateTimeOffset usedAtUtc,
        HashHex? verificationHash = null)
    {
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.ParseMediaPayload(stateJson, "Untitled ticket");
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault("ticket");
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> field in BusinessObjectPayloadBuilder.NormalizeFields(definition, payload.Fields))
        {
            fields[field.Key] = field.Value;
        }

        fields[UsedField] = "true";
        fields[UsedAtField] = usedAtUtc.ToString("O");
        if (verificationHash is not null)
        {
            fields[VerificationField] = verificationHash.Value.Value;
        }

        string[] tags = payload.TagList
            .Where(tag => !string.Equals(tag, "FreeToUse", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(tag, "Used", StringComparison.OrdinalIgnoreCase))
            .Prepend("Used")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(tag => $"#{tag}")
            .ToArray();

        BusinessObjectMediaPayload usedPayload = payload with
        {
            Tags = string.Join(' ', tags),
            Fields = fields
        };
        return BusinessObjectPayloadBuilder.Serialize(usedPayload);
    }

    private static BusinessObjectTicketHistoryEntry ToEntry(
        LedgerTransactionSummary transaction,
        LedgerTransactionOutputSummary output)
    {
        string? stateJson = BusinessObjectPayloadEnvelopeReader.TryRead(
            transaction.Data?.InlineJson,
            out BusinessObjectPayloadEnvelope envelope)
                ? envelope.StateJson
                : transaction.Data?.InlineJson;
        return new BusinessObjectTicketHistoryEntry(
            transaction.TxId,
            transaction.Timestamp,
            output.BusinessObjectVersion ?? 0,
            StateHasUsedFlag(stateJson),
            stateJson);
    }

    private static bool IsTruthy(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "used", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset RoundToMinute(DateTimeOffset value)
        => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Offset);
}
