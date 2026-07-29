using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CanDoItAll.Ledger.Simulator;

public interface ILedgerSimulatorJsonSerializer
{
    string Serialize<T>(T value);

    T? Deserialize<T>(string json);

    string HashJson<T>(T value);

    LedgerSimulatorBusinessObjectCommandDraft CreateDraft<T>(
        string objectType,
        T state,
        Address? ownerAddress = null,
        Amount? mainAmount = null,
        TokenTransfer? token = null,
        string? externalRef = null);
}

public sealed class LedgerSimulatorJsonSerializer : ILedgerSimulatorJsonSerializer
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    public string Serialize<T>(T value)
    {
        string json = JsonSerializer.Serialize(value, Options);
        JsonNode? node = JsonNode.Parse(json);
        return SortNode(node)?.ToJsonString(Options) ?? json;
    }

    public T? Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, Options);

    public string HashJson<T>(T value)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Serialize(value)))).ToLowerInvariant();

    public LedgerSimulatorBusinessObjectCommandDraft CreateDraft<T>(
        string objectType,
        T state,
        Address? ownerAddress = null,
        Amount? mainAmount = null,
        TokenTransfer? token = null,
        string? externalRef = null)
    {
        LedgerSimulatorObjectTypeCatalog catalog = LedgerSimulatorObjectTypeCatalog.Instance;
        if (!catalog.Contains(objectType))
        {
            throw new ArgumentException($"Unknown economy simulator object type '{objectType}'.", nameof(objectType));
        }

        return new LedgerSimulatorBusinessObjectCommandDraft(
            objectType,
            LedgerSimulatorSchema.Version,
            Serialize(state),
            ownerAddress,
            mainAmount,
            token,
            externalRef);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new BusinessUnitIdJsonConverter());
        options.Converters.Add(new LedgerSimulatorModelIdJsonConverter());
        options.Converters.Add(new LedgerSimulatorTemplateIdJsonConverter());
        options.Converters.Add(new EconomicEventIdJsonConverter());
        options.Converters.Add(new ScenarioRunIdJsonConverter());
        return options;
    }

    private static JsonNode? SortNode(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            var sorted = new JsonObject();
            foreach (KeyValuePair<string, JsonNode?> property in jsonObject.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                sorted[property.Key] = SortNode(property.Value?.DeepClone());
            }

            return sorted;
        }

        if (node is JsonArray jsonArray)
        {
            var sorted = new JsonArray();
            foreach (JsonNode? item in jsonArray)
            {
                sorted.Add(SortNode(item?.DeepClone()));
            }

            return sorted;
        }

        return node;
    }
}

internal abstract class LedgerSimulatorValueObjectJsonConverter<T> : JsonConverter<T>
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return Create(reader.GetString() ?? string.Empty);
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected string or object for {typeof(T).Name}.");
        }

        string? value = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            string? propertyName = reader.GetString();
            reader.Read();
            if (string.Equals(propertyName, "value", StringComparison.OrdinalIgnoreCase))
            {
                value = reader.GetString();
            }
            else
            {
                reader.Skip();
            }
        }

        return Create(value ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("value", value?.ToString());
        writer.WriteEndObject();
    }

    protected abstract T Create(string value);
}

internal sealed class BusinessUnitIdJsonConverter : LedgerSimulatorValueObjectJsonConverter<BusinessUnitId>
{
    protected override BusinessUnitId Create(string value) => new(value);
}

internal sealed class LedgerSimulatorModelIdJsonConverter : LedgerSimulatorValueObjectJsonConverter<LedgerSimulatorModelId>
{
    protected override LedgerSimulatorModelId Create(string value) => new(value);
}

internal sealed class LedgerSimulatorTemplateIdJsonConverter : LedgerSimulatorValueObjectJsonConverter<LedgerSimulatorTemplateId>
{
    protected override LedgerSimulatorTemplateId Create(string value) => new(value);
}

internal sealed class EconomicEventIdJsonConverter : LedgerSimulatorValueObjectJsonConverter<EconomicEventId>
{
    protected override EconomicEventId Create(string value) => new(value);
}

internal sealed class ScenarioRunIdJsonConverter : LedgerSimulatorValueObjectJsonConverter<ScenarioRunId>
{
    protected override ScenarioRunId Create(string value) => new(value);
}

public static class LedgerSimulatorCommandDraftExtensions
{
    public static CreateBusinessObjectRequest ToCreateBusinessObjectRequest(this LedgerSimulatorBusinessObjectCommandDraft draft)
        => new(
            draft.ObjectType,
            draft.StateJson,
            draft.SchemaVersion,
            draft.OwnerAddress,
            draft.MainAmount,
            draft.Token,
            TransactionDataItemType.BusinessObject,
            ExternalRef: draft.ExternalRef);
}
