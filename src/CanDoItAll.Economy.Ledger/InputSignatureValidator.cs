using System.Text;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Ledger;

public sealed class InputSignatureValidator(
    ISignaturePayloadBuilder payloadBuilder,
    ISignatureProviderRegistry signatureProviderRegistry)
{
    public ValidationResult Validate(LedgerTransaction transaction)
    {
        if (transaction.Inputs.Count == 0)
        {
            return ValidationResult.Success;
        }

        var errors = new List<ValidationError>();
        foreach (TransactionInput input in transaction.Inputs.OrderBy(x => x.InputIndex))
        {
            HashHex expectedPayloadHash = transaction.SigningMode == SigningMode.Ordered
                ? payloadBuilder.BuildOrderedInputPayloadHash(transaction, input)
                : payloadBuilder.BuildIndependentInputPayloadHash(transaction, input);

            if (input.SignaturePayloadHash != expectedPayloadHash)
            {
                errors.Add(Invalid(input, "signature payload hash is missing or does not match the transaction payload."));
            }

            if (input.SignatureBundle.Signatures.Count == 0)
            {
                errors.Add(Invalid(input, "must contain at least one signature."));
                continue;
            }

            foreach (SignatureEnvelope signature in input.SignatureBundle.Signatures)
            {
                ValidateEnvelope(input, expectedPayloadHash, signature, errors);
            }
        }

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    private void ValidateEnvelope(
        TransactionInput input,
        HashHex expectedPayloadHash,
        SignatureEnvelope signature,
        List<ValidationError> errors)
    {
        if (signature.PayloadHash != expectedPayloadHash)
        {
            errors.Add(Invalid(input, "signature payload hash does not match the expected payload."));
            return;
        }

        if (signature.PublicKeyMaterial is not { Length: > 0 } publicKeyMaterial)
        {
            errors.Add(Invalid(input, "signature is missing public key material."));
            return;
        }

        if (!input.Address.MatchesPublicKey(publicKeyMaterial))
        {
            errors.Add(Invalid(input, "signature public key does not match the input address."));
            return;
        }

        if (Address.CalculatePublicKeyHash(publicKeyMaterial) != signature.PublicKeyHash)
        {
            errors.Add(Invalid(input, "signature public key hash does not match the public key material."));
            return;
        }

        try
        {
            ISignatureProvider provider = signatureProviderRegistry.Get(signature.Algorithm);
            byte[] payload = Encoding.UTF8.GetBytes(expectedPayloadHash.Value);
            if (!provider.Verify(payload, signature.SignatureBytes, publicKeyMaterial))
            {
                errors.Add(Invalid(input, "signature failed verification."));
            }
        }
        catch (Exception ex) when (ex is NotSupportedException or PlatformNotSupportedException or InvalidOperationException or ArgumentException)
        {
            errors.Add(Invalid(input, $"signature could not be verified: {ex.Message}"));
        }
    }

    private static ValidationError Invalid(TransactionInput input, string message)
        => new(ValidationErrorCodes.InvalidSignature, $"Input {input.InputIndex} {message}");
}
