# SB01 Privacy Red Team Baseline

## Status

- `Completed`

## Objective

- Convert the raw tx privacy report into executable tests that fail on the current leak and pass only when public secure-message tx data is clean.

## Covered Inputs

- User raw tx shows subject in object id and public attachment IPFS metadata.

## Prerequisites

- Existing secure-message test fixtures build.

## Exact Source References

- `repo://tests/CanDoItAll.Ledger.Tests/SecureLedgerMessageTests.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs`

## Deliverables

- Privacy assertion helper that serializes public tx data and searches for forbidden values.
- Tests covering subject, body, file name, description, CID/URI, and attachment bytes.

## Dependency Impact

- Enables SB02 and SB03 to prove they fix real leaks.

## Validation Depth

- Unit and integration tests inspecting persisted transaction JSON.

## Implementation Steps

1. Add forbidden-value assertions around secure-message build output.
2. Add adapter-level assertion around object id and external ref.
3. Keep tests named clearly enough for bundle proof.

## Do Not Do

- Do not only assert component-rendered text.

## Acceptance Checklist

- [ ] Test catches subject-derived object id before fix.
- [ ] Test catches public attachment locator before fix.
- [ ] Tests remain stable after privacy fixes.

## Proof Required

- `dotnet test` command output with specific test names.

## Browser Validation Logging

- None required for SB01.

## Progression Gate

- SB02/SB03 may start after tests define the privacy target.

## Suggested Agent Prompt

- Add privacy red-team tests for secure-message raw transaction leakage and preserve test names in the execution report.
