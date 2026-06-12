using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Economy.ConsoleDemo;

internal static class DemoProgram
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static async Task<int> RunAsync(string[] args)
    {
        DemoOptions options = DemoOptions.Parse(args);
        if (options.ListLessons)
        {
            LearningScenarios.PrintLessonCatalog();
            return 0;
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        CancellationToken cancellationToken = cancellation.Token;

        try
        {
            ConsoleUi.Title("CanDoItAll Economy SDK learning demo");
            ConsoleUi.KeyValue("Node", options.NodeBaseUri.ToString());
            ConsoleUi.KeyValue("Ledger", options.Context.LedgerId.Value);
            ConsoleUi.KeyValue("Scenario", options.Context.ScenarioId?.Value ?? "(default)");
            ConsoleUi.KeyValue("Lesson", options.Lesson ?? "all");
            ConsoleUi.KeyValue("Key root", options.KeyRoot);

            await ProbeNodeAsync(options.NodeBaseUri, cancellationToken).ConfigureAwait(false);

            var keyStore = new DemoKeyStore(options.KeyRoot);
            using DemoAccountDriver alice = await DemoAccountDriver.CreateAsync("alice", options, keyStore, cancellationToken).ConfigureAwait(false);
            using DemoAccountDriver bob = await DemoAccountDriver.CreateAsync("bob", options, keyStore, cancellationToken).ConfigureAwait(false);

            var lessons = new LearningScenarios(options, keyStore, alice, bob);
            if (options.Lesson is null)
            {
                await lessons.Learn01SdkCompositionAndAccountLoadingAsync(cancellationToken).ConfigureAwait(false);
                await lessons.Learn02AccountReadsAsync(cancellationToken).ConfigureAwait(false);
                await lessons.Learn03MainPaymentAsync(cancellationToken).ConfigureAwait(false);
                await lessons.Learn04TokenMintAndTransferAsync(cancellationToken).ConfigureAwait(false);
                await lessons.Learn05BusinessObjectDataItemsAndViewsAsync(cancellationToken).ConfigureAwait(false);
                await lessons.Learn06BusinessObjectTransferAndTransactionDetailsAsync(cancellationToken).ConfigureAwait(false);
                await lessons.Learn07AdvancedBusinessObjectTransactionsAsync(cancellationToken).ConfigureAwait(false);
                await lessons.Learn08SubAccountsExportsAndMessageSecurityAsync(cancellationToken).ConfigureAwait(false);
                await lessons.Learn09AccountAutoRefreshAsync(cancellationToken).ConfigureAwait(false);
                await lessons.Learn10ExpectedFailuresAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await RunSelectedLessonAsync(lessons, options.Lesson, cancellationToken).ConfigureAwait(false);
            }

            ConsoleUi.Section("Done");
            ConsoleUi.Success("Demo completed. Keys were loaded from or stored under the external test key folder.");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleUi.Section("Demo stopped");
            ConsoleUi.Error(ex.Message);
            return 1;
        }
    }

    private static Task RunSelectedLessonAsync(
        LearningScenarios lessons,
        string lesson,
        CancellationToken cancellationToken)
        => lesson.Trim() switch
        {
            "01" or "1" => lessons.Learn01SdkCompositionAndAccountLoadingAsync(cancellationToken),
            "02" or "2" => lessons.Learn02AccountReadsAsync(cancellationToken),
            "03" or "3" => lessons.Learn03MainPaymentAsync(cancellationToken),
            "04" or "4" => lessons.Learn04TokenMintAndTransferAsync(cancellationToken),
            "05" or "5" => lessons.Learn05BusinessObjectDataItemsAndViewsAsync(cancellationToken),
            "06" or "6" => lessons.Learn06BusinessObjectTransferAndTransactionDetailsAsync(cancellationToken),
            "07" or "7" => lessons.Learn07AdvancedBusinessObjectTransactionsAsync(cancellationToken),
            "08" or "8" => lessons.Learn08SubAccountsExportsAndMessageSecurityAsync(cancellationToken),
            "09" or "9" => lessons.Learn09AccountAutoRefreshAsync(cancellationToken),
            "10" => lessons.Learn10ExpectedFailuresAsync(cancellationToken),
            _ => throw new ArgumentException($"Unknown lesson '{lesson}'. Use --list-lessons to see available lessons.")
        };

    private static async Task ProbeNodeAsync(Uri nodeBaseUri, CancellationToken cancellationToken)
    {
        ConsoleUi.Section("Node check");
        using var httpClient = new HttpClient { BaseAddress = nodeBaseUri };
        HealthResponse? health = await httpClient
            .GetFromJsonAsync<HealthResponse>("api/v1/health", JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        ConsoleUi.Success($"Health: {health?.Status ?? "unknown"} at {health?.UtcNow:O}");

        NodeBootstrapInfo? bootstrap = await httpClient
            .GetFromJsonAsync<NodeBootstrapInfo>("api/v1/node/bootstrap", JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (bootstrap is not null)
        {
            ConsoleUi.Info(
                $"Node bootstrap ledger '{bootstrap.LedgerId}', API {bootstrap.ApiBasePath}, " +
                $"treasury address {ConsoleUi.Short(bootstrap.AdminAddress)}.");
        }
    }

    private sealed record HealthResponse(string Status, DateTimeOffset UtcNow);

    private sealed record NodeBootstrapInfo(
        string LedgerId,
        bool BootstrapEnabled,
        bool GenesisCreated,
        string AdminAddress,
        string KeyId,
        string Algorithm,
        string ApiVersion,
        string ApiBasePath,
        string? LegacyApiBasePath);
}
