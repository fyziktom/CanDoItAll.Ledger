# CanDoItAll.Ledger.AccountApp

Blazor WebAssembly account client for managing a local encrypted vault and
interacting with a Ledger node.

The app consumes published CanDoItAll Components packages and is also built as
the Docker AccountApp image. Node URLs, vault identifiers, and optional IPFS
endpoints remain configurable through environment settings.

Build from the repository root:

```powershell
dotnet build examples\CanDoItAll.Ledger.AccountApp\CanDoItAll.Ledger.AccountApp.csproj
```
