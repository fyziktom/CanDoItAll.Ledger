# SB03 API Only Node Client Contracts

## Status

- `Complete`

## Objective

- Prove and adapt the node/SDK API contracts needed by AccountApp: spendable outputs, transaction list, transaction details, and signed transaction broadcast with browser-safe configuration.

## Success Criteria

- AccountApp can be configured with a node base URL and call only API endpoints.
- API contract tests cover account spendable outputs, account transactions, transaction details, and signed broadcast.
- Authentication/CORS/static-hosting decisions are explicit and browser-safe.
- No private key material is needed or accepted by AccountApp node endpoints.

## Covered Inputs

- RQ02, RQ03, RQ23, RQ24, RQ25, RQ26, RQ27, RQ28, RQ30.
- Architect note that API is only for transaction info/list for address and broadcast of signed transactions.

## Prerequisites

- Can run after preparation. SB04 depends on SB03 closure before signing/broadcast proof.
- `proof/SB03/manifest.md` exists and confirms AccountApp API-only contract proof.

## Completion Notes

- Closed on `2026-06-29`.
- Added browser-safe SDK registration through `AddEconomySdkWithBrowserNodeDriver`.
- Proved exact API request paths for spendable outputs, account transactions, ledger-scoped transaction details, top-level transaction details, and signed transaction broadcast.
- Proved the default browser-safe path sends no `Authorization` header and no `X-CDA-Economy-Key` header.
- Proved signed broadcast request body contains public signature material and excludes private key fields.
- Focused AccountApp API contract tests: `2 passed`.
- SDK regression with existing client tests: `4 passed`.
- Full ledger regression after SB03: `181 passed`.

## Exact Source References

- repo://src/CanDoItAll.Economy.Sdk/EconomyNodeLedgerClient.cs
- repo://src/CanDoItAll.Economy.Sdk/EconomyNodeContracts.cs
- repo://src/CanDoItAll.Economy.Sdk/ServiceCollectionExtensions.cs
- repo://src/CanDoItAll.Economy.Node/EconomyNodeApiEndpoints.cs
- repo://src/CanDoItAll.Economy.Node/EconomyNodePipeline.cs
- repo://src/CanDoItAll.Economy.Node/EconomyLedgerReadModelService.cs
- repo://tests/CanDoItAll.Ledger.Tests

## Deliverables

- Browser-safe SDK registration/config profile for AccountApp.
- API contract tests for required read and broadcast endpoints.
- CORS/static-hosting/auth decision implemented or documented with code-level enforcement.
- Negative tests or assertions showing node API does not receive private key material from AccountApp flows.
- Documentation of endpoint paths, request/response DTOs, and auth expectations.

## Dependency Impact

- SB04 requires API read/broadcast contracts to assemble and submit signed transactions.
- SB05 needs data-loading adapters based on these contracts.
- SB06 needs a stable node client profile.
- SB07 must prove these endpoints in the complete browser flow.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Audit existing SDK and node endpoints against AccountApp requirements.
2. Add missing endpoint/client support only if the current contracts do not cover a required flow.
3. Decide browser-safe auth and CORS/static-hosting configuration.
4. Add API contract tests and no-private-key request assertions.
5. Document configuration in the AccountApp plan/docs.
6. Update workbook rows, execution report, and proof manifest.

## Scope Exceptions

- Do not implement browser signing or transaction assembly here.
- Do not build the PWA shell here.

## Do Not Do

- Do not embed node operator API keys or bearer secrets into PWA assets.
- Do not add an endpoint that accepts raw private keys.
- Do not let AccountApp use in-process node services instead of HTTP API.

## Acceptance Checklist

- Required read endpoints are exercised by tests.
- Signed broadcast endpoint is exercised by a contract test using a signed transaction fixture.
- Browser configuration has no private operator secret in shipped static files.
- CORS/auth behavior is documented and tested.
- SDK registration is usable from WASM DI.

## Proof Required

- `proof/SB03/manifest.md` with command transcript links and endpoint matrix.
- API contract test command output.
- Request/response fixture artifacts for read/list/detail/broadcast.
- Negative proof that raw private key fields are absent from AccountApp API requests.
- Configuration proof for CORS/auth/static hosting decision.

## Browser Validation Logging

- Target route: API client smoke route or AccountApp data-loading route if available.
- Viewports: desktop 1440x1000 is sufficient unless UI is introduced.
- Actions: call health/bootstrap if needed, load spendable outputs, load transactions, load transaction details, submit signed transaction fixture.
- Screenshots/evidence: `proof/SB03/api-contract-network.png` or Playwright network trace.
- Review questions: Are all node calls HTTP API calls, and is no private key present in request bodies, headers, logs, or query strings?

## Progression Gate

- Passed. SB04 may proceed with browser-side transaction assembly/signing against the proven read/broadcast API contract.

## Suggested Agent Prompt

```text
Implement SB03 only. Prove the AccountApp API contract and browser-safe configuration, add only missing client/endpoint support, and stop if auth/CORS requires unsafe browser secrets.
```
