# Verification Summary

## Automated verification

- `dotnet build CanDoItAll.Ledger.slnx --no-restore --nologo -m:1`
  - Result: succeeded, 0 errors, 63 existing warnings.
  - Existing repository warnings remain for `ncalc` framework compatibility, AppHost `MessagePack` advisories, simulator package pruning, obsolete compatibility fixtures, and two xUnit analyzer suggestions.
- `dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj --no-build --no-restore`
  - Result: 483 passed, 0 failed, 0 skipped.
- Focused reference-validator suite
  - Result: 39 passed, 0 failed; includes cross-lineage `previous-version`, unrelated silent consumption, valid same-lineage pass-through, and malformed fail-closed coverage.
- Wider reference, advanced BO transaction, identity, and history suites
  - Result: 71 passed, 0 failed.
- Public Node/SDK identity-boundary suites
  - Result: passed, including an adversarial SDK transaction-summary response whose otherwise valid canonical BO ID belonged to a different outpoint; the client rejected it and reported both the claimed and expected IDs.
- Focused post-review rule execution run
  - Result: 15 passed, 0 failed.
- Focused `CanDoItAll.Economy.BusinessObjects` build
  - Result: succeeded, 0 warnings, 0 errors.
- `git diff --check`
  - Result: no whitespace errors; Git reports only the repository's expected LF-to-CRLF conversion notices.

## Deployment verification

- Rebuilt images after the public read-contract and final lineage/reference-validator repairs:
  - `candoitall-ledger-economy-node:dev`
  - `candoitall-ledger-accountapp:dev` for Alice, Bob, and Lab
- Force-recreated services without deleting PostgreSQL data:
  - `economy-node`
  - `accountapp-alice`
  - `accountapp-bob`
  - `accountapp-lab`
- HTTP verification:
  - Node liveness: HTTP 200, `status=ok`.
  - Node PostgreSQL readiness: HTTP 200, semantic ledger verification `valid_semantic_payload`, projection verification `valid`, and zero issues.
  - Alice `:5081`, Bob `:5082`, Lab `:5083`: HTTP 200 and serving the rebuilt WASM asset manifest.
  - Alice reverse-proxied node readiness: HTTP 200.
  - Transaction query (20 records / 12 BO outputs), get-by-txid, and account transactions (13 records / 10 BO outputs) returned `businessObject.objectId = txId:outputIndex` and omitted `lineageKey` from public summaries.
  - Flattened transaction detail returned canonical BO IDs.
  - All 7 current BO query records returned `objectId = currentOutPoint`.
  - Spendable output returned canonical `businessObject.objectId = outPoint`; required V1/V2 correlation appeared only under explicitly named `lineageKey`.

## Browser verification

- AccountApp WASM was exercised through the in-app browser against the live Docker Node using an isolated temporary AccountApp container and Alice demo backup; Alice/Bob/Lab vaults were not overwritten.
- Clone flow opened the searchable/tag-filtered thumbnail BO picker and selected source `a9a81477e338d83dffa65c60fbeabe743c73d31f6b731b2832a1284b7be71414:0`.
- Details contained no editable `Clone object ID` control and displayed `Assigned after confirmation`.
- The captured Review displayed the same canonical source ID, showed `New object: Assigned after confirmation`, retained typed `post` / `1.0` metadata, and reported a ready draft state.
- A final wording refinement renamed those facts to `Clone-source reference` and `External reference`; the six-test rendered-component contract verifies both labels, their values, and pending confirmation-assigned identity.
- Browser console verification returned zero warnings/errors. No transaction was submitted, so the live ledger was not mutated by visual proof.
- A fresh screenshot recapture from the final Docker image was attempted, but the required bundled in-app browser client failed before browser discovery with `TypeError: Cannot redefine property: process` at `browser-client.mjs:33`. No unsupported browser fallback was used; final-image runtime proof therefore consists of HTTP/readiness/live-contract checks plus the rendered-component suite, while the screenshots below document the visually inspected flow immediately before the wording-only refinement.
- Screenshots:
  - `screenshots/clone-details-canonical-id.png`
  - `screenshots/clone-review-canonical-id.png`
