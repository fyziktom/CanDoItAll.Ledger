using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.Ledger.Components.Services;

public readonly record struct AccountWorkspaceCacheKey(string Category, string Scope, string Id)
{
    public string StorageKey
        => $"candoitall:economy:account-workspace-cache:v1:{Safe(Category)}:{Safe(Scope)}:{Safe(Id)}";

    private static string Safe(string value)
        => Convert.ToHexString(Encoding.UTF8.GetBytes(value)).ToLowerInvariant();
}

public interface IAccountWorkspaceCache
{
    ValueTask<string?> GetStringAsync(AccountWorkspaceCacheKey key, CancellationToken cancellationToken = default);

    ValueTask SetStringAsync(AccountWorkspaceCacheKey key, string value, CancellationToken cancellationToken = default);
}

public sealed class InMemoryAccountWorkspaceCache : IAccountWorkspaceCache
{
    private readonly ConcurrentDictionary<string, string> entries = new(StringComparer.Ordinal);

    public ValueTask<string?> GetStringAsync(AccountWorkspaceCacheKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        entries.TryGetValue(key.StorageKey, out string? value);
        return ValueTask.FromResult(value);
    }

    public ValueTask SetStringAsync(AccountWorkspaceCacheKey key, string value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        entries[key.StorageKey] = value;
        return ValueTask.CompletedTask;
    }
}

internal static class AccountWorkspaceCacheExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<T?> GetJsonAsync<T>(
        this IAccountWorkspaceCache cache,
        AccountWorkspaceCacheKey key,
        CancellationToken cancellationToken = default)
        where T : class
    {
        string? json = await cache.GetStringAsync(key, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static async Task SetJsonAsync<T>(
        this IAccountWorkspaceCache cache,
        AccountWorkspaceCacheKey key,
        T value,
        CancellationToken cancellationToken = default)
        where T : class
    {
        string json = JsonSerializer.Serialize(value, JsonOptions);
        await cache.SetStringAsync(key, json, cancellationToken).ConfigureAwait(false);
    }
}
