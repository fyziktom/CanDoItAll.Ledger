using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Persistence.EFCore.Records;

namespace CanDoItAll.Economy.Persistence.EFCore;

internal static class EfBusinessObjectProjectionMapper
{
    public static string BusinessObjectKey(LedgerId ledgerId, ScenarioId? scenarioId, BusinessObjectLineageKey lineageKey)
        => $"{ledgerId.Value}|{scenarioId?.Value ?? string.Empty}|{lineageKey.Value}";

    public static string BusinessObjectKey(string ledgerId, string? scenarioId, string lineageKey)
        => $"{ledgerId}|{scenarioId ?? string.Empty}|{lineageKey}";

    public static BusinessObjectProjectionPayload ResolvePayload(
        string? inlineJson,
        string? dataHash,
        UnspentOutputRecord output,
        BusinessObjectRecord? previous)
    {
        BusinessObjectPayloadEnvelope? envelope = ReadPayloadEnvelope(inlineJson);
        bool payloadMatchesOutput = !string.IsNullOrWhiteSpace(dataHash)
            && string.Equals(output.BusinessObjectDataHash, dataHash, StringComparison.Ordinal);
        bool shouldUseEnvelope = envelope is not null
            && (payloadMatchesOutput
                || previous is null
                || output.BusinessObjectOperation == (int)BusinessObjectOperation.Create);

        if (shouldUseEnvelope)
        {
            return new BusinessObjectProjectionPayload(
                envelope!.ObjectType,
                envelope.SchemaVersion,
                envelope.StateJson,
                envelope.StateJson);
        }

        if (payloadMatchesOutput && inlineJson is not null)
        {
            return new BusinessObjectProjectionPayload(
                previous?.ObjectType ?? "business-object",
                "1.0",
                inlineJson,
                inlineJson);
        }

        if (previous is not null)
        {
            return new BusinessObjectProjectionPayload(
                previous.ObjectType,
                "1.0",
                previous.CurrentState,
                previous.CurrentState);
        }

        return envelope is null
            ? new BusinessObjectProjectionPayload("business-object", "1.0", inlineJson ?? "{}", inlineJson)
            : new BusinessObjectProjectionPayload(envelope.ObjectType, envelope.SchemaVersion, envelope.StateJson, envelope.StateJson);
    }

    public static BusinessObjectRecord ToCurrentRecord(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        TxId txId,
        UnspentOutputRecord output,
        BusinessObjectProjectionPayload payload,
        BusinessObjectRecord? previous)
    {
        string lineageKey = RequireLineageKey(output);
        return new BusinessObjectRecord
        {
            BusinessObjectKey = BusinessObjectKey(ledgerId, scenarioId, new BusinessObjectLineageKey(lineageKey)),
            LedgerId = ledgerId.Value,
            ScenarioId = scenarioId?.Value,
            ObjectId = lineageKey,
            ObjectType = payload.ObjectType,
            CurrentVersion = output.BusinessObjectVersion ?? 0,
            CurrentOwnerAddress = output.Address,
            CurrentState = payload.StateJson,
            CurrentHash = output.BusinessObjectDataHash ?? string.Empty,
            CurrentOutPointTxId = output.TxId,
            CurrentOutPointIndex = output.OutputIndex,
            CreatedTxId = previous?.CreatedTxId ?? txId.Value,
            UpdatedTxId = txId.Value
        };
    }

    public static void CopyCurrentRecord(BusinessObjectRecord target, BusinessObjectRecord source)
    {
        target.ObjectType = source.ObjectType;
        target.CurrentVersion = source.CurrentVersion;
        target.CurrentOwnerAddress = source.CurrentOwnerAddress;
        target.CurrentState = source.CurrentState;
        target.CurrentHash = source.CurrentHash;
        target.CurrentOutPointTxId = source.CurrentOutPointTxId;
        target.CurrentOutPointIndex = source.CurrentOutPointIndex;
        target.UpdatedTxId = source.UpdatedTxId;
    }

    public static BusinessObjectVersionRecord ToVersionRecord(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        TxId txId,
        UnspentOutputRecord output,
        BusinessObjectProjectionPayload payload,
        DateTimeOffset confirmedAt)
    {
        string lineageKey = RequireLineageKey(output);
        return new BusinessObjectVersionRecord
        {
            LedgerId = ledgerId.Value,
            ScenarioId = scenarioId?.Value,
            ObjectId = lineageKey,
            Version = output.BusinessObjectVersion ?? 0,
            TxId = txId.Value,
            OutPointTxId = output.TxId,
            OutPointIndex = output.OutputIndex,
            OwnerAddress = output.Address,
            State = payload.StateJson,
            SchemaVersion = payload.SchemaVersion,
            DataHash = output.BusinessObjectDataHash ?? string.Empty,
            DataJson = payload.DataJson,
            CreatedAt = confirmedAt
        };
    }

    private static string RequireLineageKey(UnspentOutputRecord output)
        => string.IsNullOrWhiteSpace(output.BusinessObjectId)
            ? throw new InvalidOperationException("BusinessObject projection output is missing its legacy lineage key.")
            : output.BusinessObjectId;

    private static BusinessObjectPayloadEnvelope? ReadPayloadEnvelope(string? inlineJson)
        => BusinessObjectPayloadEnvelopeReader.TryRead(inlineJson, out BusinessObjectPayloadEnvelope envelope)
            ? envelope
            : null;
}

internal sealed record BusinessObjectProjectionPayload(
    string ObjectType,
    string SchemaVersion,
    string StateJson,
    string? DataJson);
