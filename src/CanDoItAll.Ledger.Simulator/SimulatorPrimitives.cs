using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Ledger.Simulator;

public readonly record struct ScenarioId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct SimulationBookId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct TxId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct TokenId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct SimulationObjectReference(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct HashHex(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct Amount(decimal Value)
{
    public static Amount Zero { get; } = new(0m);

    public static Amount FromInt64(long value) => new(value);

    public static Amount FromDecimal(decimal value) => new(value);

    public void EnsureNonNegative(string fieldName)
    {
        if (Value < 0m)
        {
            throw new ArgumentOutOfRangeException(fieldName, "Amount must be non-negative.");
        }
    }

    public override string ToString() => Value.ToString("0.#############################", CultureInfo.InvariantCulture);
}

public readonly record struct Address(string Value)
{
    private const byte AddressVersion = 0x35;
    private const int PublicKeyHashLength = 20;
    private const int ChecksumLength = 4;
    private const string AddressPrefix = "cdia1";
    private const string Base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private const int EncodedAddressByteLength = 1 + PublicKeyHashLength + ChecksumLength;

    public static Address FromPublicKey(ReadOnlySpan<byte> publicKeyMaterial)
    {
        byte[] body = BuildAddressBody(publicKeyMaterial);
        byte[] checksum = CalculateChecksum(body);
        byte[] addressBytes = new byte[body.Length + ChecksumLength];
        Buffer.BlockCopy(body, 0, addressBytes, 0, body.Length);
        Buffer.BlockCopy(checksum, 0, addressBytes, body.Length, ChecksumLength);
        return new Address(AddressPrefix + EncodeBase58(addressBytes));
    }

    public static bool TryParse(string? value, out Address address)
    {
        address = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length != value.Trim().Length)
        {
            return false;
        }

        if (!value.StartsWith(AddressPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string encoded = value[AddressPrefix.Length..];
        if (encoded.Length == 0 || !TryDecodeBase58(encoded, out byte[] decoded))
        {
            return false;
        }

        if (decoded.Length != EncodedAddressByteLength || decoded[0] != AddressVersion)
        {
            return false;
        }

        byte[] body = decoded[..^ChecksumLength];
        byte[] expectedChecksum = CalculateChecksum(body);
        ReadOnlySpan<byte> actualChecksum = decoded.AsSpan(decoded.Length - ChecksumLength, ChecksumLength);
        if (!actualChecksum.SequenceEqual(expectedChecksum))
        {
            return false;
        }

        address = new Address(value);
        return true;
    }

    public override string ToString() => Value;

    private static byte[] BuildAddressBody(ReadOnlySpan<byte> publicKeyMaterial)
    {
        if (publicKeyMaterial.IsEmpty)
        {
            throw new ArgumentException("Public key material is required.", nameof(publicKeyMaterial));
        }

        byte[] publicKeyHash = SHA256.HashData(publicKeyMaterial);
        byte[] body = new byte[1 + PublicKeyHashLength];
        body[0] = AddressVersion;
        Buffer.BlockCopy(publicKeyHash, 0, body, 1, PublicKeyHashLength);
        return body;
    }

    private static byte[] CalculateChecksum(byte[] body)
    {
        byte[] first = SHA256.HashData(body);
        byte[] second = SHA256.HashData(first);
        byte[] checksum = new byte[ChecksumLength];
        Buffer.BlockCopy(second, 0, checksum, 0, ChecksumLength);
        return checksum;
    }

    private static string EncodeBase58(ReadOnlySpan<byte> data)
    {
        var value = new BigInteger(data, isUnsigned: true, isBigEndian: true);
        var builder = new StringBuilder();

        while (value > BigInteger.Zero)
        {
            value = BigInteger.DivRem(value, 58, out BigInteger remainder);
            builder.Insert(0, Base58Alphabet[(int)remainder]);
        }

        foreach (byte item in data)
        {
            if (item != 0)
            {
                break;
            }

            builder.Insert(0, Base58Alphabet[0]);
        }

        return builder.ToString();
    }

    private static bool TryDecodeBase58(string encoded, out byte[] data)
    {
        data = [];
        var value = BigInteger.Zero;

        foreach (char character in encoded)
        {
            int index = Base58Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            value = (value * 58) + index;
        }

        byte[] decoded = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        int leadingZeroCount = encoded.TakeWhile(x => x == Base58Alphabet[0]).Count();
        if (leadingZeroCount > 0)
        {
            byte[] withLeadingZeros = new byte[leadingZeroCount + decoded.Length];
            Buffer.BlockCopy(decoded, 0, withLeadingZeros, leadingZeroCount, decoded.Length);
            decoded = withLeadingZeros;
        }

        data = decoded;
        return true;
    }
}

public sealed record OutPoint(TxId TxId, int OutputIndex);

public sealed record TokenTransfer(TokenId TokenId, Amount Amount);

public sealed record SimulationAccountContext(SimulationBookId BookId, ScenarioId? ScenarioId = null);

public enum BusinessObjectOperation
{
    Create = 0,
    Update = 1,
    Transfer = 2,
    Close = 3
}

public enum TransactionDataItemType
{
    Generic = 0,
    TokenMetadata = 1,
    BusinessObject = 2,
    AccountSettings = 3,
    ScenarioMetadata = 4,
    SnapshotMetadata = 5,
    Permission = 6,
    Rule = 7
}

public sealed record CreateBusinessObjectRequest(
    string ObjectType,
    string StateJson,
    string SchemaVersion = "1.0",
    Address? OwnerAddress = null,
    Amount? MainAmount = null,
    TokenTransfer? Token = null,
    TransactionDataItemType ItemType = TransactionDataItemType.BusinessObject,
    string ContentType = "application/json",
    string? ExternalRef = null,
    OutPoint? SourceOutPoint = null);

public sealed record PaymentRequest(Address ReceiverAddress, Amount Amount);

public sealed record ValidationError(string Code, string Message)
{
    public override string ToString() => $"{Code}: {Message}";
}

public sealed record SimulatorSubmitResult(
    bool Accepted,
    TxId? TxId,
    long? Sequence,
    IReadOnlyList<ValidationError> Errors)
{
    public static SimulatorSubmitResult Confirmed(TxId txId, long sequence) => new(true, txId, sequence, []);

    public static SimulatorSubmitResult Rejected(IReadOnlyList<ValidationError> errors) => new(false, null, null, errors);
}

public sealed record AccountRuleExecutionResult(
    string RuleId,
    SimulationObjectReference? TriggerObjectReference,
    AccountRuleActionKind ActionKind,
    AccountRuleExecutionStatus Status,
    TxId? TxId,
    string Message)
{
    public TxId? TriggerTransactionId { get; init; }
}

public enum AccountRuleExecutionStatus
{
    Executed = 0,
    Skipped = 1,
    Failed = 2
}

public interface ITimeProvider
{
    DateTimeOffset UtcNow { get; }
}

public sealed class ManualTimeProvider : ITimeProvider
{
    private readonly object gate = new();
    private DateTimeOffset utcNow;

    public ManualTimeProvider(DateTimeOffset utcNow)
    {
        this.utcNow = utcNow.ToUniversalTime();
    }

    public DateTimeOffset UtcNow
    {
        get
        {
            lock (gate)
            {
                return utcNow;
            }
        }
    }

    public void SetUtcNow(DateTimeOffset value)
    {
        lock (gate)
        {
            utcNow = value.ToUniversalTime();
        }
    }

    public DateTimeOffset Advance(TimeSpan duration)
    {
        lock (gate)
        {
            utcNow = utcNow.Add(duration);
            return utcNow;
        }
    }
}

public abstract class BusinessObjectAccount
{
    public abstract Task<SimulatorSubmitResult> CreateBusinessObjectAsync(
        CreateBusinessObjectRequest request,
        CancellationToken cancellationToken);
}

public static class RuleTokenAuthority
{
    public const string RuleContentType = "application/vnd.candoitall.rule+json";

    public static bool TryValidate(AccountRuleDefinition rule, out IReadOnlyList<string> errors)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(rule.RuleId))
        {
            result.Add("Rule id is required.");
        }

        if (string.IsNullOrWhiteSpace(rule.RuleNamespace))
        {
            result.Add("Rule namespace is required.");
        }

        if (rule.Conditions.Count == 0 || rule.Conditions.Any(string.IsNullOrWhiteSpace))
        {
            result.Add("Rule must include at least one condition expression.");
        }

        if (rule.Actions.Count == 0)
        {
            result.Add("Rule must include at least one action.");
        }

        foreach (AccountRuleAction action in rule.Actions)
        {
            ValidateAction(action, result);
        }

        errors = result;
        return result.Count == 0;
    }

    private static void ValidateAction(AccountRuleAction action, List<string> errors)
    {
        switch (action.Kind)
        {
            case AccountRuleActionKind.Payment:
            case AccountRuleActionKind.TokenTransfer:
                if (string.IsNullOrWhiteSpace(action.ReceiverAddress))
                {
                    errors.Add($"{action.Kind} action requires a receiver address or receiver variable.");
                }

                if (string.IsNullOrWhiteSpace(action.AmountExpression))
                {
                    errors.Add($"{action.Kind} action requires an amount expression.");
                }

                if (action.Kind == AccountRuleActionKind.TokenTransfer && string.IsNullOrWhiteSpace(action.TokenId))
                {
                    errors.Add("Token transfer action requires a token id.");
                }

                break;
            case AccountRuleActionKind.TransferBusinessObject:
                if (string.IsNullOrWhiteSpace(action.ReceiverAddress))
                {
                    errors.Add("TransferBusinessObject action requires a receiver address or receiver variable.");
                }

                break;
            case AccountRuleActionKind.UpdateBusinessObject:
                if (string.IsNullOrWhiteSpace(action.StateJson))
                {
                    errors.Add("UpdateBusinessObject action requires state JSON.");
                }

                break;
            case AccountRuleActionKind.MarkProcessed:
                break;
            default:
                errors.Add($"Unsupported rule action kind: {action.Kind}.");
                break;
        }
    }
}

public sealed record AccountRuleDefinition(
    string RuleId,
    string RuleNamespace,
    bool Enabled,
    AccountRuleTrigger? Trigger,
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyList<string> Conditions,
    IReadOnlyList<AccountRuleAction> Actions,
    AccountRuleProcessingOptions? Processing);

public sealed record AccountRuleTrigger(
    AccountRuleTriggerKind Kind,
    string? ObjectType = null,
    string? ProcessedProperty = "processed")
{
    public static AccountRuleTrigger Default { get; } = new(AccountRuleTriggerKind.BusinessObject);
}

public sealed record AccountRuleAction(
    AccountRuleActionKind Kind,
    string? ReceiverAddress = null,
    string? AmountExpression = null,
    string? TokenId = null,
    string? StateJson = null,
    string? ObjectIdExpression = null);

public sealed record AccountRuleProcessingOptions(
    bool MarkTriggerProcessed = true,
    string ProcessedProperty = "processed",
    bool KeepProcessedBusinessObject = true,
    int MaxActionsPerRefresh = 10)
{
    public static AccountRuleProcessingOptions Default { get; } = new();
}

public enum AccountRuleTriggerKind
{
    AccountSnapshot = 0,
    BusinessObject = 1,
    NewTransaction = 2
}

public enum AccountRuleActionKind
{
    Payment = 0,
    TokenTransfer = 1,
    TransferBusinessObject = 2,
    UpdateBusinessObject = 3,
    MarkProcessed = 4
}
