# SB01 - Tags And Current State

## Status

- Completed

## Objective

Remove `Neblio` from production default tags and capture source assertions that preserve the current transaction signature/public-key baseline before deeper messaging work starts.

## Covered Inputs

- N001
- Current-state source scan for N002-N014 prerequisites

## Prerequisites

- Bundle prepared-stage validator passes or current repair is in progress.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/Components/VenftSsrEconomyApp.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/VenftBusinessObjectApp.razor`
- `repo://src/CanDoItAll.Economy.BusinessObjects/CryptoDriverAccountInputSigner.cs`
- `repo://src/CanDoItAll.Economy.Core/Signatures.cs`
- `repo://src/CanDoItAll.Economy.Ledger/LedgerExecutor.cs`

## Deliverables

- Production default tag strings do not include `Neblio`.
- Source assertion transcript names existing public-key material signer/ledger validation paths.
- Changed-file hash record under `proof/SB01/`.

## Dependency Impact

- SB02 depends on this baseline to avoid mixing tag cleanup with crypto changes.

## Validation Depth

- Source search and targeted tests only; no browser proof required unless this phase unexpectedly changes UI rendering.

## Implementation Steps

1. Remove `Neblio` from production default tag strings.
2. Run a source-only search to confirm no production default tag still contains `#Neblio`.
3. Capture source assertions for signer public key material and ledger key validation.
4. Record changed-file hashes and transcript paths.

## Scope Exceptions

- Historical bundle/proof text can continue to mention Neblio; do not rewrite archived evidence.

## Do Not Do

- Do not change crypto or messaging behavior in this subbundle.
- Do not remove VEFramework historical references that are not production defaults.

## Acceptance Checklist

- [x] Production default tags no longer contain `Neblio`.
- [x] Source assertions recorded for transaction public key material.
- [x] Source search transcript recorded.
- [x] `proof/SB01/manifest.md` and `proof/SB01/semantic-invariants.md` exist.

## Proof Required

- `proof/SB01/transcripts/source-search-tags.txt`
- `proof/SB01/transcripts/source-assertions.txt`
- `proof/SB01/changed-file-hashes.json`
- `proof/SB01/manifest.md`
- `proof/SB01/semantic-invariants.md`

## Browser Validation Logging

- N/A unless visible tag defaults are changed in a rendered flow during this subbundle.

## Progression Gate

- Downstream SB02 may start only after source search confirms production default `Neblio` removal and public-key signature baseline assertions are recorded.

## Suggested Agent Prompt

Execute SB01 only. Remove `Neblio` from production default tags, preserve historical evidence, capture source assertions for signed transaction public key material, and update proof artifacts before moving to SB02.
