# CanDoItAll.Ledger.AppHost

.NET Aspire development host for the Ledger node, component demo, and local
IPFS engine.

The IPFS resource is a small Ledger composition host that consumes the
published `CanDoItAll.IPFS.Engine` package and exposes it as a normal Aspire
project resource.

## Run

The default profile uses in-memory Ledger storage:

```powershell
dotnet run `
  --project examples\CanDoItAll.Ledger.AppHost\CanDoItAll.Ledger.AppHost.csproj `
  --launch-profile http
```

To compose PostgreSQL through Aspire:

```powershell
dotnet run `
  --project examples\CanDoItAll.Ledger.AppHost\CanDoItAll.Ledger.AppHost.csproj `
  --launch-profile http `
  -- --Ledger:UsePostgreSql=true
```

PostgreSQL mode requires the secret Aspire parameter
`ledger-postgres-password`. Supply it through normal Aspire parameter
configuration or .NET user secrets.

The Aspire dashboard exposes the demo, Ledger node, and IPFS endpoints. The
IPFS engine serves both its API and immutable `/ipfs/...` gateway on port
`5001`.

## Local DCP Certificate Handling

The `http` and `https` launch profiles set
`ASPIRE_DCP_USE_DEVELOPER_CERTIFICATE=true`, allowing Aspire's internal DCP
control plane to use the trusted ASP.NET developer certificate.

The AppHost also uses the local `dcp-proxy` helper. It forwards commands to
Aspire's packaged DCP executable and can add the local AVG root as a kubeconfig
trust anchor on machines where AVG Web/Mail Shield performs TLS inspection.
This affects only local Aspire control-plane startup.
