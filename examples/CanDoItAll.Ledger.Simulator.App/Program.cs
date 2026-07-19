using CanDoItAll.Economy.Simulator.App.Hosting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddEconomySimulatorAppHost();

WebApplication app = builder.Build();
app.SeedEconomySimulatorSampleProject();
app.UseEconomySimulatorAppHost();

await app.RunAsync();
