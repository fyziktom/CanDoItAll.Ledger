namespace CanDoItAll.Economy.Components.Models;

public sealed record MessageSecuritySignRequest(
    string AccountAlias,
    string Message);

public sealed record MessageSecurityVerifyRequest(
    string AccountAlias,
    string Message,
    string SignatureJson);

public sealed record MessageSecurityEncryptRequest(
    string AccountAlias,
    string ReceiverAccountAlias,
    string Message);

public sealed record MessageSecurityDecryptRequest(
    string AccountAlias,
    string EncryptedMessageJson);

public sealed record MessageSecurityActionResult(
    bool Success,
    string Summary,
    string? PayloadJson = null,
    string? PlainText = null)
{
    public static MessageSecurityActionResult Ok(
        string summary,
        string? payloadJson = null,
        string? plainText = null)
        => new(true, summary, payloadJson, plainText);

    public static MessageSecurityActionResult Fail(string summary)
        => new(false, summary);
}
