# Local Build, Test, And Audit Runbook

Run these commands from the Ledger repository root.

## Standard Lane

```powershell
.\tools\validation\Test-LedgerIdentity.ps1 -RunNegativeFixture
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\audit-ledger-boundaries.ps1
.\tools\validation\Test-Docker.ps1 -EnvFile .env.example
```

## Direct Lane

```powershell
dotnet build CanDoItAll.Ledger.slnx -c Release -m:1
dotnet test tests\CanDoItAll.Ledger.Tests\CanDoItAll.Ledger.Tests.csproj -c Release
.\scripts\audit-ledger-repo-does-not-contain-basic-simulator.ps1 -RunNegativeFixture
```

`Test-LedgerIdentity.ps1` fails on an old project path, namespace, type,
configuration prefix, Aspire metadata name, infrastructure identity, or
`Ledger.Ledger` collision. Its negative fixture proves the rules reject an
injected `CanDoItAll.Economy.Node` import and `Economy__` configuration key.

For Dockerfile build checks, run:

```powershell
.\tools\validation\Test-Docker.ps1 -EnvFile .env.example -RunBuildChecks
```

## Two-Repo Boundary Lane

```powershell
.\scripts\audit-economy-ledger-split.ps1 -EconomyRoot C:\repositories\CanDoItAll.Economy -LedgerRoot C:\repositories\CanDoItAll.Ledger
```

The two-repo lane writes boundary reports under the selected artifacts directory. It is the quickest check for accidental ownership drift between the simulator repository and the ledger/node repository.

The Economy name in this command is intentional: it is the real sibling
repository boundary, not an active Ledger namespace.
