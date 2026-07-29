using CanDoItAll.Ledger.Simulator.App.Hosting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddLedgerSimulatorAppHost();

WebApplication app = builder.Build();
app.SeedLedgerSimulatorSampleProject();
app.UseLedgerSimulatorAppHost();

await app.RunAsync();
