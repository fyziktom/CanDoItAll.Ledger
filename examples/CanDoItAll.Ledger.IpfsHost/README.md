# CanDoItAll Ledger IPFS Host

This small executable hosts the published `CanDoItAll.IPFS.Engine` NuGet
package for the Ledger Aspire AppHost. It exposes the engine's HTTP API and
immutable `/ipfs/...` gateway through a normal Aspire project resource.

Run it directly with:

```powershell
dotnet run --project examples\CanDoItAll.Ledger.IpfsHost\CanDoItAll.Ledger.IpfsHost.csproj
```

Set `IPFS_NODE_API_URL` and the `IpfsHttpHost` configuration section through
the normal .NET configuration providers when non-default host behavior is
needed.
