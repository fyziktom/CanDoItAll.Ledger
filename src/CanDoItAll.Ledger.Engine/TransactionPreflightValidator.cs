using System.Text;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Engine;

/// <summary>
/// Applies cheap null and resource limits before canonical serialization or hashing touches an untrusted envelope.
/// Full protocol shape validation remains in <see cref="TransactionShapeValidator"/>.
/// </summary>
public sealed class TransactionPreflightValidator
{
    public ValidationResult Validate(LedgerTransaction transaction)
    {
        var errors = new List<ValidationError>();

        if (transaction.Inputs is null)
        {
            errors.Add(Invalid("Transaction inputs cannot be null."));
            return new ValidationResult(errors);
        }

        if (transaction.Outputs is null)
        {
            errors.Add(Invalid("Transaction outputs cannot be null."));
            return new ValidationResult(errors);
        }

        if (transaction.TokenDefinition is not null && transaction.TokenDefinition.Definition is null)
        {
            errors.Add(Invalid("Token definition envelope must contain an immutable definition."));
            return new ValidationResult(errors);
        }

        if (transaction.Inputs.Count > LedgerValidationLimits.MaxTransactionInputs)
        {
            errors.Add(Limit($"Transaction cannot contain more than {LedgerValidationLimits.MaxTransactionInputs} inputs."));
        }

        if (transaction.Outputs.Count > LedgerValidationLimits.MaxTransactionOutputs)
        {
            errors.Add(Limit($"Transaction cannot contain more than {LedgerValidationLimits.MaxTransactionOutputs} outputs."));
        }

        if (transaction.Data?.InlineJson is { } inlineJson
            && Encoding.UTF8.GetByteCount(inlineJson) > LedgerValidationLimits.MaxInlineJsonBytes)
        {
            errors.Add(Limit($"Transaction inline JSON cannot exceed {LedgerValidationLimits.MaxInlineJsonBytes} bytes."));
        }

        long totalSignatureBytes = 0;
        long totalPublicKeyBytes = 0;
        foreach (TransactionInput? input in transaction.Inputs)
        {
            if (input is null)
            {
                errors.Add(Invalid("Transaction inputs cannot contain null items."));
                continue;
            }

            if (input.SignatureBundle?.Signatures is null)
            {
                errors.Add(Invalid($"Input {input.InputIndex} signature bundle cannot be null."));
                continue;
            }

            if (input.SignatureBundle.Signatures.Count > LedgerValidationLimits.MaxSignaturesPerInput)
            {
                errors.Add(Limit($"Input {input.InputIndex} cannot contain more than {LedgerValidationLimits.MaxSignaturesPerInput} signatures."));
            }

            foreach (SignatureEnvelope? signature in input.SignatureBundle.Signatures)
            {
                if (signature is null)
                {
                    errors.Add(Invalid($"Input {input.InputIndex} signature bundle cannot contain null items."));
                    continue;
                }

                if (signature.SignatureBytes is null)
                {
                    errors.Add(Invalid($"Input {input.InputIndex} signature bytes cannot be null."));
                    continue;
                }

                totalSignatureBytes += signature.SignatureBytes.LongLength;
                totalPublicKeyBytes += signature.PublicKeyMaterial?.LongLength ?? 0;
            }
        }

        if (transaction.Outputs.Any(output => output is null))
        {
            errors.Add(Invalid("Transaction outputs cannot contain null items."));
        }

        if (totalSignatureBytes > LedgerValidationLimits.MaxTransactionSignatureBytes)
        {
            errors.Add(Limit($"Transaction signatures cannot exceed {LedgerValidationLimits.MaxTransactionSignatureBytes} bytes in total."));
        }

        if (totalPublicKeyBytes > LedgerValidationLimits.MaxTransactionPublicKeyMaterialBytes)
        {
            errors.Add(Limit($"Transaction public key material cannot exceed {LedgerValidationLimits.MaxTransactionPublicKeyMaterialBytes} bytes in total."));
        }

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    private static ValidationError Invalid(string message)
        => new(ValidationErrorCodes.InvalidTransactionShape, message);

    private static ValidationError Limit(string message)
        => new(ValidationErrorCodes.ResourceLimitExceeded, message);
}
