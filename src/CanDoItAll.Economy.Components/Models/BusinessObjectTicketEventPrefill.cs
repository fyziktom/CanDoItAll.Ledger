using CanDoItAll.Economy.BusinessObjects;

namespace CanDoItAll.Economy.Components.Models;

public sealed record BusinessObjectTicketEventPrefill(
    IBusinessObjectView EventObject,
    string EventName,
    IReadOnlyDictionary<string, string> Fields)
{
    public static BusinessObjectTicketEventPrefill FromEvent(IBusinessObjectView eventObject)
    {
        ArgumentNullException.ThrowIfNull(eventObject);
        if (!eventObject.ObjectType.Equals("event", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The supplied BusinessObject is not an event.", nameof(eventObject));
        }

        IReadOnlyDictionary<string, string> eventFields = ReadEventFields(eventObject);
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["eventId"] = eventObject.ObjectId.Value,
            ["eventAddress"] = eventObject.OwnerAddress.Value,
            ["used"] = "false",
            ["status"] = "FreeToUse"
        };
        CopyFirst(fields, "eventDate", eventFields, "eventDate", "startsAt");
        CopyFirst(fields, "location", eventFields, "location", "venue");
        CopyFirst(fields, "locationCoordinates", eventFields, "locationCoordinates");
        CopyFirst(fields, "authorLink", eventFields, "authorLink", "website", "link");

        return new BusinessObjectTicketEventPrefill(
            eventObject,
            EventDisplayName(eventObject),
            fields);
    }

    private static IReadOnlyDictionary<string, string> ReadEventFields(IBusinessObjectView eventObject)
    {
        if (eventObject is ITypedBusinessObjectView typed)
        {
            return typed.Fields;
        }

        if (BusinessObjectPayloadBuilder.TryParseMediaPayload(eventObject.StateJson, out BusinessObjectMediaPayload? payload)
            && payload?.Fields is not null)
        {
            return BusinessObjectPayloadBuilder.NormalizeFields(
                BusinessObjectFamilyCatalog.FindOrDefault("event"),
                payload.Fields);
        }

        return new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static void CopyFirst(
        IDictionary<string, string> target,
        string targetKey,
        IReadOnlyDictionary<string, string> source,
        params string[] sourceKeys)
    {
        foreach (string sourceKey in sourceKeys)
        {
            if (source.TryGetValue(sourceKey, out string? value) && !string.IsNullOrWhiteSpace(value))
            {
                target[targetKey] = value.Trim();
                return;
            }
        }
    }

    private static string EventDisplayName(IBusinessObjectView eventObject)
        => eventObject is ITypedBusinessObjectView typed && !string.IsNullOrWhiteSpace(typed.Name)
            ? typed.Name
            : eventObject.ObjectId.Value;
}
