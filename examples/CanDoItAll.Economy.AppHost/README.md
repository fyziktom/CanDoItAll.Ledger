# CanDoItAll Economy AppHost

The AppHost runs the BO demo, IPFS node, and economy ledger node together.

By default the ledger node uses EF Core InMemory storage:

```powershell
dotnet run --project examples\CanDoItAll.Economy.AppHost\CanDoItAll.Economy.AppHost.csproj --launch-profile http
```

When Docker is available and a PostgreSQL-backed ledger is wanted, opt in:

```powershell
dotnet run --project examples\CanDoItAll.Economy.AppHost\CanDoItAll.Economy.AppHost.csproj --launch-profile http -- --Economy:UsePostgreSql=true
```

The `http` and `https` profiles are available in Visual Studio. Start the AppHost project with either profile; the console prints the Aspire dashboard URL and the dashboard Resources page exposes the demo, ledger node, and IPFS node endpoints.

Both launch profiles set `ASPIRE_DCP_USE_DEVELOPER_CERTIFICATE=true` so Aspire's internal DCP control plane uses the trusted ASP.NET developer certificate instead of an ephemeral certificate.

The AppHost also builds and uses a small `dcp-proxy` helper. It forwards all commands to Aspire's packaged DCP executable, and on machines with AVG Web/Mail Shield TLS inspection it adds the local AVG root as the DCP kubeconfig trust anchor. This keeps `dotnet run` and Visual Studio startup working without changing the ledger, demo, or IPFS projects.
