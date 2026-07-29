# CanDoItAll Ledger AppHost

The AppHost runs the Ledger demo, IPFS node, and ledger node together.

This project uses the Ledger project and configuration identities. It must not
host the separate Economy simulator lane.

By default the ledger node uses EF Core InMemory storage:

```powershell
dotnet run --project examples\CanDoItAll.Ledger.AppHost\CanDoItAll.Ledger.AppHost.csproj --launch-profile http
```

When Docker is available and a PostgreSQL-backed ledger is wanted, opt in:

```powershell
dotnet run --project examples\CanDoItAll.Ledger.AppHost\CanDoItAll.Ledger.AppHost.csproj --launch-profile http -- --Ledger:UsePostgreSql=true
```

The PostgreSQL mode requires the secret Aspire parameter
`ledger-postgres-password`; provide it through the normal Aspire parameter
configuration or user-secrets mechanism.

The `http` and `https` profiles are available in Visual Studio. Start the AppHost project with either profile; the console prints the Aspire dashboard URL and the dashboard Resources page exposes the demo, ledger node, and IPFS node endpoints.

Both launch profiles set `ASPIRE_DCP_USE_DEVELOPER_CERTIFICATE=true` so Aspire's internal DCP control plane uses the trusted ASP.NET developer certificate instead of an ephemeral certificate.

The AppHost also builds and uses a small `dcp-proxy` helper. It forwards all commands to Aspire's packaged DCP executable, and on machines with AVG Web/Mail Shield TLS inspection it adds the local AVG root as the DCP kubeconfig trust anchor. This keeps `dotnet run` and Visual Studio startup working without changing the ledger, demo, or IPFS projects.
