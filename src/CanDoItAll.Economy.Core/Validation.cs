namespace CanDoItAll.Economy.Core;

public sealed record ValidationError(string Code, string Message)
{
    public override string ToString() => $"{Code}: {Message}";
}

public sealed record ValidationResult(IReadOnlyList<ValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static ValidationResult Success { get; } = new(Array.Empty<ValidationError>());

    public static ValidationResult Failure(params ValidationError[] errors) => new(errors);
}

public sealed record LedgerSubmitResult(
    bool Accepted,
    TxId? TxId,
    long? LedgerSequence,
    IReadOnlyList<ValidationError> Errors)
{
    public static LedgerSubmitResult Rejected(IReadOnlyList<ValidationError> errors) => new(false, null, null, errors);

    public static LedgerSubmitResult Confirmed(TxId txId, long ledgerSequence) => new(true, txId, ledgerSequence, Array.Empty<ValidationError>());
}

public sealed class LedgerStoreConflictException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public static class ValidationErrorCodes
{
    public const string DuplicateInput = "duplicate_input";
    public const string DuplicateTransaction = "duplicate_transaction";
    public const string InvalidTransactionShape = "invalid_transaction_shape";
    public const string LedgerStateConflict = "ledger_state_conflict";
    public const string UnknownInput = "unknown_input";
    public const string SpentInput = "spent_input";
    public const string InvalidSignature = "invalid_signature";
    public const string InvalidAddress = "invalid_address";
    public const string InvalidIdentifier = "invalid_identifier";
    public const string InvalidHash = "invalid_hash";
    public const string ResourceLimitExceeded = "resource_limit_exceeded";
    public const string InvalidGenesisSequence = "invalid_genesis_sequence";
    public const string MissingInput = "missing_input";
    public const string AddressMismatch = "address_mismatch";
    public const string AssetImbalance = "asset_imbalance";
    public const string AmountOverflow = "amount_overflow";
    public const string NegativeAmount = "negative_amount";
    public const string UnknownAddress = "unknown_address";
    public const string InvalidBusinessObjectTransition = "invalid_business_object_transition";
    public const string InvalidTokenId = "invalid_token_id";
    public const string InvalidTokenDefinition = "invalid_token_definition";
    public const string InvalidTokenDefinitionId = "invalid_token_definition_id";
    public const string InvalidTokenInitialSupply = "invalid_token_initial_supply";
    public const string InvalidTokenClassification = "invalid_token_classification";
    public const string DuplicateTokenType = "duplicate_token_type";
    public const string UnauthorizedTokenCreation = "unauthorized_token_creation";
    public const string UnauthorizedMainCurrencyIssuance = "unauthorized_main_currency_issuance";
    public const string UnauthorizedTokenAuthorityGrant = "unauthorized_token_authority_grant";
    public const string UnauthorizedRuleTokenCreation = "unauthorized_rule_token_creation";
}
