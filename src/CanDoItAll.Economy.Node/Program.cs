using CanDoItAll.Economy.Node;

WebApplication app = await EconomyNodeHost.StartAsync(args).ConfigureAwait(false);
await app.WaitForShutdownAsync().ConfigureAwait(false);
