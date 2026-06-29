# SB04 Transaction Assembly Signing Split

## Status

- `Closed`

## Objective

- Refactor or adapt transaction assembly so AccountApp assembles transactions in the browser, signs them with the SB01 browser signer, and broadcasts only signed transactions through the SB03 API client.

## Success Criteria

- Existing transaction-builder behavior is preserved for non-WASM/SSR paths.
- AccountApp can assemble supported transaction drafts with API-sourced spendable outputs.
- Signing occurs browser-side using the vault-unlocked key.
- Node verifies and accepts a browser-signed transaction without server-side account signing.
- Tests prove no raw private key is sent to node or SSR services.

## Covered Inputs

- RQ04, RQ05, RQ16, RQ23, RQ26, RQ27, RQ30.
- Architect note that signature and transaction assembly happen on the AccountApp side.

## Prerequisites

- SB01 progression gate passed.
- SB03 progression gate passed.

## Exact Source References

- repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs
- repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectAccount.cs
- repo://src/CanDoItAll.Economy.BusinessObjects/AccountKeys.cs
- repo://src/CanDoItAll.Economy.BusinessObjects/LedgerContracts.cs
- repo://src/CanDoItAll.Economy.Cryptography/SignaturePayloadBuilder.cs
- repo://src/CanDoItAll.Economy.Cryptography/DefaultTransactionCanonicalizer.cs
- repo://src/CanDoItAll.Economy.Sdk/EconomyNodeLedgerClient.cs
- repo://tests/CanDoItAll.Ledger.Tests

## Deliverables

- Browser-safe transaction assembly/signing orchestration.
- Refactor that separates unsigned assembly from signing, or a browser-backed signer implementation that satisfies existing builder contracts without exposing persisted keys.
- Tests for at least one representative account transaction through assemble, sign, verify, and broadcast.
- Regression tests for existing server/SSR builder behavior.
- No-private-key request proof.

## Dependency Impact

- SB05 needs transaction actions for shared UI adapters.
- SB06 needs this path to enable AccountApp transaction submission.
- SB07 final proof depends on browser-signed broadcast.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Audit `BusinessObjectTransactionBuilder` signing points and supported account operations.
2. Choose the smallest compatible split: unsigned draft plus signature application, or browser-backed signer.
3. Wire browser signer to payload hash/canonicalization without changing node verification semantics.
4. Use SB03 API client to load spendable outputs and broadcast signed transactions.
5. Add positive tests for assemble/sign/verify/broadcast.
6. Add negative tests that fail if server-side private key signing is required.
7. Update workbook rows, execution report, and proof manifest.

## Scope Exceptions

- Do not build full transaction UI in this subbundle.
- Limit end-to-end transaction types to a representative safe set unless the existing builder split makes broader coverage cheap and tested.

## Do Not Do

- Do not submit unsigned drafts to the node.
- Do not pass raw private key material to `EconomyNodeWorkspaceSession` or node services.
- Do not break existing server-side builder tests.

## Acceptance Checklist

- Browser service can assemble a transaction using API-sourced inputs.
- Browser signer applies valid signatures.
- Node/server verification accepts the browser-signed transaction.
- Network proof shows only signed transaction payload is broadcast.
- Existing transaction-builder tests still pass.

## Proof Required

- `proof/SB04/manifest.md` with command transcript links, changed-file hashes, and anti-stub audit.
- Unit/integration test output for assemble/sign/verify/broadcast.
- Browser or network trace proof that broadcast request has signed transaction data and no private key.
- Negative fixture showing server-side signing is not used for AccountApp path.
- Production Behavior Artifact Matrix for transaction draft/signed/broadcast lifecycle states if new models are introduced.

## Browser Validation Logging

- Target route: transaction signing harness or AccountApp transaction composer route.
- Viewports: desktop 1440x1000 and mobile 390x844 if UI is affected.
- Actions: unlock account, load spendable outputs, assemble transaction, sign, broadcast, inspect network payload.
- Screenshots/evidence: `proof/SB04/sign-broadcast-network.png`, `proof/SB04/signed-transaction-result.png`.
- Review questions: Can the proof distinguish browser signing from a server-signed fallback?

## Progression Gate

- SB05 and SB06 may proceed only after a browser/account-path transaction can be assembled, signed, verified, and broadcast without private-key leakage.

## Suggested Agent Prompt

```text
Implement SB04 only. Split or adapt transaction assembly/signing for the browser account path, preserve existing server behavior, prove signed broadcast through node API, and stop if the node still needs private key material.
```
