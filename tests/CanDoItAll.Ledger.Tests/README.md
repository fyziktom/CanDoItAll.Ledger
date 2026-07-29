# CanDoItAll.Ledger.Tests

Integration and unit tests for transaction validation, execution, cryptography,
storage, persistence, SDK, node hosting, components, demos, Simulator behavior,
and repository boundaries.

Run from the repository root:

```powershell
dotnet test tests\CanDoItAll.Ledger.Tests\CanDoItAll.Ledger.Tests.csproj `
  --configuration Release
```

Tests must keep confirmed Ledger behavior distinct from Simulator projections
and preserve transaction, serialization, persistence, and identity
compatibility contracts.
