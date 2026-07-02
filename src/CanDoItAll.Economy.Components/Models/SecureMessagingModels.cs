using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components.Models;

public sealed record SecureMessageAttachmentDraft(
    string FileName,
    string ContentType,
    byte[] Data,
    string? Description = null)
{
    public long Size => Data.LongLength;
}

public sealed record SecureMessageComposeRequest(
    string AccountAlias,
    string Receiver,
    string Subject,
    string Body,
    IReadOnlyList<SecureMessageAttachmentDraft> Attachments);

public sealed record SecureMessageOpenRequest(
    string AccountAlias,
    TxId TxId,
    SecureLedgerMessagePayload Payload);

public sealed record SecureMessageAttachmentOpenRequest(
    string AccountAlias,
    SecureLedgerMessagePayload Payload,
    SecureLedgerMessageContent Content,
    string AttachmentId);

public sealed record SecureMessageOpenResult(
    bool Success,
    string Summary,
    SecureLedgerMessageContent? Content = null)
{
    public static SecureMessageOpenResult Ok(string summary, SecureLedgerMessageContent content)
        => new(true, summary, content);

    public static SecureMessageOpenResult Fail(string summary)
        => new(false, summary);
}

public sealed record SecureMessageAttachmentOpenResult(
    bool Success,
    string Summary,
    SecureLedgerMessageAttachmentPlaintext? Attachment = null)
{
    public static SecureMessageAttachmentOpenResult Ok(
        string summary,
        SecureLedgerMessageAttachmentPlaintext attachment)
        => new(true, summary, attachment);

    public static SecureMessageAttachmentOpenResult Fail(string summary)
        => new(false, summary);
}
