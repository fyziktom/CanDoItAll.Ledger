using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace CanDoItAll.Ledger.Node;

internal sealed class LedgerNodeAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor)
    : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        ClaimsPrincipal user = httpContextAccessor.HttpContext?.User
            ?? new ClaimsPrincipal(new ClaimsIdentity());
        return Task.FromResult(new AuthenticationState(user));
    }
}
