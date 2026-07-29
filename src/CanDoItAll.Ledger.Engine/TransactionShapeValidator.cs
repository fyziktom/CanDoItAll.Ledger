using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Engine;

public sealed class TransactionShapeValidator
{
    public ValidationResult ValidateShape(LedgerTransaction transaction)
    {
        var errors = new List<ValidationError>();

        if (transaction.Version is not (1 or 2))
        {
            errors.Add(new ValidationError("unsupported_transaction_version", "Transaction version must be 1 or 2."));
        }

        if (transaction.Version == 1 && transaction.TokenDefinition is not null)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidTokenDefinition,
                "Protocol-v1 transactions cannot carry protocol-v2 token definitions."));
        }

        if (!Enum.IsDefined(transaction.Kind))
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidTransactionShape,
                $"Transaction kind is not supported: {(int)transaction.Kind}."));
        }

        if (!Enum.IsDefined(transaction.SigningMode))
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidTransactionShape,
                $"Signing mode is not supported: {(int)transaction.SigningMode}."));
        }

        if (transaction.Outputs.Count == 0 && transaction.Kind != TransactionKind.Burn)
        {
            errors.Add(new ValidationError("missing_outputs", "Transaction must have at least one output unless it is a burn transaction."));
        }

        if (transaction.Inputs.Count > LedgerValidationLimits.MaxTransactionInputs)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.ResourceLimitExceeded,
                $"Transaction cannot contain more than {LedgerValidationLimits.MaxTransactionInputs} inputs."));
        }

        if (transaction.Outputs.Count > LedgerValidationLimits.MaxTransactionOutputs)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.ResourceLimitExceeded,
                $"Transaction cannot contain more than {LedgerValidationLimits.MaxTransactionOutputs} outputs."));
        }

        ValidateIdentifier(errors, transaction.LedgerId.Value, LedgerValidationLimits.MaxLedgerIdLength, "ledger id");
        if (transaction.ScenarioId is { } scenarioId)
        {
            ValidateIdentifier(errors, scenarioId.Value, LedgerValidationLimits.MaxScenarioIdLength, "scenario id");
        }

        if (transaction.TxId is { } txId)
        {
            ValidateIdentifier(errors, txId.Value, LedgerValidationLimits.MaxTxIdLength, "transaction id");
        }

        if (transaction.TxHash is { } txHash)
        {
            ValidateHash(errors, txHash.Value, "transaction hash");
        }

        ValidateTransactionData(errors, transaction.Data);

        if (transaction.Kind != TransactionKind.Genesis && transaction.Inputs.Count == 0)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.MissingInput,
                "Only the first genesis transaction may have no inputs."));
        }

        if (transaction.Kind == TransactionKind.Genesis && transaction.Inputs.Count > 0)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidTransactionShape,
                "Genesis transactions cannot spend previous outputs."));
        }

        foreach (var output in transaction.Outputs)
        {
            if (output.OutputIndex < 0)
            {
                errors.Add(new ValidationError(ValidationErrorCodes.InvalidTransactionShape, $"Output index must be non-negative: {output.OutputIndex}."));
            }

            if (!Address.IsValid(output.Address.Value))
            {
                errors.Add(new ValidationError(ValidationErrorCodes.InvalidAddress, $"Output {output.OutputIndex} address is not a valid derived address: {output.Address.Value}."));
            }

            if (output.Token is not null)
            {
                ValidateIdentifier(errors, output.Token.TokenId.Value, LedgerValidationLimits.MaxTokenIdLength, $"output {output.OutputIndex} token id");
            }

            if (output.BusinessObject is not null)
            {
                ValidateIdentifier(errors, output.BusinessObject.LineageKey.Value, LedgerValidationLimits.MaxBusinessObjectIdLength, $"output {output.OutputIndex} business object lineage key");
                ValidateHash(errors, output.BusinessObject.DataHash.Value, $"output {output.OutputIndex} business object data hash");
            }

            if (output.MetadataHash is not null)
            {
                ValidateHash(errors, output.MetadataHash.Value.Value, $"output {output.OutputIndex} metadata hash");
            }

            if (output.MainAmount.Value < 0)
            {
                errors.Add(new ValidationError(ValidationErrorCodes.NegativeAmount, $"Output {output.OutputIndex} has negative main amount."));
            }

            if (output.Token is not null && output.Token.Amount.Value < 0)
            {
                errors.Add(new ValidationError(ValidationErrorCodes.NegativeAmount, $"Output {output.OutputIndex} has negative token amount."));
            }
            else if (output.Token is not null && output.Token.Amount.Value == 0m)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.InvalidTransactionShape,
                    $"Output {output.OutputIndex} token amount must be positive."));
            }

            if (output.MainAmount.Value == 0m && output.Token is null)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.InvalidTransactionShape,
                    $"Output {output.OutputIndex} carries no spendable value. Use a positive main amount or positive token amount."));
            }

            ValidateTokenOutputSeparation(errors, output);
            ValidateNormalBusinessObjectCarrier(errors, transaction.Data, output);
        }

        foreach (var input in transaction.Inputs)
        {
            if (input.InputIndex < 0)
            {
                errors.Add(new ValidationError(ValidationErrorCodes.InvalidTransactionShape, $"Input index must be non-negative: {input.InputIndex}."));
            }

            if (!Address.IsValid(input.Address.Value))
            {
                errors.Add(new ValidationError(ValidationErrorCodes.InvalidAddress, $"Input {input.InputIndex} address is not a valid derived address: {input.Address.Value}."));
            }

            ValidateIdentifier(errors, input.PreviousOutPoint.TxId.Value, LedgerValidationLimits.MaxTxIdLength, $"input {input.InputIndex} previous transaction id");

            if (input.PreviousOutPoint.OutputIndex < 0)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.InvalidTransactionShape,
                    $"Input {input.InputIndex} previous output index must be non-negative."));
            }

            if (input.SignaturePayloadHash is not null)
            {
                ValidateHash(errors, input.SignaturePayloadHash.Value.Value, $"input {input.InputIndex} signature payload hash");
            }

            if (input.SignatureBundle.Signatures.Count > LedgerValidationLimits.MaxSignaturesPerInput)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCodes.ResourceLimitExceeded,
                    $"Input {input.InputIndex} cannot contain more than {LedgerValidationLimits.MaxSignaturesPerInput} signatures."));
            }

            foreach (SignatureEnvelope signature in input.SignatureBundle.Signatures)
            {
                ValidateSignature(errors, input.InputIndex, signature);
            }
        }

        var duplicateInputIndexes = transaction.Inputs
            .GroupBy(x => x.InputIndex)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToArray();

        foreach (var duplicateIndex in duplicateInputIndexes)
        {
            errors.Add(new ValidationError(ValidationErrorCodes.InvalidTransactionShape, $"Duplicate input index: {duplicateIndex}."));
        }

        var duplicateOutputIndexes = transaction.Outputs
            .GroupBy(x => x.OutputIndex)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToArray();

        foreach (var duplicateIndex in duplicateOutputIndexes)
        {
            errors.Add(new ValidationError(ValidationErrorCodes.InvalidTransactionShape, $"Duplicate output index: {duplicateIndex}."));
        }

        var duplicateInputs = transaction.Inputs
            .GroupBy(x => $"{x.PreviousOutPoint.TxId.Value}:{x.PreviousOutPoint.OutputIndex}:{x.Address.Value}")
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToArray();

        foreach (var duplicate in duplicateInputs)
        {
            errors.Add(new ValidationError(ValidationErrorCodes.DuplicateInput, $"Duplicate input: {duplicate}"));
        }

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    private static void ValidateTransactionData(List<ValidationError> errors, TransactionData? data)
    {
        if (data is null)
        {
            return;
        }

        if (!Enum.IsDefined(data.ItemType))
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidTransactionShape,
                $"Transaction data item type is not supported: {(int)data.ItemType}."));
        }

        if (!LedgerValueValidation.IsSafeText(data.ContentType, LedgerValidationLimits.MaxContentTypeLength, allowEmpty: true))
        {
            errors.Add(new ValidationError(ValidationErrorCodes.InvalidIdentifier, "Transaction data content type is invalid or too long."));
        }

        if (data.InlineJson is not null && data.DataHash is null)
        {
            errors.Add(new ValidationError(ValidationErrorCodes.InvalidHash, "Transaction inline JSON must declare a data hash."));
        }

        if (data.DataHash is not null)
        {
            ValidateHash(errors, data.DataHash.Value.Value, "transaction data hash");
        }

        ValidateInlineJsonHash(errors, data);

        if (!LedgerValueValidation.IsSafeText(data.ExternalRef, LedgerValidationLimits.MaxExternalRefLength, allowEmpty: true))
        {
            errors.Add(new ValidationError(ValidationErrorCodes.InvalidIdentifier, "Transaction data external reference is invalid or too long."));
        }

        if (data.InlineJson is not null && System.Text.Encoding.UTF8.GetByteCount(data.InlineJson) > LedgerValidationLimits.MaxInlineJsonBytes)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.ResourceLimitExceeded,
                $"Transaction inline JSON cannot exceed {LedgerValidationLimits.MaxInlineJsonBytes} bytes."));
        }
    }

    private static void ValidateTokenOutputSeparation(List<ValidationError> errors, TransactionOutput output)
    {
        if (output.Token is null || output.MainAmount.Value == 0m)
        {
            return;
        }

        errors.Add(new ValidationError(
            ValidationErrorCodes.InvalidTransactionShape,
            $"Output {output.OutputIndex} carries both token and main amount. Token outputs must use 0 main amount; create a separate plain main-currency output."));
    }

    private static void ValidateNormalBusinessObjectCarrier(
        List<ValidationError> errors,
        TransactionData? data,
        TransactionOutput output)
    {
        if (output.BusinessObject is null || IsAuthorityBusinessObjectOutput(data, output))
        {
            return;
        }

        if (output.Token is null)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidTransactionShape,
                $"Output {output.OutputIndex} carries a BusinessObject without a token carrier. Normal BusinessObject outputs must carry exactly 1 token and 0 main amount."));
            return;
        }

        if (output.Token.Amount.Value != 1m)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidTransactionShape,
                $"Output {output.OutputIndex} carries a BusinessObject with token amount {output.Token.Amount}. Normal BusinessObject outputs must carry exactly 1 token."));
        }
    }

    private static bool IsAuthorityBusinessObjectOutput(TransactionData? data, TransactionOutput output)
        => data?.ItemType is TransactionDataItemType.Permission or TransactionDataItemType.Rule
            || PermissionTokenAuthority.IsPermissionToken(output.Token)
            || RuleTokenAuthority.IsRuleToken(output.Token);

    private static void ValidateInlineJsonHash(List<ValidationError> errors, TransactionData data)
    {
        if (data.InlineJson is null || data.DataHash is null)
        {
            return;
        }

        string actualHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data.InlineJson))).ToLowerInvariant();
        if (!string.Equals(actualHash, data.DataHash.Value.Value, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ValidationError(ValidationErrorCodes.InvalidHash, "Transaction data hash does not match inline JSON content."));
        }
    }

    private static void ValidateSignature(List<ValidationError> errors, int inputIndex, SignatureEnvelope signature)
    {
        if (!LedgerValueValidation.IsValidIdentifier(signature.KeyId, LedgerValidationLimits.MaxKeyIdLength))
        {
            errors.Add(new ValidationError(ValidationErrorCodes.InvalidIdentifier, $"Input {inputIndex} signature key id is invalid."));
        }

        if (!LedgerValueValidation.IsSafeText(signature.Algorithm.ToString(), LedgerValidationLimits.MaxAlgorithmLength))
        {
            errors.Add(new ValidationError(ValidationErrorCodes.InvalidIdentifier, $"Input {inputIndex} signature algorithm is invalid."));
        }

        ValidateHash(errors, signature.PublicKeyHash.Value, $"input {inputIndex} signature public key hash");
        ValidateHash(errors, signature.PayloadHash.Value, $"input {inputIndex} signature payload hash");

        if (signature.SignatureBytes.Length > LedgerValidationLimits.MaxSignatureBytes)
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.ResourceLimitExceeded,
                $"Input {inputIndex} signature bytes exceed {LedgerValidationLimits.MaxSignatureBytes} bytes."));
        }

        if (signature.PublicKeyMaterial is { Length: > LedgerValidationLimits.MaxPublicKeyMaterialBytes })
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.ResourceLimitExceeded,
                $"Input {inputIndex} signature public key material exceeds {LedgerValidationLimits.MaxPublicKeyMaterialBytes} bytes."));
        }
    }

    private static void ValidateIdentifier(List<ValidationError> errors, string? value, int maxLength, string name)
    {
        if (!LedgerValueValidation.IsValidIdentifier(value, maxLength))
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidIdentifier,
                $"{name} contains unsupported characters, is empty, or is too long."));
        }
    }

    private static void ValidateHash(List<ValidationError> errors, string? value, string name)
    {
        if (!LedgerValueValidation.IsValidHashHex(value))
        {
            errors.Add(new ValidationError(
                ValidationErrorCodes.InvalidHash,
                $"{name} must be hex text with an even length up to {LedgerValidationLimits.MaxHashHexLength}."));
        }
    }
}
