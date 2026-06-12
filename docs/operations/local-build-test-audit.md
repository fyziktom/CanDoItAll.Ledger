# Local Build, Test, And Audit Runbook

Run these commands from the Ledger repository root.

## Standard Lane

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\audit-ledger-boundaries.ps1
```

## Direct Lane

```powershell
dotnet build CanDoItAll.Ledger.slnx -c Release -m:1
dotnet test tests\CanDoItAll.Ledger.Tests\CanDoItAll.Ledger.Tests.csproj -c Release
.\scripts\audit-ledger-repo-does-not-contain-basic-simulator.ps1 -RunNegativeFixture
```

## Two-Repo Boundary Lane

```powershell
.\scripts\audit-economy-ledger-split.ps1 -EconomyRoot C:\repositories\CanDoItAll.Economy -LedgerRoot C:\repositories\CanDoItAll.Ledger
```

The two-repo lane writes boundary reports under the selected artifacts directory. It is the quickest check for accidental ownership drift between the simulator repository and the ledger/node repository.
