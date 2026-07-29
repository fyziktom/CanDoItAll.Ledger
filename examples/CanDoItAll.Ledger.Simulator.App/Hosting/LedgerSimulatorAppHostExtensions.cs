using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Ledger.Simulator;
using CanDoItAll.Ledger.Simulator.Components.Samples;
using CanDoItAll.Ledger.Simulator.Persistence.EFCore;
using Microsoft.AspNetCore.DataProtection;
using SimulatorRootComponent = CanDoItAll.Ledger.Simulator.App.Components.App;

namespace CanDoItAll.Ledger.Simulator.App.Hosting;

internal static class LedgerSimulatorAppHostExtensions
{
    private const string DataProtectionApplicationName = "CanDoItAll.Ledger.Simulator.App";
    private const string DataProtectionKeyRingPathKey = "DataProtection:KeyRingPath";
    private const string DefaultDataProtectionKeyRingPath = "artifacts/dataprotection/simulator-app";

    public static WebApplicationBuilder AddLedgerSimulatorAppHost(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WebHost.UseStaticWebAssets();
        ConfigureLogging(builder.Logging);
        AddDataProtection(builder);

        builder.Services
            .AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddHealthChecks();
        builder.Services.AddCanDoItAllBaseLib();
        builder.Services.AddCanDoItAllCharts();
        builder.Services.AddLedgerSimulator();
        builder.Services.AddLedgerSimulatorProjectionPersistence(builder.Configuration);

        return builder;
    }

    public static WebApplication SeedLedgerSimulatorSampleProject(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        using IServiceScope scope = app.Services.CreateScope();
        ISimulationProjectWorkspaceService workspace = scope.ServiceProvider
            .GetRequiredService<ISimulationProjectWorkspaceService>();
        LedgerSimulatorProjectionPersistenceSelection persistenceSelection = scope.ServiceProvider
            .GetRequiredService<LedgerSimulatorProjectionPersistenceSelection>();
        workspace.UpsertProject(
            LedgerSimulatorSampleData.CreateProjectState(persistenceSelection.StorageModeName));

        return app;
    }

    public static WebApplication UseLedgerSimulatorAppHost(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }

        app.UseStaticFiles();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapHealthChecks("/health");
        app.MapRazorComponents<SimulatorRootComponent>()
            .AddInteractiveServerRenderMode();
        app.MapLedgerSimulatorApi();

        return app;
    }

    private static void ConfigureLogging(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddDebug();
    }

    private static void AddDataProtection(WebApplicationBuilder builder)
    {
        DirectoryInfo keyRingDirectory = ResolveDataProtectionKeyRingDirectory(builder);
        IDataProtectionBuilder dataProtection = builder.Services
            .AddDataProtection()
            .SetApplicationName(DataProtectionApplicationName)
            .PersistKeysToFileSystem(keyRingDirectory);

        if (OperatingSystem.IsWindows())
        {
            dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
        }
    }

    private static DirectoryInfo ResolveDataProtectionKeyRingDirectory(WebApplicationBuilder builder)
    {
        string configuredPath = builder.Configuration[DataProtectionKeyRingPathKey]
            ?? DefaultDataProtectionKeyRingPath;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                $"Configuration value '{DataProtectionKeyRingPathKey}' cannot be empty.");
        }

        string fullPath = Path.IsPathFullyQualified(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(configuredPath, builder.Environment.ContentRootPath);

        try
        {
            return Directory.CreateDirectory(fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"The simulator cannot create its Data Protection key ring at '{fullPath}'. "
                + $"Configure a writable path with '{DataProtectionKeyRingPathKey}'.",
                exception);
        }
    }
}
