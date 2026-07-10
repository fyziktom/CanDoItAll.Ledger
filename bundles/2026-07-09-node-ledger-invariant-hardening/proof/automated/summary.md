# Automated proof summary

## Final full suite

Command:

```powershell
dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj -c Release --no-restore --logger "trx;LogFileName=ledger-hardening-full-final.trx" --results-directory bundles/2026-07-09-node-ledger-invariant-hardening/proof/automated --logger "console;verbosity=minimal"
```

Result: 334 passed, 0 failed, 0 skipped; duration 1m13s.

Machine-readable result: `ledger-hardening-full-final.trx`.

The run includes PostgreSQL double-spend and duplicate-token concurrency, SQLite relational constraints, golden vectors, production security profile, BO builders/transitions, Node contracts, AccountApp contracts, and the new hardening regressions.

## Focused hardening

`LedgerHardeningRegressionTests`: 19 passed, 0 failed. Coverage includes:

- preflight before hashing;
- empty and zero-token outputs;
- fail-closed default token policy;
- main-currency destruction rejection, fail-closed account issuance, authorized treasury issuance, and conservation of normal tokens/permission supply;
- deterministic decimal overflow rejection;
- BO owner/operation, carrier, and external data hash rules;
- direct reference/signature validators;
- concurrent in-memory double-spend and duplicate-token registration.

`LedgerPersistenceConstraintTests` proves direct SQLite inserts cannot bypass mixed-asset/nonempty UTXO checks. `EconomyNodeBoundaryConfigurationTests` proves the configured request limit reaches Kestrel.

## Dependency audit

Command:

```powershell
dotnet list tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj package --vulnerable --include-transitive --no-restore
```

Result: the project has no vulnerable packages for the current configured sources. The baseline SQLite native advisory is no longer reported. Remaining warnings are NU1701 for `ncalc` 1.3.8 and NU1510 for an unnecessary simulator abstraction reference.
