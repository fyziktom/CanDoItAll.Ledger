using System.Text;
using System.Text.Json;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Persistence.EFCore;
using CanDoItAll.Economy.Persistence.PostgreSql;
using CanDoItAll.Economy.Sdk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Economy.Cli;

public static class Program
{
    private const string DefaultPostgreSqlConnectionString =
        "Host=localhost;Port=54329;Username=economy;Password=economy;Database=candoitall_economy";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly IReadOnlyList<CliCommand> Commands =
    [
        new("verify chain", "Verify confirmed ledger chain history.", VerifyChainAsync),
        new("verify projections", "Verify derived ledger projections.", VerifyProjectionsAsync),
        new("projections rebuild", "Rebuild derived ledger projections from confirmed history.", RebuildProjectionsAsync),
        new("snapshot create", "Create and sign an EF ledger snapshot.", CreateSnapshotAsync),
        new("snapshot verify", "Verify an EF ledger snapshot.", VerifySnapshotAsync),
        new("node health", "Read node liveness or readiness health.", NodeHealthAsync),
        new("node bootstrap", "Read node bootstrap and security metadata.", NodeBootstrapAsync),
        new("time advance", "Advance node simulation time.", AdvanceTimeAsync),
        new("ledger reset", "Reset the node ledger when enabled by node configuration.", ResetLedgerAsync),
        new("transaction submit", "Submit a raw ledger transaction JSON file to the node.", SubmitTransactionAsync),
        new("transaction get", "Read node transaction details by transaction id.", GetTransactionAsync),
        new("transaction mint", "Mint main and optional token funds through the node helper endpoint.", MintTransactionAsync),
        new("transaction transfer", "Transfer main and optional token funds through the node helper endpoint.", TransferTransactionAsync),
        new("permission grant", "Grant token namespace permission through the node helper endpoint.", GrantPermissionAsync),
        new("account balance", "Read an account balance through the node driver.", AccountBalanceAsync),
        new("account transactions", "Read account transaction summaries through the node driver.", AccountTransactionsAsync)
    ];

    public static async Task<int> Main(string[] args)
    {
        CommandLine commandLine = CommandLine.Parse(args);
        if (commandLine.Command.Count == 0 || commandLine.Command[0] is "help" or "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            CliCommand? command = Commands.FirstOrDefault(candidate => candidate.Matches(commandLine.Command));
            return command is null
                ? UnknownCommand(commandLine)
                : await command.Execute(commandLine, cancellation.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or JsonException or InvalidOperationException or DbUpdateException or EconomyNodeRequestException)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Command canceled.");
            return 130;
        }
    }

    private sealed record CliCommand(
        string Pattern,
        string Description,
        Func<CommandLine, CancellationToken, Task<int>> Execute)
    {
        private readonly string[] segments = Pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        public bool Matches(IReadOnlyList<string> command)
            => command.Count == segments.Length
               && command.Zip(segments).All(pair => string.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<int> VerifyChainAsync(CommandLine commandLine, CancellationToken cancellationToken)
    {
        await using ServiceProvider services = BuildServices(commandLine);
        var ledgerId = new LedgerId(commandLine.Get("ledger", "default"));
        ScenarioId? scenarioId = ToScenarioId(commandLine.Get("scenario", null));
        IReadOnlyList<ConfirmedTransaction> history = await services
            .GetRequiredService<EfLedgerHistoryReader>()
            .ReadConfirmedTransactionsAsync(ledgerId, scenarioId, cancellationToken)
            .ConfigureAwait(false);
        ITransactionHasher hasher = services.GetRequiredService<ITransactionHasher>();
        ISignatureProviderRegistry registry = services.GetRequiredService<ISignatureProviderRegistry>();
        LedgerAdminVerificationKey[] trustedKeys = LoadTrustedAdminKeys().ToArray();
        var verifier = new LedgerChainVerifier(
            hasher,
            services.GetRequiredService<ISignaturePayloadBuilder>(),
            trustedKeys.Length == 0 ? null : registry,
            trustedKeys);
        LedgerChainVerificationReport report = verifier.Verify(history);

        Console.WriteLine($"{(report.IsValid ? "OK" : "INVALID")} chain ledger={ledgerId.Value} sequence={report.HighestSequence} hash={report.VerifiedLedgerHash.Value} transactions={report.TransactionCount}");
        foreach (LedgerChainVerificationIssue issue in report.Issues)
        {
            Console.WriteLine($"{issue.Code} seq={issue.LedgerSequence} tx={issue.TxId?.Value ?? "<none>"} {issue.Message}");
        }

        return report.IsValid ? 0 : 1;
    }

    private static async Task<int> VerifyProjectionsAsync(CommandLine commandLine, CancellationToken cancellationToken)
    {
        await using ServiceProvider services = BuildServices(commandLine);
        EfLedgerProjectionVerificationReport report = await services
            .GetRequiredService<EfLedgerProjectionVerifier>()
            .VerifyAsync(
                new LedgerId(commandLine.Get("ledger", "default")),
                ToScenarioId(commandLine.Get("scenario", null)),
                cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine($"{(report.IsValid ? "OK" : "INVALID")} projections sequence={report.HashSet.CurrentSequence} projectionHash={report.HashSet.ProjectionHash.Value}");
        foreach (EfLedgerProjectionVerificationIssue issue in report.Issues)
        {
            Console.WriteLine($"{issue.Code} scope={issue.Scope} {issue.Message}");
        }

        return report.IsValid ? 0 : 1;
    }

    private static async Task<int> RebuildProjectionsAsync(CommandLine commandLine, CancellationToken cancellationToken)
    {
        await using ServiceProvider services = BuildServices(commandLine);
        EfLedgerProjectionRebuildResult result = await services
            .GetRequiredService<EfLedgerProjectionRebuilder>()
            .RebuildAsync(
                new LedgerId(commandLine.Get("ledger", "default")),
                ToScenarioId(commandLine.Get("scenario", null)),
                cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine($"OK projections rebuilt transactions={result.TransactionCount} utxos={result.UnspentOutputCount} spent={result.SpentOutputCount} addresses={result.AddressProjectionCount} businessObjects={result.BusinessObjectCount} sequence={result.CurrentSequence}");
        return 0;
    }

    private static async Task<int> CreateSnapshotAsync(CommandLine commandLine, CancellationToken cancellationToken)
    {
        await using ServiceProvider services = BuildServices(commandLine);
        IDbContextFactory<EconomyDbContext> factory = services.GetRequiredService<IDbContextFactory<EconomyDbContext>>();
        EfLedgerProjectionVerifier verifier = services.GetRequiredService<EfLedgerProjectionVerifier>();
        ISignatureProviderRegistry registry = services.GetRequiredService<ISignatureProviderRegistry>();
        using CliLedgerAdminSigner signer = LoadAdminSigner();
        var snapshots = new EfLedgerSnapshotService(factory, verifier, signer, registry);
        LedgerSnapshot snapshot = await snapshots
            .CreateSnapshotAsync(
                new LedgerId(commandLine.Get("ledger", "default")),
                ToScenarioId(commandLine.Get("scenario", null)),
                cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine($"OK snapshot id={snapshot.SnapshotId} sequence={snapshot.LedgerSequence} ledgerHash={snapshot.LedgerHash.Value}");
        return 0;
    }

    private static async Task<int> VerifySnapshotAsync(CommandLine commandLine, CancellationToken cancellationToken)
    {
        string? snapshotId = commandLine.Get("snapshot-id", null);
        if (string.IsNullOrWhiteSpace(snapshotId))
        {
            throw new InvalidOperationException("snapshot verify requires --snapshot-id <id>.");
        }

        await using ServiceProvider services = BuildServices(commandLine);
        var snapshots = new EfLedgerSnapshotService(
            services.GetRequiredService<IDbContextFactory<EconomyDbContext>>(),
            services.GetRequiredService<EfLedgerProjectionVerifier>(),
            null,
            services.GetRequiredService<ISignatureProviderRegistry>());
        EfLedgerSnapshotVerificationReport report = await snapshots
            .VerifySnapshotAsync(snapshotId, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine($"{(report.IsValid ? "OK" : "INVALID")} snapshot id={snapshotId}");
        foreach (EfLedgerSnapshotVerificationIssue issue in report.Issues)
        {
            Console.WriteLine($"{issue.Code} scope={issue.Scope} {issue.Message}");
        }

        return report.IsValid ? 0 : 1;
    }

    private static async Task<int> NodeHealthAsync(CommandLine commandLine, CancellationToken cancellationToken)
    {
        using EconomyNodeLedgerClient client = CreateNodeClient(commandLine);
        NodeHealthResponse health = commandLine.GetBool("readiness", false)
            ? await client.GetReadinessAsync(cancellationToken).ConfigureAwait(false)
            : await client.GetHealthAsync(cancellationToken).ConfigureAwait(false);

        WriteResult(commandLine, health, $"OK health status={health.Status} ledger={health.LedgerId} sequence={health.LatestLedgerSequence} verification={health.VerificationStatus}");
        return 0;
    }

    private static async Task<int> NodeBootstrapAsync(CommandLine commandLine, CancellationToken cancellationToken)
    {
        using EconomyNodeLedgerClient client = CreateNodeClient(commandLine);
        NodeBootstrapInfo bootstrap = await client.GetBootstrapInfoAsync(cancellationToken).ConfigureAwait(false);
        WriteResult(commandLine, bootstrap, $"OK bootstrap ledger={bootstrap.LedgerId} api={bootstrap.ApiBasePath} admin={bootstrap.AdminAddress}");
        return 0;
    }

    private static async Task<int> AdvanceTimeAsync(CommandLine commandLine, CancellationToken cancellationToken)
    {
        using EconomyNodeLedgerClient client = CreateNodeClient(commandLine);
        AdvanceTimeResponse response = await client
            .AdvanceTimeAsync(
                new AdvanceTimeRequest(
                    GetDouble(commandLine, "days", 0d),
                    GetDouble(commandLine, "hours", 0d)),
                cancellationToken)
            .ConfigureAwait(false);
        WriteResult(commandLine, response, $"OK time utcNow={response.UtcNow:u}");
        return 0;
    }

    private static async Task<int> ResetLedgerAsync(CommandLine commandLine, CancellationToken cancellationToken)
    {
        using EconomyNodeLedgerClient client = CreateNodeClient(commandLine);
        LedgerResetResponse response = await client.ResetLedgerAsync(cancellationToken).ConfigureAwait(false);
        WriteResult(commandLine, response, $"OK ledger {response.Status}");
        return 0;
    }

    private static async Task<int> SubmitTransactionAsync(CommandLine commandLine, CancellationToken cancellationToken)
    {
        string path = Require(commandLine, "file", "transaction submit requires --file <path>.");
        string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        LedgerTransaction transaction = JsonSerializer.Deserialize<LedgerTransaction>(json, JsonOptions)
            ?? throw new InvalidOperationException("Transaction file did not contain a ledger transaction.");
        using EconomyNodeLedgerClient client = CreateNodeClient(commandLine);
        LedgerSubmitResult result = await client.SubmitTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
        WriteResult(commandLine, result, $"OK transaction accepted={result.Accepted} tx={result.TxId?.Value ?? "<none>"} sequence={result.LedgerSequence?.ToString() ?? "<none>"}");
        return result.Accepted ? 0 : 1;
    }

    private static async Task<int> GetTransactionAsync(CommandLine commandLine, CancellationToken cancellationToken)
    {
        string txId = Require(commandLine, "tx-id", "transaction get requires --tx-id <id>.");
        using EconomyNodeLedgerClient client = CreateNodeClient(commandLine);
        EconomyNodeTransactionDetails? details = await client
            .GetTransactionAsync(new TxId(txId), commandLine.GetBool("include-data", false), cancellationToken)
            .ConfigureAwait(false);
        if (details is null)
        {
            Console.Error.WriteLine($"Transaction not found: {txId}");
            return 1;
        }

        WriteResult(commandLine, details, $"OK transaction tx={details.TxId} sequence={details.LedgerSequence} kind={details.Kind} status={details.Status}");
        return 0;
    }

    private static async Task<int> MintTransactionAsync(CommandLine commandLine, CancellationToken cancellationToken)
    {
        using EconomyNodeLedgerClient client = CreateNodeClient(commandLine);
        TransactionReceipt receipt = await client
            .MintAsync(
                new MintTransactionRequest(
                    Require(commandLine, "address", "transaction mint requires --address <address>."),
                    GetDecimal(commandLine, "main-amount", 0m),
                    commandLine.GetOptional("token-id"),
                    commandLine.GetNullableDecimal("token-amount"),
                    commandLine.GetOptional("data-item-type"),
                    commandLine.GetOptional("data-json"),
                    commandLine.GetOptional("external-ref"),
                    null,
                    commandLine.GetOptional("scenario")),
                cancellationToken)
            .ConfigureAwait(false);
        WriteResult(commandLine, receipt, $"OK mint tx={receipt.TxId} sequence={receipt.LedgerSequence}");
        return 0;
    }

    private static async Task<int> TransferTransactionAsync(CommandLine commandLine, CancellationToken cancellationToken)
    {
        using EconomyNodeLedgerClient client = CreateNodeClient(commandLine);
        TransactionReceipt receipt = await client
            .TransferAsync(
                new TransferTransactionRequest(
                    Require(commandLine, "from", "transaction transfer requires --from <address>."),
                    Require(commandLine, "to", "transaction transfer requires --to <address>."),
                    GetDecimal(commandLine, "main-amount", 0m),
                    commandLine.GetOptional("token-id"),
                    commandLine.GetNullableDecimal("token-amount"),
                    commandLine.GetOptional("data-item-type"),
                    commandLine.GetOptional("data-json"),
                    commandLine.GetOptional("external-ref"),
                    null,
                    commandLine.GetOptional("scenario")),
                cancellationToken)
            .ConfigureAwait(false);
        WriteResult(commandLine, receipt, $"OK transfer tx={receipt.TxId} sequence={receipt.LedgerSequence}");
        return 0;
    }

    private static async Task<int> GrantPermissionAsync(CommandLine commandLine, CancellationToken cancellationToken)
    {
        using EconomyNodeLedgerClient client = CreateNodeClient(commandLine);
        PermissionGrantReceipt receipt = await client
            .GrantPermissionAsync(
                new PermissionGrantRequest(
                    Require(commandLine, "namespace", "permission grant requires --namespace <token namespace>."),
                    Require(commandLine, "address", "permission grant requires --address <address>."),
                    commandLine.GetNullableDecimal("amount") ?? 1m,
                    commandLine.GetNullableBool("repeatable") ?? true,
                    commandLine.GetOptional("action") ?? PermissionTokenAuthority.MintNewTokenTypeAction,
                    commandLine.GetOptional("scenario")),
                cancellationToken)
            .ConfigureAwait(false);
        WriteResult(commandLine, receipt, $"OK permission object={receipt.PermissionObjectId} tx={receipt.TxId} sequence={receipt.LedgerSequence}");
        return 0;
    }

    private static async Task<int> AccountBalanceAsync(CommandLine commandLine, CancellationToken cancellationToken)
    {
        using EconomyNodeLedgerClient client = CreateNodeClient(commandLine);
        LedgerAccountBalance balance = await client
            .GetBalanceAsync(CreateLedgerAddressQuery(commandLine), cancellationToken)
            .ConfigureAwait(false);
        WriteResult(commandLine, balance, $"OK balance address={balance.Address.Value} main={balance.MainBalance.Value} tokens={balance.TokenBalances.Count} utxos={balance.UtxoCount}");
        return 0;
    }

    private static async Task<int> AccountTransactionsAsync(CommandLine commandLine, CancellationToken cancellationToken)
    {
        using EconomyNodeLedgerClient client = CreateNodeClient(commandLine);
        IReadOnlyList<LedgerTransactionSummary> transactions = await client
            .GetTransactionsAsync(CreateLedgerAddressQuery(commandLine), commandLine.GetBool("include-data", false), cancellationToken)
            .ConfigureAwait(false);
        WriteResult(commandLine, transactions, $"OK transactions count={transactions.Count}");
        return 0;
    }

    private static EconomyNodeLedgerClient CreateNodeClient(CommandLine commandLine)
    {
        string node = commandLine.Get(
            "node",
            Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_NODE") ?? "http://localhost:5000/");
        var options = new EconomyNodeLedgerClientOptions
        {
            ApiBasePath = commandLine.Get("api-base", "api/v1"),
            ApiKeyHeaderName = commandLine.Get("api-key-header", "X-CDA-Economy-Key"),
            ApiKey = commandLine.GetOptional("api-key") ?? Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_API_KEY"),
            BearerToken = commandLine.GetOptional("bearer") ?? Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_BEARER_TOKEN")
        };

        return new EconomyNodeLedgerClient(new Uri(EnsureTrailingSlash(node), UriKind.Absolute), options);
    }

    private static LedgerAddressQuery CreateLedgerAddressQuery(CommandLine commandLine)
        => new(
            new LedgerAccountContext(
                new LedgerId(commandLine.Get("ledger", "default")),
                ToScenarioId(commandLine.Get("scenario", null))),
            new Address(Require(commandLine, "address", "account command requires --address <address>.")));

    private static void WriteResult<T>(CommandLine commandLine, T value, string text)
    {
        if (commandLine.GetBool("json", false))
        {
            Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
            return;
        }

        Console.WriteLine(text);
    }

    private static string Require(CommandLine commandLine, string name, string message)
    {
        string? value = commandLine.GetOptional(name);
        return string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException(message) : value;
    }

    private static decimal GetDecimal(CommandLine commandLine, string name, decimal fallback)
        => commandLine.GetNullableDecimal(name) ?? fallback;

    private static double GetDouble(CommandLine commandLine, string name, double fallback)
        => double.TryParse(commandLine.GetOptional(name), out double value) ? value : fallback;

    private static string EnsureTrailingSlash(string value)
        => value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";

    private static ServiceProvider BuildServices(CommandLine commandLine)
    {
        var services = new ServiceCollection();
        string provider = commandLine.Get("provider", "postgres");
        if (string.Equals(provider, "inmemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddEconomyInMemoryPersistence(commandLine.Get("database", "economy-cli"));
        }
        else
        {
            string connectionString = commandLine.Get(
                "connection",
                Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_CONNECTION")
                    ?? DefaultPostgreSqlConnectionString);
            services.AddEconomyPostgreSqlPersistence(connectionString);
        }

        services.AddScoped<ITransactionCanonicalizer, DefaultTransactionCanonicalizer>();
        services.AddScoped<ITransactionHasher, TransactionHasher>();
        services.AddScoped<ISignaturePayloadBuilder, SignaturePayloadBuilder>();
        services.AddSingleton<ISignatureProviderRegistry>(_ => SignatureProviderRegistry.CreateDefault());
        return services.BuildServiceProvider(validateScopes: false);
    }

    private static IEnumerable<LedgerAdminVerificationKey> LoadTrustedAdminKeys()
    {
        string? publicKeyText = Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_LEDGER_ADMIN_PUBLIC_KEY");
        if (string.IsNullOrWhiteSpace(publicKeyText))
        {
            yield break;
        }

        byte[] publicKey = DecodeKeyMaterial(publicKeyText);
        yield return new LedgerAdminVerificationKey(
            Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_LEDGER_ADMIN_KEY_ID") ?? "node-ledger-admin",
            ParseAlgorithm(Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_LEDGER_ADMIN_ALGORITHM")),
            Address.CalculatePublicKeyHash(publicKey),
            publicKey);
    }

    private static CliLedgerAdminSigner LoadAdminSigner()
    {
        string? publicKeyText = Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_LEDGER_ADMIN_PUBLIC_KEY");
        string? privateKeyText = Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_LEDGER_ADMIN_PRIVATE_KEY");
        if (string.IsNullOrWhiteSpace(publicKeyText) || string.IsNullOrWhiteSpace(privateKeyText))
        {
            throw new InvalidOperationException("snapshot create requires CANDOITALL_ECONOMY_LEDGER_ADMIN_PUBLIC_KEY and CANDOITALL_ECONOMY_LEDGER_ADMIN_PRIVATE_KEY.");
        }

        return new CliLedgerAdminSigner(
            Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_LEDGER_ADMIN_KEY_ID") ?? "node-ledger-admin",
            ParseAlgorithm(Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_LEDGER_ADMIN_ALGORITHM")),
            DecodeKeyMaterial(publicKeyText),
            DecodeKeyMaterial(privateKeyText));
    }

    private static SignatureAlgorithmId ParseAlgorithm(string? value)
        => Enum.TryParse(value, ignoreCase: true, out SignatureAlgorithmId algorithm)
            ? algorithm
            : SignatureAlgorithmId.EcdsaP256Sha256;

    private static byte[] DecodeKeyMaterial(string value)
    {
        string trimmed = value.Trim();
        try
        {
            byte[] decoded = Convert.FromBase64String(trimmed);
            if (decoded.Length > 0)
            {
                return decoded;
            }
        }
        catch (FormatException)
        {
        }

        return Encoding.UTF8.GetBytes(trimmed);
    }

    private static ScenarioId? ToScenarioId(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new ScenarioId(value.Trim());

    private static int UnknownCommand(CommandLine commandLine)
    {
        Console.Error.WriteLine($"Unknown command: {string.Join(' ', commandLine.Command)}");
        PrintHelp();
        return 2;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("CanDoItAll.Economy CLI");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        foreach (CliCommand command in Commands)
        {
            Console.WriteLine($"  {command.Pattern,-24} {command.Description}");
        }

        Console.WriteLine();
        Console.WriteLine("Common EF options:");
        Console.WriteLine("  --provider postgres|inmemory --connection <connection> --database <name> --ledger <id> --scenario <id>");
        Console.WriteLine("Common node options:");
        Console.WriteLine("  --node <uri> --api-base <path> --api-key <key> --bearer <jwt> --json");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  economy account balance --node http://localhost:5000 --address <address>");
        Console.WriteLine("  economy transaction mint --address <address> --main-amount 100 --api-key <key>");
        Console.WriteLine("  economy transaction submit --file transaction.json --bearer <jwt>");
        Console.WriteLine();
        Console.WriteLine("Deferred until backing APIs exist:");
        Console.WriteLine("  scenario lifecycle/fork commands");
        Console.WriteLine("  archive compact/restore/rebuild-from-snapshot commands");
        Console.WriteLine();
        Console.WriteLine("Environment:");
        Console.WriteLine("  CANDOITALL_ECONOMY_CONNECTION");
        Console.WriteLine("  CANDOITALL_ECONOMY_NODE");
        Console.WriteLine("  CANDOITALL_ECONOMY_API_KEY");
        Console.WriteLine("  CANDOITALL_ECONOMY_BEARER_TOKEN");
        Console.WriteLine("  CANDOITALL_ECONOMY_LEDGER_ADMIN_KEY_ID");
        Console.WriteLine("  CANDOITALL_ECONOMY_LEDGER_ADMIN_PUBLIC_KEY");
        Console.WriteLine("  CANDOITALL_ECONOMY_LEDGER_ADMIN_PRIVATE_KEY");
        Console.WriteLine("  CANDOITALL_ECONOMY_LEDGER_ADMIN_ALGORITHM");
    }

    private sealed record CommandLine(IReadOnlyList<string> Command, IReadOnlyDictionary<string, string> Options)
    {
        public static CommandLine Parse(string[] args)
        {
            var command = new List<string>();
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < args.Length; index++)
            {
                string arg = args[index];
                if (!arg.StartsWith("--", StringComparison.Ordinal))
                {
                    command.Add(arg);
                    continue;
                }

                string name = arg[2..];
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    options[name] = args[++index];
                }
                else
                {
                    options[name] = "true";
                }
            }

            return new CommandLine(command, options);
        }

        public string Get(string name, string? fallback)
            => Options.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback ?? string.Empty;

        public string? GetOptional(string name)
            => Options.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : null;

        public bool GetBool(string name, bool fallback)
            => Options.TryGetValue(name, out string? value)
                ? bool.TryParse(value, out bool parsed) ? parsed : string.Equals(value, "1", StringComparison.Ordinal)
                : fallback;

        public bool? GetNullableBool(string name)
            => Options.TryGetValue(name, out string? value)
                ? bool.TryParse(value, out bool parsed) ? parsed : string.Equals(value, "1", StringComparison.Ordinal)
                : null;

        public decimal? GetNullableDecimal(string name)
            => Options.TryGetValue(name, out string? value)
               && decimal.TryParse(value, out decimal parsed)
                ? parsed
                : null;
    }

    private sealed class CliLedgerAdminSigner(
        string keyId,
        SignatureAlgorithmId algorithm,
        byte[] publicKeyMaterial,
        byte[] privateKeyMaterial) : ILedgerAdminSigner, IDisposable
    {
        public SignatureBundle Sign(HashHex payloadHash)
        {
            ISignatureProvider provider = SignatureProviderRegistry.CreateDefault().Get(algorithm);
            byte[] payload = Encoding.UTF8.GetBytes(payloadHash.Value);
            return new SignatureBundle(new[]
            {
                new SignatureEnvelope(
                    algorithm,
                    keyId,
                    Address.CalculatePublicKeyHash(publicKeyMaterial),
                    payloadHash,
                    provider.Sign(payload, privateKeyMaterial),
                    publicKeyMaterial)
            });
        }

        public void Dispose()
        {
        }
    }
}
