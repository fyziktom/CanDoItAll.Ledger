# CanDoItAll Ledger IPFS Host

This small executable adapts the published `CanDoItAll.IPFS.Engine` NuGet
package for the Ledger Aspire AppHost. It keeps Ledger orchestration independent
from a sibling IPFS source checkout while preserving the engine's HTTP API and
immutable `/ipfs/...` gateway.

Run it directly with:

```powershell
dotnet run --project examples\CanDoItAll.Ledger.IpfsHost\CanDoItAll.Ledger.IpfsHost.csproj
```

Set `IPFS_NODE_API_URL` and the `IpfsHttpHost` configuration section through
the normal .NET configuration providers when non-default host behavior is
needed.
