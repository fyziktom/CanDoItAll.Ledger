using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Sdk;

namespace CanDoItAll.Ledger.ConsoleDemo;

internal sealed class DemoKeyStore(string rootPath)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<StoredDemoKey> LoadOrCreateAsync(string accountName, CancellationToken cancellationToken)
    {
        string directory = Path.Combine(rootPath, "economy-console-demo", "accounts", accountName);
        string path = Path.Combine(directory, "account-key.json");

        if (File.Exists(path))
        {
            string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            LedgerAccountKeyEnvelope envelope = DeserializeKey(json, path);
            if (!CanUseForSigning(envelope))
            {
                string backupPath = Path.Combine(directory, $"account-key.legacy-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json");
                File.Copy(path, backupPath, overwrite: false);
                LedgerAccountKeyEnvelope replacement = CreateAccountKey(accountName);
                string replacementJson = JsonSerializer.Serialize(DemoAccountKeyFile.FromEnvelope(replacement), JsonOptions);
                await File.WriteAllTextAsync(path, replacementJson, cancellationToken).ConfigureAwait(false);
                return new StoredDemoKey(replacement, path, Created: true);
            }

            if (!IsCurrentKeyFileShape(json))
            {
                string normalized = JsonSerializer.Serialize(DemoAccountKeyFile.FromEnvelope(envelope), JsonOptions);
                await File.WriteAllTextAsync(path, normalized, cancellationToken).ConfigureAwait(false);
            }

            return new StoredDemoKey(envelope, path, Created: false);
        }

        Directory.CreateDirectory(directory);
        LedgerAccountKeyEnvelope created = CreateAccountKey(accountName);
        string serialized = JsonSerializer.Serialize(DemoAccountKeyFile.FromEnvelope(created), JsonOptions);
        await File.WriteAllTextAsync(path, serialized, cancellationToken).ConfigureAwait(false);
        return new StoredDemoKey(created, path, Created: true);
    }

    private static LedgerAccountKeyEnvelope CreateAccountKey(string accountName)
    {
        (byte[] publicKeyMaterial, byte[] privateKeyMaterial) = EcdsaP256KeyMaterial.CreateKeyPair();
        return new LedgerAccountKeyEnvelope(
            SignatureAlgorithmId.EcdsaP256Sha256,
            $"console-demo-{accountName}-{Guid.NewGuid():N}",
            publicKeyMaterial,
            privateKeyMaterial);
    }

    private static bool CanUseForSigning(LedgerAccountKeyEnvelope envelope)
        => envelope.Algorithm != SignatureAlgorithmId.EcdsaP256Sha256
            || EcdsaP256KeyMaterial.CanImport(envelope.PublicKeyMaterial, envelope.PrivateKeyMaterial);

    private static LedgerAccountKeyEnvelope DeserializeKey(string json, string path)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        string address = ReadStringOrValueObject(root, "address");
        SignatureAlgorithmId algorithm = ReadAlgorithm(root);
        string keyId = ReadRequiredString(root, "keyId");
        string publicKeyBase64 = ReadOptionalString(root, "publicKeyMaterialBase64")
            ?? ReadRequiredString(root, "publicKeyMaterial");
        string privateKeyBase64 = ReadOptionalString(root, "privateKeyMaterialBase64")
            ?? ReadRequiredString(root, "privateKeyMaterial");
        string? publicKeyHash = ReadOptionalString(root, "publicKeyHash") ?? ReadOptionalValueObject(root, "publicKeyHash");

        try
        {
            return new LedgerAccountKeyEnvelope(
                new Address(address),
                algorithm,
                keyId,
                Convert.FromBase64String(publicKeyBase64),
                Convert.FromBase64String(privateKeyBase64),
                string.IsNullOrWhiteSpace(publicKeyHash) ? null : new HashHex(publicKeyHash));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not read account key from {path}: {ex.Message}", ex);
        }
    }

    private static bool IsCurrentKeyFileShape(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("publicKeyMaterialBase64", out _);
    }

    private static SignatureAlgorithmId ReadAlgorithm(JsonElement root)
    {
        JsonElement value = root.GetProperty("algorithm");
        return value.ValueKind == JsonValueKind.String
            ? Enum.Parse<SignatureAlgorithmId>(value.GetString()!, ignoreCase: true)
            : (SignatureAlgorithmId)value.GetInt32();
    }

    private static string ReadStringOrValueObject(JsonElement root, string propertyName)
    {
        JsonElement value = root.GetProperty(propertyName);
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        return value.GetProperty("value").GetString() ?? string.Empty;
    }

    private static string? ReadOptionalValueObject(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return value.TryGetProperty("value", out JsonElement nested)
            ? nested.GetString()
            : null;
    }

    private static string ReadRequiredString(JsonElement root, string propertyName)
        => ReadOptionalString(root, propertyName)
            ?? throw new InvalidOperationException($"Key file is missing '{propertyName}'.");

    private static string? ReadOptionalString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private sealed record DemoAccountKeyFile(
        string Address,
        SignatureAlgorithmId Algorithm,
        string KeyId,
        string PublicKeyMaterialBase64,
        string PrivateKeyMaterialBase64,
        string PublicKeyHash)
    {
        public static DemoAccountKeyFile FromEnvelope(LedgerAccountKeyEnvelope envelope)
            => new(
                envelope.Address.Value,
                envelope.Algorithm,
                envelope.KeyId,
                Convert.ToBase64String(envelope.PublicKeyMaterial),
                Convert.ToBase64String(envelope.PrivateKeyMaterial),
                (envelope.PublicKeyHash ?? CanDoItAll.Ledger.Core.Address.CalculatePublicKeyHash(envelope.PublicKeyMaterial)).Value);
    }
}

internal sealed record StoredDemoKey(LedgerAccountKeyEnvelope Envelope, string Path, bool Created);
