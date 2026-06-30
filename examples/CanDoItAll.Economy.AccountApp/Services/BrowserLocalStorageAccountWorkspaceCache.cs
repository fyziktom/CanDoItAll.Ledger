using CanDoItAll.Economy.Components.Services;
using Microsoft.JSInterop;

namespace CanDoItAll.Economy.AccountApp.Services;

public sealed class BrowserLocalStorageAccountWorkspaceCache(IJSRuntime jsRuntime) : IAccountWorkspaceCache
{
    public async ValueTask<string?> GetStringAsync(AccountWorkspaceCacheKey key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, key.StorageKey)
                .ConfigureAwait(false);
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async ValueTask SetStringAsync(AccountWorkspaceCacheKey key, string value, CancellationToken cancellationToken = default)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, key.StorageKey, value)
                .ConfigureAwait(false);
        }
        catch (JSException)
        {
            // Cache writes are opportunistic; account functionality must not depend on browser storage availability.
        }
    }
}
