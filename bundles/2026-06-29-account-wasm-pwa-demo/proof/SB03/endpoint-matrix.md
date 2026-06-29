# SB03 AccountApp API Endpoint Matrix

## Browser-Safe Configuration

- SDK registration: `AddEconomySdkWithBrowserNodeDriver(Uri nodeBaseUri, Action<EconomyBrowserNodeClientOptions>? configure = null)`.
- Allowed static browser configuration: node base URI and API base path only.
- Not supported in the browser-safe registration: static API key, static bearer token, node operator private key, or ledger admin private key.
- Auth decision for the demo PWA: reads can use the node public-read mode; signed broadcast can use development/same-origin mode for demo or a future user JWT flow. Do not embed operator API keys into PWA assets.

## Required AccountApp Calls

| Need | Method | Path | SDK Method | Body | Key-Material Rule |
| --- | --- | --- | --- | --- | --- |
| Spendable outputs | `GET` | `/api/v1/ledger/{ledgerId}/accounts/{address}/spendable-outputs?scenarioId={scenarioId}` | `GetSpendableOutputsAsync` | none | no private key |
| Account transaction list | `GET` | `/api/v1/ledger/{ledgerId}/accounts/{address}/transactions?scenarioId={scenarioId}&includeData=true` | `GetTransactionsAsync(query, includeData: true)` | none | no private key |
| Ledger-scoped transaction detail | `GET` | `/api/v1/ledger/{ledgerId}/transactions/{txId}?scenarioId={scenarioId}&includeData=true` | `GetTransactionAsync(context, txId, includeData: true)` | none | no private key |
| Top-level transaction detail | `GET` | `/api/v1/transactions/{txId}?includeData=true` | `GetTransactionAsync(txId, includeData: true)` | none | no private key |
| Signed transaction broadcast | `POST` | `/api/v1/ledger/transactions` | `SubmitTransactionAsync` | signed `LedgerTransaction` | public key, payload hash, signature bytes only |

## Contract Test Proof

- `EconomyNodeAccountAppApiContractTests.AccountAppNodeClient_UsesOnlyApiReadsAndSignedBroadcastWithoutPrivateKeyMaterial`
  - Captures all five AccountApp calls above.
  - Asserts no `Authorization` header and no `X-CDA-Economy-Key` header in the browser-safe default.
  - Asserts signed broadcast body contains `signatureBundle` and `publicKeyMaterial`.
  - Asserts signed broadcast body does not contain the private-key sentinel and does not contain `privateKey` / `privateKeyMaterial` fields.
- `EconomyNodeAccountAppApiContractTests.BrowserNodeDriverRegistration_ExposesLedgerClientsWithoutStaticSecrets`
  - Proves DI exposes `EconomyNodeLedgerClient`, `ILedgerGateway`, and `ILedgerReadClient`.
  - Proves API base path normalization for browser registration.

## Downstream

- SB04 can use this matrix for browser-side transaction assembly/signing and signed broadcast.
- SB05 can build data-loading adapters around the read methods.
- SB06 can configure AccountApp with only node base URL and API base path.
