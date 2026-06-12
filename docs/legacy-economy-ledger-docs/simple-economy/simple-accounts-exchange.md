# Simple Accounts Exchange

The simple accounts exchange is a non-ledger sandbox for account balances, reservations, deterministic batch clearing, and scenario playback.

## Projects

- `src/CanDoItAll.Economy.Accounts` defines accounts, assets, balances, transfers, reservations, operation/journal contracts, issuance policy contracts, and `IAccountHandler`.
- `src/CanDoItAll.Economy.Markets` defines markets, limit orders, trades, clearing cycles, price history, and `IMarketHandler`.
- `src/CanDoItAll.Economy.Investments` defines contract-only claim, obligation, cash-flow schedule, position, draft, lifecycle, validation, loan-term, fund-term, and revenue-share DTOs for future investment bundles.
- `src/CanDoItAll.Economy.Persistence.EFCore` stores the simple economy model with EF Core InMemory support.
- `src/CanDoItAll.Economy.Persistence.PostgreSql` exposes PostgreSQL registration for the same model.
- `src/CanDoItAll.Economy.Sandbox` provides reusable market and thought-economy scenarios with JSON import/export.
- `examples/CanDoItAll.Economy.MarketSandbox.Demo` hosts the large-screen Blazor sandbox.

## Demo

Run the demo with:

```powershell
dotnet run --project examples/CanDoItAll.Economy.MarketSandbox.Demo/CanDoItAll.Economy.MarketSandbox.Demo.csproj --urls http://localhost:5198
```

The host uses EF Core InMemory by default. Production-style PostgreSQL wiring is available through `AddSimpleEconomyPostgreSqlPersistence(connectionString)`.

## Foundation Contracts

All balance-changing account operations write an `AccountOperation` envelope and `JournalLine` rows. Use `AccountCommandContext`, `IdempotencyKey`, scoped `IdempotencyScope`, and `CanonicalAccountCommandFingerprintCalculator` command fingerprints for retryable commands. Same-scope/same-key/same-fingerprint requests replay; same-scope/same-key/different-fingerprint requests are rejected and audited without mutating value.

Market settlement stores operation IDs, a settlement group, and a settlement batch ID on trades so quote and base asset movements can be traced back to one business event and its journal lines. Stale clearing cycles require an explicit `StaleClearingCyclePolicy`.

Issuance is controlled by `IAssetIssuancePolicy`. Demo/in-memory hosts are permissive; PostgreSQL hosts are restrictive by default. For future capital assets, configure `AssetSupplyMode.AuthorizedIssuer`, authorized issuer account IDs, and `MaxSupply`. EF persistence tracks issued supply, exposes `GetAssetSupplyProjectionAsync`, and records rejected over-cap or unauthorized issue attempts as rejected operations when command context is supplied.

Reservations track kind, owner operation, correlation, optional expiration, committed amount, released amount, and remaining amount. `ListReservationsAsync` supports contract-aware reservation reads. Explicit expiry processing releases expired holds through journal-backed operations. Collateral and fund-subscription guards prevent unscoped investment-style holds from silently becoming global commitments.

`IAccountJournalVerifier` replays journal lines and checks operation shape, reservation invariants, and balance projection consistency. V3 results can return reconstructed balance projections for audit UIs and corruption proof.

## Validation

Focused coverage lives in `tests/CanDoItAll.Economy.Tests/SimpleEconomyTests.cs`, `SimpleEconomyBoundaryTests.cs`, `SimpleEconomyFoundationReadinessTests.cs`, `SimpleEconomyFoundationHardeningV2Tests.cs`, and `SimpleEconomyFoundationHardeningV3Tests.cs`. The suite verifies reservation concurrency, precision rules, deterministic clearing, settlement idempotency, command fingerprint conflicts, rejected conflict audit, operation/journal reconstruction, journal verification corruption detection, lifecycle rejection, issuance authority and supply caps, reservation expiry, metadata preservation, package boundaries, SQLite relational fallback proof, Docker-backed PostgreSQL proof, scenario JSON, thought-economy assertions, policy evaluation IDs, and PostgreSQL DI/schema registration without ledger dependencies.
