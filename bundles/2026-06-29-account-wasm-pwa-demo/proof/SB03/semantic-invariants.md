# SB03 Semantic Invariants

## Shallow-Pass Trap

A shallow implementation could claim "API only" because endpoint names exist, while the SDK still requires in-process services, embeds static API secrets in browser configuration, or sends private key material inside broadcast payloads.

SB03 rejects that trap with request-capturing SDK tests, a browser-safe DI registration that has no static secret fields, no-private-key body assertions, and endpoint fixtures for every AccountApp read/broadcast call.

## Invariants

| Invariant | Proof |
| --- | --- |
| AccountApp data loading can use only HTTP API calls | `EconomyNodeAccountAppApiContractTests.AccountAppNodeClient_UsesOnlyApiReadsAndSignedBroadcastWithoutPrivateKeyMaterial` |
| Spendable outputs use the account API path | Captured `GET /api/v1/ledger/{ledgerId}/accounts/{address}/spendable-outputs` |
| Transaction list uses the account API path with `includeData=true` | Captured `GET /api/v1/ledger/{ledgerId}/accounts/{address}/transactions?...&includeData=true` |
| Transaction detail is available through ledger-scoped and top-level paths | Captured `GET /api/v1/ledger/{ledgerId}/transactions/{txId}` and `GET /api/v1/transactions/{txId}` |
| Broadcast sends a signed `LedgerTransaction`, not private key material | Test asserts `signatureBundle` and `publicKeyMaterial` are present and `privateKey`/`privateKeyMaterial` are absent |
| Browser-safe SDK registration cannot embed static API key or bearer token | `EconomyBrowserNodeClientOptions` exposes only `ApiBasePath`; registration maps to `ILedgerGateway` and `ILedgerReadClient` |
| Existing SDK behavior stays intact | `EconomyClientTests` regression run remains green |

## Adversarial Negative Proof

- Private-key sentinel: test asserts the broadcast body does not contain the base64 sentinel value for `private-key-do-not-send`.
- Private-key fields: test asserts the broadcast body does not contain `privateKey` or `privateKeyMaterial`.
- Static browser secrets: browser-safe options expose only API base path; no API key or bearer-token options are available in that registration path.
- Stub/secret audit: no `TODO`, `NotImplementedException`, static `ApiKey =`, static `BearerToken =`, or private-key assignment hits in the changed SB03 registration/test scope.

## Semantic Positive Proof

- The SDK can call spendable outputs, account transaction list, transaction detail, and signed broadcast using `EconomyNodeLedgerClient`.
- The browser-safe DI extension registers `EconomyNodeLedgerClient` as both `ILedgerGateway` and `ILedgerReadClient`, allowing later AccountApp services to use existing business object abstractions through HTTP.
- Endpoint fixtures document the exact browser request shape and explicitly mark private key material as disallowed.

## Raw-Note Literal Closure

- "API is just to get transactions info and list for address and broadcast of the signed transactions": SB03 proves list/detail/spendable read calls and signed broadcast.
- "Signature and assembly of transaction will happen on side of the AccountApp": SB03 does not implement signing; it proves the node client sends only signed transactions and no private key material.
- "AccountApp must communicate with node only via API": SB03 adds browser-safe SDK registration and request-shape tests for HTTP-only node access.
