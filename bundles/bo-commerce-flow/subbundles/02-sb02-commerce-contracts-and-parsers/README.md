# SB02 Commerce Contracts And Parsers

## Status

- `Completed`

## Objective

Create the typed commerce model and parser/builder helpers for sale terms, payment, purchase, and receipt BO records while preserving manual BO creation.

## Covered Inputs

- `RN-02`, `RN-03`, `RN-07`
- `REQ-002`, `REQ-003`, `REQ-007`, `REQ-009`

## Prerequisites

- `SB01` closure gate passed.
- Ledger projection constraint in `architecture/01-target-solution.md` reviewed.

## Exact Source References

- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectPayload.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectRequests.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/AdvancedBusinessObjectRequests.cs`
- `repo://src/CanDoItAll.Economy.Components/Models/EconomyTokenOption.cs`
- `repo://tests/CanDoItAll.Ledger.Tests`
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\NFT\CommonNFT.cs`
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\NFT\PaymentNFT.cs`
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\NFT\ReceiptNFT.cs`
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\NFT\Dto\NFTSoldInfo.cs`

## Deliverables

- Commerce contracts for sale terms, payment records, purchase records, receipt records, status, and sale mode.
- Payload builder/parser helpers that attach commerce state to `BusinessObjectMediaPayload` or a compatible structured extension without breaking existing media objects.
- Typed view/fact support so payment/purchase/receipt records display meaningful fields.
- Validation helpers for price, seller, buyer, target object id, max clone count, and status transitions.

## Dependency Impact

- `SB03` and `SB04` consume these contracts.
- Weak contract proof invalidates buy UI, dispatch automation, dashboard lists, and final Alice/Bob proof.

## Validation Depth

- `Critical foundation`
- Requires Semantic Adequacy Gate proof and Production Behavior Artifact Matrix.

## Implementation Steps

1. Define commerce enums/records in the business-object layer.
2. Add parser/builder helpers for sale terms and commerce records.
3. Preserve existing generic family definitions and manual wizard behavior.
4. Add tests for valid and invalid sale terms, payment records, receipt records, and round-trip parsing.
5. Add source assertions that no VEFramework dependency is introduced.

## Scope Exceptions

- Dogecoin/BTC and VEFramework domain-specific NFT types are out of scope.
- Main-currency sale is required; token-priced purchase may be disabled until implemented safely.

## Do Not Do

- Do not change ledger consensus or projection rules.
- Do not store commerce state as opaque nested string wrappers.
- Do not copy VEFramework classes.

## Acceptance Checklist

- Sale terms include original/copy mode and optional max clones.
- Payment and receipt records link buyer, seller, original object, payment tx, dispatch tx, and delivered object.
- Invalid empty seller/buyer/object/amount states are rejected by helper tests.
- Manual payment/purchase/receipt BO creation remains possible.

## Proof Required

- `bundle://proof/SB02/manifest.md`
- `bundle://proof/SB02/semantic-invariants.md`
- Failing-first transcript for parser/validation gap.
- Passing targeted test transcript.
- Changed-file hashes and source assertions.
- Anti-stub audit for TODO/NotImplemented/hard-coded Alice/Bob paths.
- Production Behavior Artifact Matrix for sale terms and commerce record states.

## Browser Validation Logging

- N/A for browser closure; UI proof is in `SB03`.

## Progression Gate

- `SB03` and `SB04` may start only after commerce contracts have passing parser/validation tests and artifact-backed manifest.

## Closure

- Closure decision: `Passed`
- Manifest: `bundle://proof/SB02/manifest.md`
- Semantic invariants: `bundle://proof/SB02/semantic-invariants.md`
- Notes: Commerce records and sale terms are now typed metadata on `BusinessObjectMediaPayload`; payment/purchase/receipt manual family casting remains covered by regression tests.

## Suggested Agent Prompt

```text
Implement SB02 only. Add typed commerce records and parsers with tests; do not build the UI or dispatcher yet.
```
