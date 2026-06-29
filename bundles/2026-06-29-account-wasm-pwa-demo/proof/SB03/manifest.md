# SB03 Proof Manifest - API-Only Node Client Contracts

## Status

- Closure result: `Passed`
- Date: `2026-06-29`
- Scope closed: AccountApp API endpoint matrix, browser-safe SDK registration, request-shape tests for read/list/detail/broadcast, no-private-key broadcast assertions, SDK regression proof, endpoint fixtures.
- Downstream gate: SB04 may consume the API contract for browser-side assembly/signing; SB05/SB06 may consume browser-safe SDK registration.

## Implementation Summary

- Added `AddEconomySdkWithBrowserNodeDriver` to the SDK service collection extensions.
- Added `EconomyBrowserNodeClientOptions` with `ApiBasePath` only; no static API key or bearer-token options are exposed for the browser-safe path.
- Added `EconomyNodeAccountAppApiContractTests` covering spendable outputs, account transaction list, ledger-scoped transaction detail, top-level transaction detail, and signed broadcast request bodies.
- Added endpoint matrix and request/response fixture artifacts under `proof/SB03/`.

## Source Assertions

- Browser-safe SDK registration configures only node base URI and API base path.
- Default browser-safe HTTP requests contain no `Authorization` header and no `X-CDA-Economy-Key` header.
- Signed broadcast body contains signature bundle/public key material and excludes private key fields.
- Existing `EconomyNodeLedgerClient` methods already cover required AccountApp reads and signed transaction broadcast.

## Endpoint Matrix

See `endpoint-matrix.md`.

| Need | Method | Path | Proof |
| --- | --- | --- | --- |
| Spendable outputs | `GET` | `/api/v1/ledger/{ledgerId}/accounts/{address}/spendable-outputs` | `fixtures/spendable-outputs-request.json` |
| Account transaction list | `GET` | `/api/v1/ledger/{ledgerId}/accounts/{address}/transactions` | `fixtures/account-transactions-request.json` |
| Ledger-scoped transaction detail | `GET` | `/api/v1/ledger/{ledgerId}/transactions/{txId}` | `fixtures/transaction-detail-request.json` |
| Top-level transaction detail | `GET` | `/api/v1/transactions/{txId}` | `fixtures/top-level-transaction-detail-request.json` |
| Signed broadcast | `POST` | `/api/v1/ledger/transactions` | `fixtures/signed-broadcast-request.json`, `fixtures/signed-broadcast-response.json` |

## Production Behavior Artifact Matrix

| Artifact | Behavior Proven | Evidence |
| --- | --- | --- |
| `EconomyNodeLedgerClient.GetSpendableOutputsAsync` | Builds account spendable-output API path | `transcripts/02-account-app-api-contract-tests-after-registration.txt` |
| `EconomyNodeLedgerClient.GetTransactionsAsync(..., includeData: true)` | Builds account transaction list API path with scenario/includeData | `transcripts/02-account-app-api-contract-tests-after-registration.txt` |
| `EconomyNodeLedgerClient.GetTransactionAsync(context, txId, includeData: true)` | Builds ledger-scoped transaction detail path | `transcripts/02-account-app-api-contract-tests-after-registration.txt` |
| `EconomyNodeLedgerClient.GetTransactionAsync(txId, includeData: true)` | Builds top-level transaction detail path | `transcripts/02-account-app-api-contract-tests-after-registration.txt` |
| `EconomyNodeLedgerClient.SubmitTransactionAsync` | Posts signed transaction to broadcast endpoint without private key material | `transcripts/02-account-app-api-contract-tests-after-registration.txt` |
| `AddEconomySdkWithBrowserNodeDriver` | Registers browser-safe `EconomyNodeLedgerClient`, `ILedgerGateway`, and `ILedgerReadClient` | `transcripts/02-account-app-api-contract-tests-after-registration.txt` |

## Negative Fixture Proof

| Fixture | Expected Result | Evidence |
| --- | --- | --- |
| Static API key in browser-safe options | Not representable | `EconomyBrowserNodeClientOptions` exposes no API key property |
| Static bearer token in browser-safe options | Not representable | `EconomyBrowserNodeClientOptions` exposes no bearer-token property |
| Private-key sentinel in broadcast body | Absent | `EconomyNodeAccountAppApiContractTests` |
| `privateKey` / `privateKeyMaterial` request fields | Absent | `EconomyNodeAccountAppApiContractTests` |
| Stub/static-secret audit | Zero hits | `transcripts/04-sb03-stub-secret-key-audit.txt` |

## Command Transcripts

| Transcript | Result |
| --- | --- |
| `transcripts/01-failing-account-app-api-contract-tests.txt` | Expected red compile: missing `AddEconomySdkWithBrowserNodeDriver` |
| `transcripts/02-account-app-api-contract-tests-after-registration.txt` | SB03 contract tests: 2 passed |
| `transcripts/03-account-app-api-and-sdk-regression-tests.txt` | SB03 plus existing SDK tests: 4 passed |
| `transcripts/04-sb03-stub-secret-key-audit.txt` | No stub/static-secret/private-key assignment hits in changed scope |
| `transcripts/05-full-ledger-tests-after-sb03.txt` | Full `CanDoItAll.Ledger.Tests`: 181 passed |
| `transcripts/06-git-diff-check-after-sb03.txt` | `git diff --check` exit `0`; only LF-to-CRLF normalization warnings |
| `transcripts/07-workbook-regeneration-after-sb03.txt` | XLSX regenerated; formula error scan matched 0 entries |

## Validation Commands

```powershell
dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj --no-restore --filter EconomyNodeAccountAppApiContractTests
dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj --no-restore --filter "EconomyNodeAccountAppApiContractTests|EconomyClientTests"
rg -n "TODO|NotImplementedException|throw new NotImplemented|ApiKey\s*=|BearerToken\s*=|privateKeyMaterial\s*=|privateKey\s*=" src/CanDoItAll.Economy.Sdk/ServiceCollectionExtensions.cs tests/CanDoItAll.Ledger.Tests/EconomyNodeAccountAppApiContractTests.cs
dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj --no-restore
git diff --check
node bundles/2026-06-29-account-wasm-pwa-demo/workbook/build-planning-workbook.mjs
```

## Known Non-SB03 Blockers

- Full solution build blockers documented in SB01 still apply outside SB03. They did not affect SDK project/test proof.

## Changed File Hashes

- See `changed-file-hashes.txt`.
