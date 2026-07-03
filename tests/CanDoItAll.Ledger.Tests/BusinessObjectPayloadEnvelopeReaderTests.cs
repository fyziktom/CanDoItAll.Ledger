using CanDoItAll.Economy.BusinessObjects;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectPayloadEnvelopeReaderTests
{
    [Fact]
    public void TryRead_accepts_legacy_string_state_json()
    {
        string inlineJson = "{\"objectType\":\"secure-message\",\"schemaVersion\":\"1.0\",\"stateJson\":\"{\\\"format\\\":\\\"legacy\\\"}\"}";

        Assert.True(BusinessObjectPayloadEnvelopeReader.TryRead(inlineJson, out BusinessObjectPayloadEnvelope envelope));

        Assert.Equal("secure-message", envelope.ObjectType);
        Assert.Equal("1.0", envelope.SchemaVersion);
        Assert.Equal("{\"format\":\"legacy\"}", envelope.StateJson);
    }

    [Fact]
    public void TryRead_accepts_object_valued_state_json()
    {
        string inlineJson = "{\"objectType\":\"secure-message\",\"schemaVersion\":\"1.0\",\"stateJson\":{\"format\":\"structured\",\"attachments\":[]}}";

        Assert.True(BusinessObjectPayloadEnvelopeReader.TryRead(inlineJson, out BusinessObjectPayloadEnvelope envelope));

        Assert.Equal("secure-message", envelope.ObjectType);
        Assert.Equal("1.0", envelope.SchemaVersion);
        Assert.Equal("{\"format\":\"structured\",\"attachments\":[]}", envelope.StateJson);
    }

    [Fact]
    public void TryRead_accepts_state_alias()
    {
        string inlineJson = "{\"objectType\":\"post\",\"schemaVersion\":\"1.0\",\"state\":{\"name\":\"post\"}}";

        Assert.True(BusinessObjectPayloadEnvelopeReader.TryRead(inlineJson, out BusinessObjectPayloadEnvelope envelope));

        Assert.Equal("post", envelope.ObjectType);
        Assert.Equal("{\"name\":\"post\"}", envelope.StateJson);
    }
}
