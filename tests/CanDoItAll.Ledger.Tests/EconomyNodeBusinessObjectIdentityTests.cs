using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Node;
using CanDoItAll.Economy.Persistence.EFCore;
using CanDoItAll.Economy.Persistence.EFCore.Records;
using CanDoItAll.Economy.Sdk;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Economy.Tests;

public sealed class EconomyNodeBusinessObjectIdentityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void LegacyBusinessObjectCreationMarker_PreservesJsonCompatibilityWithoutClaimingPublicIdentity()
    {
        var request = new MintTransactionRequest(
            "address",
            0m,
            "ops.object",
            1m,
            TransactionDataItemType.BusinessObject.ToString(),
            "{}",
            null,
            "user-selected-object-name",
            null);

        string json = JsonSerializer.Serialize(request, JsonOptions);

        Assert.Contains("\"businessObjectId\":\"user-selected-object-name\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("legacyBusinessObjectCreationMarker", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VersionOneBusinessObjectRef_KeepsHistoricalObjectIdWireNameForOpaqueLineage()
    {
        var reference = new BusinessObjectRef(
            new BusinessObjectLineageKey("lineage:opaque-test"),
            1,
            BusinessObjectOperation.Create,
            new HashHex(new string('a', 64)));

        string json = JsonSerializer.Serialize(reference, JsonOptions);

        Assert.Contains("\"objectId\":{\"value\":\"lineage:opaque-test\"}", json, StringComparison.Ordinal);
        Assert.DoesNotContain("lineageKey", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Node_AssignsConfirmedOutpointIds_AndResolvesPermissionsFromDataInsteadOfLineageText()
    {
        string databaseName = $"bo-identity-{Guid.NewGuid():N}";
        WebApplicationBuilder builder = EconomyNodeHost.CreateBuilder(
            ["--urls", "http://127.0.0.1:0"],
            options =>
            {
                options.InMemoryDatabaseName = databaseName;
                options.AuthInMemoryDatabaseName = databaseName + "-auth";
                options.ApiSecurityMode = EconomyNodeApiSecurityMode.Development;
                options.ReadSecurityMode = EconomyNodeApiReadSecurityMode.Public;
                options.BootstrapTreasuryAdmin = true;
                options.TreasuryAdminMainAmount = 1_000m;
                options.EnableRateLimiting = false;
                options.MapLegacyApiRoutes = false;
            });
        await using WebApplication app = EconomyNodeHost.Build(builder);
        await EconomyNodeHost.InitializeAsync(app.Services);
        await app.StartAsync();

        try
        {
            PermissionGrantReceipt grant;
            TransactionReceipt businessObjectMint;
            TransactionDetails details;
            string adminAddress;
            string internalLineageKey;
            const string legacyMarker = "bo:post:user-selected-name";

            await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
            {
                EconomyNodeHostOptions options = scope.ServiceProvider.GetRequiredService<EconomyNodeHostOptions>();
                EconomyTreasuryCommandService commands = scope.ServiceProvider.GetRequiredService<EconomyTreasuryCommandService>();
                EconomyLedgerQueryService queries = scope.ServiceProvider.GetRequiredService<EconomyLedgerQueryService>();
                adminAddress = options.TreasuryAdminAddress;

                grant = await commands.GrantPermissionAsync(
                    new PermissionGrantRequest(
                        "ops",
                        adminAddress,
                        Amount: 3m,
                        Repeatable: true,
                        Action: PermissionTokenAuthority.MintNewTokenTypeAction,
                        ScenarioId: null),
                    CancellationToken.None);

                Assert.Equal($"{grant.TxId}:0", grant.PermissionObjectId);
                Assert.True(new BusinessObjectId(grant.PermissionObjectId).IsCanonicalOutPointId());

                businessObjectMint = await commands.MintAsync(
                    new MintTransactionRequest(
                        adminAddress,
                        0m,
                        "ops.post",
                        1m,
                        TransactionDataItemType.BusinessObject.ToString(),
                        "{\"objectType\":\"post\",\"schemaVersion\":\"1.0\",\"stateJson\":{\"name\":\"Demo post\"},\"references\":[]}",
                        null,
                        legacyMarker,
                        null),
                    CancellationToken.None);

                details = Assert.IsType<TransactionDetails>(await queries.GetTransactionAsync(
                    businessObjectMint.TxId,
                    includeData: true,
                    CancellationToken.None));
                TransactionOutputDetails publicBusinessObject = Assert.Single(
                    details.Outputs,
                    output => output.OutputIndex == 0);
                Assert.Equal($"{businessObjectMint.TxId}:0", publicBusinessObject.BusinessObjectId);
                Assert.True(new BusinessObjectId(publicBusinessObject.BusinessObjectId!).IsCanonicalOutPointId());

                // The repeatable permission has moved to a new UTXO whose transaction carries no
                // permission JSON. A second mint proves authority is resolved by the retained data
                // hash and its originating transaction, not by parsing the opaque lineage key.
                TransactionReceipt secondMint = await commands.MintAsync(
                    new MintTransactionRequest(
                        adminAddress,
                        0m,
                        "ops.second",
                        1m,
                        null,
                        null,
                        null,
                        null,
                        null),
                    CancellationToken.None);
                Assert.False(string.IsNullOrWhiteSpace(secondMint.TxId));

                IDbContextFactory<EconomyDbContext> factory = scope.ServiceProvider
                    .GetRequiredService<IDbContextFactory<EconomyDbContext>>();
                await using EconomyDbContext db = await factory.CreateDbContextAsync();
                UnspentOutputRecord created = await db.UnspentOutputs
                    .AsNoTracking()
                    .SingleAsync(row => row.TxId == businessObjectMint.TxId && row.OutputIndex == 0);
                internalLineageKey = Assert.IsType<string>(created.BusinessObjectId);
                Assert.StartsWith("lineage:", created.BusinessObjectId, StringComparison.Ordinal);
                Assert.NotEqual(legacyMarker, created.BusinessObjectId);
                Assert.DoesNotContain("post", created.BusinessObjectId!, StringComparison.OrdinalIgnoreCase);
            }

            string baseAddress = Assert.Single(app.Urls);
            using var client = new HttpClient { BaseAddress = new Uri(baseAddress) };
            using HttpResponseMessage invalidBusinessObjectQuery = await client.PostAsJsonAsync(
                "api/v1/ledger/default/business-objects/query",
                new LedgerBusinessObjectQueryRequest(null, "bo:post:user-name", null, null, null, null));
            using HttpResponseMessage invalidTransactionQuery = await client.PostAsJsonAsync(
                "api/v1/ledger/default/transactions/query",
                new LedgerTransactionQueryRequest(null, null, "lineage:opaque-test", null));
            using HttpResponseMessage canonicalQuery = await client.PostAsJsonAsync(
                "api/v1/ledger/default/business-objects/query",
                new LedgerBusinessObjectQueryRequest(
                    null,
                    $"{businessObjectMint.TxId}:0",
                    null,
                    null,
                    adminAddress,
                    null));
            string canonicalObjectId = $"{businessObjectMint.TxId}:0";
            using HttpResponseMessage transactionQuery = await client.PostAsJsonAsync(
                "api/v1/ledger/default/transactions/query",
                new LedgerTransactionQueryRequest(
                    null,
                    null,
                    canonicalObjectId,
                    null,
                    IncludeData: true));
            using HttpResponseMessage transactionGet = await client.GetAsync(
                $"api/v1/ledger/default/transactions/{businessObjectMint.TxId}?includeData=true");
            using HttpResponseMessage accountTransactions = await client.GetAsync(
                $"api/v1/ledger/default/accounts/{Uri.EscapeDataString(adminAddress)}/transactions?includeData=true");
            using HttpResponseMessage spendableOutputs = await client.GetAsync(
                $"api/v1/ledger/default/accounts/{Uri.EscapeDataString(adminAddress)}/spendable-outputs");

            Assert.Equal(HttpStatusCode.BadRequest, invalidBusinessObjectQuery.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, invalidTransactionQuery.StatusCode);
            Assert.Equal(HttpStatusCode.OK, canonicalQuery.StatusCode);
            Assert.Equal(HttpStatusCode.OK, transactionQuery.StatusCode);
            Assert.Equal(HttpStatusCode.OK, transactionGet.StatusCode);
            Assert.Equal(HttpStatusCode.OK, accountTransactions.StatusCode);
            Assert.Equal(HttpStatusCode.OK, spendableOutputs.StatusCode);

            AssertPublicTransactionIdentity(
                await transactionQuery.Content.ReadAsStringAsync(),
                canonicalObjectId,
                internalLineageKey);
            AssertPublicTransactionIdentity(
                await transactionGet.Content.ReadAsStringAsync(),
                canonicalObjectId,
                internalLineageKey);
            AssertPublicTransactionIdentity(
                await accountTransactions.Content.ReadAsStringAsync(),
                canonicalObjectId,
                internalLineageKey);

            string spendableJson = await spendableOutputs.Content.ReadAsStringAsync();
            Assert.Contains(
                $"\"objectId\":{{\"value\":\"{canonicalObjectId}\"}}",
                spendableJson,
                StringComparison.Ordinal);
            Assert.Contains(
                $"\"lineageKey\":{{\"value\":\"{internalLineageKey}\"}}",
                spendableJson,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                $"\"objectId\":{{\"value\":\"{internalLineageKey}\"}}",
                spendableJson,
                StringComparison.Ordinal);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static void AssertPublicTransactionIdentity(
        string json,
        string expectedObjectId,
        string internalLineageKey)
    {
        Assert.Contains(
            $"\"businessObject\":{{\"objectId\":{{\"value\":\"{expectedObjectId}\"}}",
            json,
            StringComparison.Ordinal);
        Assert.DoesNotContain(internalLineageKey, json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"lineageKey\"", json, StringComparison.Ordinal);
    }
}
