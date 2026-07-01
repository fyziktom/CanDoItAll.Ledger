# SB01 VEFramework Multimint Architecture Mapping

## Status

- Completed

## Objective

Extract the reusable UX/process semantics from VEFramework multiminting and map them to Ledger-native AccountApp boundaries.

## Covered Inputs

- User request.
- `VEBlazor/Components/Minting/MultimintForm.razor`.
- Current Ledger wizard and adapter host.

## Prerequisites

- Access to `C:\repositories\VirtualEconomyFramework`.
- Current Ledger codebase available.

## Exact Source References

- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Components\Minting\MultimintForm.razor`
- `C:\repositories\CanDoItAll.Ledger\src\CanDoItAll.Economy.Components\Components\BusinessObjects\BusinessObjectCreationWizard.razor`
- `C:\repositories\CanDoItAll.Ledger\src\CanDoItAll.Economy.Components\Components\Account\EconomyAccountWorkspaceAdapterHost.razor`

## Deliverables

- Current-state analysis in `analysis/01-current-state.md`.
- Target architecture in `architecture/01-target-solution.md`.

## Dependency Impact

- Unlocks SB02 by defining batch draft semantics and recipient modes.

## Validation Depth

- Source inspection and traceability are sufficient; no code proof required in this subbundle.

## Implementation Steps

1. Inspect VEFramework multimint.
2. Identify reusable semantics and implementation anti-patterns.
3. Map to Ledger draft/adapters.

## Do Not Do

- Do not copy `AppData.Account.MintMultiNFTLargeAmount` style coupling.
- Do not introduce Neblio-specific address validation.

## Acceptance Checklist

- VEFramework mapping is present.
- Ledger integration point is identified.
- Risks and scope exceptions are explicit.

## Proof Required

- Bundle validator pass after preparation.
- Mapping captured in `bundle://analysis/01-current-state.md` and `bundle://architecture/01-target-solution.md`.

## Browser Validation Logging

- Not required for this subbundle.

## Progression Gate

- SB02 may start after mapping and target architecture are written.

## Suggested Agent Prompt

Use the mapped semantics to implement batch drafts and recipient UI without bypassing existing Ledger signing/submission paths.
