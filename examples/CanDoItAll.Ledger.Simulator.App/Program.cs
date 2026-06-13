using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Economy.Simulator;
using CanDoItAll.Economy.Simulator.App.Components;
using CanDoItAll.Economy.Simulator.Components.Samples;
using CanDoItAll.Economy.Simulator.Persistence.EFCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddCanDoItAllCharts();
builder.Services.AddEconomySimulator();

string storageMode = builder.Configuration["EconomySimulator:StorageMode"] ?? "InMemory";
if (storageMode.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
{
    string connectionString = builder.Configuration["EconomySimulator:PostgreSql:ConnectionString"]
        ?? "Host=localhost;Port=5432;Username=cdaeconomy;Password=cdaeconomy;Database=candoitall_economy_simulator";
    builder.Services.AddEconomySimulatorPostgreSqlProjectionPersistence(connectionString);
}
else
{
    string databaseName = builder.Configuration["EconomySimulator:InMemoryDatabaseName"]
        ?? "candoitall-economy-simulator-app";
    builder.Services.AddEconomySimulatorInMemoryProjectionPersistence($"{databaseName}-projections");
}

WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    ISimulationProjectWorkspaceService workspace = scope.ServiceProvider.GetRequiredService<ISimulationProjectWorkspaceService>();
    workspace.UpsertProject(EconomySimulatorSampleData.CreateProjectState(storageMode));
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapEconomySimulatorApi();

await app.RunAsync();
