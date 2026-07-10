using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Node;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace CanDoItAll.Economy.Tests;

public sealed class EconomyNodeJwtScopeTests
{
    private const string SigningKey = "scope-test-signing-key-that-is-at-least-thirty-two-bytes";

    [Theory]
    [InlineData(EconomyNodeScopes.Read, EconomyNodeAuthPolicies.ReadApi, true)]
    [InlineData(EconomyNodeScopes.Write, EconomyNodeAuthPolicies.ReadApi, true)]
    [InlineData(EconomyNodeScopes.Write, EconomyNodeAuthPolicies.WriteApi, true)]
    [InlineData(EconomyNodeScopes.Admin, EconomyNodeAuthPolicies.AdminApi, true)]
    [InlineData(EconomyNodeScopes.Read, EconomyNodeAuthPolicies.WriteApi, false)]
    [InlineData(EconomyNodeScopes.Write, EconomyNodeAuthPolicies.AdminApi, false)]
    [InlineData(EconomyNodeScopes.LegacyNodeApi, EconomyNodeAuthPolicies.AdminApi, true)]
    [InlineData("unknown.scope", EconomyNodeAuthPolicies.ReadApi, false)]
    [InlineData("", EconomyNodeAuthPolicies.ReadApi, false)]
    public async Task ScopePolicies_ApplyHierarchyAndDenyByDefault(
        string scope,
        string policy,
        bool expected)
    {
        var services = new ServiceCollection();
        services.AddEconomyNode(new ConfigurationBuilder().Build());
        using ServiceProvider provider = services.BuildServiceProvider();
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim("scope", scope));
        var principal = new ClaimsPrincipal(identity);

        AuthorizationResult result = await provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(principal, resource: null, policy);

        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public async Task JwtEndpoints_ReadWriteAndAdminScopesCannotCrossPrivilegeBoundaries()
    {
        WebApplicationBuilder builder = EconomyNodeHost.CreateBuilder(
            ["--urls", "http://127.0.0.1:0"],
            options =>
            {
                options.ApiSecurityMode = EconomyNodeApiSecurityMode.RequireJwtBearer;
                options.ReadSecurityMode = EconomyNodeApiReadSecurityMode.RequireJwtBearer;
                options.JwtSigningKey = SigningKey;
                options.JwtRequireActiveTokenRecord = false;
                options.BootstrapTreasuryAdmin = false;
                options.EnableRateLimiting = false;
                options.AllowLedgerReset = true;
            });
        await using WebApplication app = EconomyNodeHost.Build(builder);
        await EconomyNodeHost.InitializeAsync(app.Services);
        await app.StartAsync();
        try
        {
            string baseAddress = Assert.Single(app.Urls);
            using var client = new HttpClient { BaseAddress = new Uri(baseAddress) };

            using HttpResponseMessage readAllowed = await SendAsync(client, HttpMethod.Get, "api/v1/node/bootstrap", EconomyNodeScopes.Read);
            Assert.Equal(HttpStatusCode.OK, readAllowed.StatusCode);

            using HttpResponseMessage readDeniedWrite = await SendJsonAsync(
                client,
                "api/v1/ledger/transactions",
                CreateMalformedButBoundTransaction(),
                EconomyNodeScopes.Read);
            Assert.Equal(HttpStatusCode.Forbidden, readDeniedWrite.StatusCode);

            using HttpResponseMessage writeAllowed = await SendJsonAsync(
                client,
                "api/v1/ledger/transactions",
                CreateMalformedButBoundTransaction(),
                EconomyNodeScopes.Write);
            Assert.Equal(HttpStatusCode.OK, writeAllowed.StatusCode);

            using HttpResponseMessage writeDeniedAdmin = await SendJsonAsync(
                client,
                "api/v1/ledger/reset",
                new { },
                EconomyNodeScopes.Write);
            Assert.Equal(HttpStatusCode.Forbidden, writeDeniedAdmin.StatusCode);

            using HttpResponseMessage adminAllowed = await SendJsonAsync(
                client,
                "api/v1/ledger/reset",
                new { },
                EconomyNodeScopes.Admin);
            Assert.Equal(HttpStatusCode.OK, adminAllowed.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static LedgerTransaction CreateMalformedButBoundTransaction()
        => new(
            new LedgerId("scope-test"),
            null,
            Version: 1,
            TransactionKind.Transfer,
            SigningMode.Independent,
            DateTimeOffset.Parse("2026-07-10T02:00:00Z"),
            [],
            [],
            null);

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string scope)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(scope));
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendJsonAsync<T>(
        HttpClient client,
        string path,
        T body,
        string scope)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(scope));
        return client.SendAsync(request);
    }

    private static string CreateToken(string scope)
    {
        DateTime now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: EconomyNodeHostDefaults.JwtIssuer,
            audience: EconomyNodeHostDefaults.JwtAudience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, "scope-test-user"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim("scope", scope)
            ],
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
