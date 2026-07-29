using CanDoItAll.Ledger.Node;

WebApplication app = await LedgerNodeHost.StartAsync(args).ConfigureAwait(false);
await app.WaitForShutdownAsync().ConfigureAwait(false);
