# SB01 Refresh Loop And Account Cache

## Status

- Status: Completed

## Objective

Add a conservative account refresh loop that probes newest transaction summaries before full workspace reload, and add cache provider abstractions for immutable transaction and BO/detail data.

## Covered Inputs

- RN-01
- RN-02

## Prerequisites

- Prepared bundle gate must pass.
- Source references in `architecture/02-source-reference-map.md` must be current.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspaceAdapterHost.razor`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs`
- `repo://examples/CanDoItAll.Economy.AccountApp/Program.cs`
- `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppSession.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs`

## Deliverables

- Refresh snapshot/fingerprint contract.
- Cache provider abstraction plus in-memory provider.
- WASM localStorage provider registration.
- Transaction detail cache used by adapters.
- Background loop with conservative interval and cancellation/disposal.
- Focused tests for changed/unchanged refresh behavior and cache hit/miss.

## Dependency Impact

- SB02 and SB03 depend on SB01 because UI proof is meaningful only if account state refreshes without page reload.

## Validation Depth

- Critical foundation.
- Requires Semantic Adequacy Gate proof, artifact-backed manifest, changed-file hashes, source assertions, anti-stub audit, and tests.

## Implementation Steps

1. Add adapter method for refresh snapshots from top 10 transactions without data.
2. Add cache interface and providers.
3. Wire localStorage provider in AccountApp and in-memory provider in shared registrations.
4. Update adapter detail reads to use cache.
5. Add timer loop in adapter host with fingerprint comparison and cancellation.
6. Add tests.

## Do Not Do

- Do not poll every second.
- Do not store private keys or transaction signing material in the new cache.
- Do not force SSR paths to depend on browser localStorage.

## Acceptance Checklist

- [x] Unchanged newest-10 fingerprint skips full workspace reload.
- [x] Changed fingerprint reloads workspace once.
- [x] Transaction detail cache avoids repeat detail reads.
- [x] Timer stops on component disposal.

## Proof Required

- `bundle://proof/SB01/manifest.md`
- `bundle://proof/SB01/semantic-invariants.md`
- Transcript for failing or missing coverage before implementation.
- Transcript for passing focused tests after implementation.

## Browser Validation Logging

- Supporting browser proof is deferred to SB03.
- SB01 closure must still record whether browser proof is pending downstream.

## Progression Gate

- SB01 may close only after focused tests and source assertions prove probe, reload, cache, and cancellation behavior.

## Suggested Agent Prompt

Implement SB01 only. Keep refresh state in the component lifetime owner and storage in provider abstractions. Update proof before moving to SB02.
