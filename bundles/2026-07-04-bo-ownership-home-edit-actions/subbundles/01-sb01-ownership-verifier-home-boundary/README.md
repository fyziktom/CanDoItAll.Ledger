# SB01 Ownership Verifier Home Boundary

## Status

- `Completed`

## Objective

- Make ownership verification scanning a Home-only wallet-owner tool while preserving owner-facing BO ownership QR presentation.

## Success Criteria

- BO details Actions does not render a `Verify ownership` scanner action.
- BO details no longer opens `BusinessObjectVerificationDialogContent`.
- Home Security still opens `MessageSecurityPanel` with ownership payload scan/paste verification.
- BO ownership QR presentation remains available for the owner to show.

## Covered Inputs

- N001: "tool for verify of ownership must be only in Home page as tool for owner of wallet."
- R001: remove BO details scanner action.
- R002: preserve Home ownership verification.
- R003: preserve owner-facing QR presentation.

## Prerequisites

- Root bundle readiness gate passed.
- Source references in this README still exist.
- No production code edits from SB02 have started.

## Exact Source References

- repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor
- repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor
- repo://src/CanDoItAll.Economy.Components/Components/Account/MessageSecurityPanel.razor
- repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs
- repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceLocalServicesTests.cs

## Deliverables

- Remove BO details verifier scanner action and its direct dialog-opening method if no longer used.
- Keep Home Security wiring to `MessageSecurityPanel`.
- Add or update a component test proving scanner placement is Home-only.
- Capture source assertions and browser proof.

## Dependency Impact

- SB02 adds a new Edit action to the same BO details Actions tab. It must depend on SB01 so it does not accidentally preserve or reintroduce the verifier scanner.
- SB03 browser closure depends on SB01 proof before validating final integrated navigation.

## Validation Depth

- Critical UI foundation with Semantic Adequacy Gate, source assertion, targeted component test, Playwright proof, and anti-stub audit.

## Implementation Steps

1. Remove the `Verify ownership` scanner action from `BusinessObjectLedgerDetailsDialogContent`.
2. Remove or stop using `OpenVerificationDialogAsync` from BO details.
3. Verify `MessageSecurityPanel` remains reachable from Home Security.
4. Add component-level assertions for Home scanner availability and BO details scanner absence.
5. Record proof and update the execution report.

## Scope Exceptions

- None. The raw note requires Home-only scanner placement without exception.

## Do Not Do

- Do not remove `MessageSecurityPanel` ownership verification.
- Do not remove BO ownership QR presentation used by the owner to show ownership.
- Do not add a scanner action to Gallery, BO details, or any BO action rail.

## Acceptance Checklist

- BO details markup has no `Verify ownership` action.
- BO details source has no direct ownership verifier dialog launcher.
- Home Security still opens `MessageSecurityPanel`.
- Ownership QR button remains available for owner presentation.
- Targeted tests pass.

## Proof Required

- Failing-first or red-team transcript proving pre-change BO details contains the verifier scanner action.
- Passing transcript for targeted tests after removal.
- Source assertion transcript proving `BusinessObjectVerificationDialogContent` is not opened from BO details and Home still opens `MessageSecurityPanel`.
- Anti-stub audit transcript covering production TODO, NotImplemented, fixture-specific branching, and template-only output in changed files.
- `proof/SB01/manifest.md` with changed-file hashes and portable proof references.
- `proof/SB01/semantic-invariants.md` covering Home-only scanner placement.
- Playwright proof for Home Security and BO details Actions.
- Final proof manifest: `bundle://proof/SB01/manifest.md`
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`
- Docker browser closure: `bundle://proof/SB03/transcripts/playwright-docker-accountapp.txt`

## Browser Validation Logging

- Route: AccountApp Home `/` with full app shell and Gallery BO details.
- Viewport: large desktop first; narrower follow-up only if action layout changes affect wrapping.
- Actions: open Home Security, assert ownership verifier content; open BO details Actions, assert scanner action absent and QR presentation present.
- Screenshots: `bundle://proof/SB01/browser/home-security-ownership.png` and `bundle://proof/SB01/browser/bo-actions-no-verifier.png`.
- Review questions: text readable, dialog not clipped, no lateral overflow, actions still understandable, scanner only appears on Home security surface.

## Progression Gate

- Completed: tests/source proof show Home scanner exists and BO details scanner is absent, and SB03 Docker browser proof verified the final integrated app.

## Suggested Agent Prompt

```text
Implement SB01 only. Remove the BO details ownership verifier scanner while preserving Home MessageSecurityPanel ownership verification and owner QR presentation. Add semantic tests and proof, update the execution report, and stop if any BO details scanner entry point remains.
```
