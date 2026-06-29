using CanDoItAll.Economy.Cryptography;
using Microsoft.JSInterop;

namespace CanDoItAll.Economy.Cryptography.Browser;

public sealed class BrowserEconomyCryptoDriver : IEconomyCryptoDriver, IAsyncDisposable
{
    public const string DefaultModulePath = "./_content/CanDoItAll.Economy.Cryptography.Browser/candoitall-economy-crypto.js";

    private readonly IJSRuntime jsRuntime;
    private readonly string modulePath;
    private readonly SemaphoreSlim moduleLock = new(1, 1);
    private IJSObjectReference? module;

    public BrowserEconomyCryptoDriver(IJSRuntime jsRuntime)
        : this(jsRuntime, DefaultModulePath)
    {
    }

    public BrowserEconomyCryptoDriver(IJSRuntime jsRuntime, string modulePath)
    {
        this.jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        this.modulePath = string.IsNullOrWhiteSpace(modulePath)
            ? throw new ArgumentException("Browser crypto module path is required.", nameof(modulePath))
            : modulePath;
    }

    public async ValueTask<byte[]> GetRandomBytesAsync(int length, CancellationToken cancellationToken = default)
    {
        IJSObjectReference crypto = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        string result = await crypto.InvokeAsync<string>("getRandomBytes", cancellationToken, length).ConfigureAwait(false);
        return FromBase64(result);
    }

    public async ValueTask<byte[]> Sha256Async(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        IJSObjectReference crypto = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        string result = await crypto.InvokeAsync<string>("sha256", cancellationToken, ToBase64(data)).ConfigureAwait(false);
        return FromBase64(result);
    }

    public async ValueTask<byte[]> Pbkdf2Sha256Async(
        ReadOnlyMemory<byte> password,
        ReadOnlyMemory<byte> salt,
        int iterations,
        int byteCount,
        CancellationToken cancellationToken = default)
    {
        IJSObjectReference crypto = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        string result = await crypto.InvokeAsync<string>(
            "pbkdf2Sha256",
            cancellationToken,
            ToBase64(password),
            ToBase64(salt),
            iterations,
            byteCount).ConfigureAwait(false);
        return FromBase64(result);
    }

    public async ValueTask<byte[]> AesGcmEncryptAsync(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> plaintext,
        ReadOnlyMemory<byte> associatedData,
        CancellationToken cancellationToken = default)
    {
        IJSObjectReference crypto = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        string result = await crypto.InvokeAsync<string>(
            "aesGcmEncrypt",
            cancellationToken,
            ToBase64(key),
            ToBase64(nonce),
            ToBase64(plaintext),
            ToBase64(associatedData)).ConfigureAwait(false);
        return FromBase64(result);
    }

    public async ValueTask<byte[]> AesGcmDecryptAsync(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> ciphertextWithTag,
        ReadOnlyMemory<byte> associatedData,
        CancellationToken cancellationToken = default)
    {
        IJSObjectReference crypto = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        string result = await crypto.InvokeAsync<string>(
            "aesGcmDecrypt",
            cancellationToken,
            ToBase64(key),
            ToBase64(nonce),
            ToBase64(ciphertextWithTag),
            ToBase64(associatedData)).ConfigureAwait(false);
        return FromBase64(result);
    }

    public async ValueTask<EcdsaP256KeyPair> CreateEcdsaP256KeyPairAsync(CancellationToken cancellationToken = default)
    {
        IJSObjectReference crypto = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        BrowserEcdsaP256KeyPair result = await crypto.InvokeAsync<BrowserEcdsaP256KeyPair>(
            "createEcdsaP256KeyPair",
            cancellationToken).ConfigureAwait(false);
        return new EcdsaP256KeyPair(FromBase64(result.PublicKeyMaterialBase64), FromBase64(result.PrivateKeyMaterialBase64));
    }

    public async ValueTask<byte[]> SignEcdsaP256Sha256Async(
        ReadOnlyMemory<byte> payload,
        ReadOnlyMemory<byte> privateKeyMaterial,
        CancellationToken cancellationToken = default)
    {
        IJSObjectReference crypto = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        string result = await crypto.InvokeAsync<string>(
            "signEcdsaP256Sha256",
            cancellationToken,
            ToBase64(payload),
            ToBase64(privateKeyMaterial)).ConfigureAwait(false);
        return FromBase64(result);
    }

    public async ValueTask<bool> VerifyEcdsaP256Sha256Async(
        ReadOnlyMemory<byte> payload,
        ReadOnlyMemory<byte> signature,
        ReadOnlyMemory<byte> publicKeyMaterial,
        CancellationToken cancellationToken = default)
    {
        IJSObjectReference crypto = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await crypto.InvokeAsync<bool>(
            "verifyEcdsaP256Sha256",
            cancellationToken,
            ToBase64(payload),
            ToBase64(signature),
            ToBase64(publicKeyMaterial)).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            await module.DisposeAsync().ConfigureAwait(false);
        }

        moduleLock.Dispose();
    }

    internal async ValueTask<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken)
    {
        if (module is not null)
        {
            return module;
        }

        await moduleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, modulePath).ConfigureAwait(false);
            return module;
        }
        finally
        {
            moduleLock.Release();
        }
    }

    internal static string ToBase64(ReadOnlyMemory<byte> bytes)
        => Convert.ToBase64String(bytes.Span);

    internal static byte[] FromBase64(string value)
        => string.IsNullOrEmpty(value) ? Array.Empty<byte>() : Convert.FromBase64String(value);

    private sealed class BrowserEcdsaP256KeyPair
    {
        public string PublicKeyMaterialBase64 { get; set; } = string.Empty;

        public string PrivateKeyMaterialBase64 { get; set; } = string.Empty;
    }
}
