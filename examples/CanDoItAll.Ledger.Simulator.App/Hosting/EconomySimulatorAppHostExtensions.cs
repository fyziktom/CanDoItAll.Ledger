using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Economy.Simulator;
using CanDoItAll.Economy.Simulator.Components.Samples;
using CanDoItAll.Economy.Simulator.Persistence.EFCore;
using Microsoft.AspNetCore.DataProtection;
using SimulatorRootComponent = CanDoItAll.Economy.Simulator.App.Components.App;

namespace CanDoItAll.Economy.Simulator.App.Hosting;

internal static class EconomySimulatorAppHostExtensions
{
    private const string DataProtectionApplicationName = "CanDoItAll.Ledger.Simulator.App";
    private const string DataProtectionKeyRingPathKey = "DataProtection:KeyRingPath";
    private const string DefaultDataProtectionKeyRingPath = "DataProtectionKeys";

    public static WebApplicationBuilder AddEconomySimulatorAppHost(this WebApplicationBuilder builder)
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
        builder.Services.AddEconomySimulator();
        builder.Services.AddEconomySimulatorProjectionPersistence(builder.Configuration);

        return builder;
    }

    public static WebApplication SeedEconomySimulatorSampleProject(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        using IServiceScope scope = app.Services.CreateScope();
        ISimulationProjectWorkspaceService workspace = scope.ServiceProvider
            .GetRequiredService<ISimulationProjectWorkspaceService>();
        EconomySimulatorProjectionPersistenceSelection persistenceSelection = scope.ServiceProvider
            .GetRequiredService<EconomySimulatorProjectionPersistenceSelection>();
        workspace.UpsertProject(
            EconomySimulatorSampleData.CreateProjectState(persistenceSelection.StorageModeName));

        return app;
    }

    public static WebApplication UseEconomySimulatorAppHost(this WebApplication app)
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
        app.MapEconomySimulatorApi();

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
