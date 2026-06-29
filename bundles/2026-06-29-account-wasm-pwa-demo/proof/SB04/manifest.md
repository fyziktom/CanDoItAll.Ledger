# SB04 Proof Manifest - Transaction Assembly Signing Split

## Status

- Closure result: `Passed`
- Date: `2026-06-29`
- Scope closed: unsigned transaction assembly boundary, reusable account transaction signer, crypto-driver input signer for browser-compatible P-256 signatures, vault-unlocked key-source adapter, browser-safe SDK signing registration, representative assemble/sign/verify/broadcast tests, and browser WebCrypto POST proof.
- Downstream gate: SB05 and SB06 may consume unsigned draft builders, `IAccountTransactionSigner`, `CryptoDriverAccountInputSigner`, `AccountVaultKeySource`, and the browser-safe SDK registration.

## Implementation Summary

- Added `IAccountTransactionSigner` and `AccountTransactionSigner` to apply transaction ids, signature payload hashes, and input signature bundles as a separate step from draft assembly.
- Added `CryptoDriverAccountInputSigner` so browser paths sign through `IEconomyCryptoDriver`; in WASM this routes to the SB01 WebCrypto implementation.
- Added `AccountVaultKeySource` as the adapter from an unlocked encrypted vault key to `IAccountKeySource`.
- Refactored `BusinessObjectTransactionBuilder` so every existing signed builder method delegates to a new unsigned draft method and then signs through `IAccountTransactionSigner`.
- Added account-level unsigned pass-through methods on `BusinessObjectAccount`.
- Updated `AddEconomySdkWithBrowserNodeDriver` to opt into `AddBusinessObjectBrowserAccountSigning`, selecting the crypto-driver signer for AccountApp browser usage.
- Added browser/network proof harness under `proof/SB04/` that imports the production WebCrypto JS module, signs and verifies a payload, POSTs a signed transaction-shaped body, and captures the POST body.

## Source Assertions

- Draft assembly does not call `GetPrivateKeyMaterialAsync`.
- Signing happens after draft assembly and is locally bound to the unlocked key source.
- Server/SSR callers using existing signed builder methods still receive signed transactions.
- Browser-safe SDK registration resolves `CryptoDriverAccountInputSigner` and `AccountTransactionSigner`.
- Broadcast payloads contain `signatureBundle` and `publicKeyMaterial`, and exclude private-key fields/material.

## Production Behavior Artifact Matrix

| Artifact | Behavior Proven | Evidence |
| --- | --- | --- |
| `BusinessObjectTransactionBuilder.BuildUnsigned*Async` | Assembles unsigned drafts without signatures or private key access | `transcripts/02-transaction-assembly-signing-split-tests-green.txt` |
| `AccountTransactionSigner.SignAsync` | Applies `TxId`, `SignaturePayloadHash`, and per-input signature bundles | `transcripts/02-transaction-assembly-signing-split-tests-green.txt` |
| `CryptoDriverAccountInputSigner` | Signs P-256 payload hashes via the crypto driver and emits public key material only | `transcripts/02-transaction-assembly-signing-split-tests-green.txt`, `transcripts/04-browser-signing-harness.txt` |
| `AddEconomySdkWithBrowserNodeDriver` | Browser-safe registration uses the crypto-driver account signer with no static browser secrets | `transcripts/03-api-and-signing-contract-tests-green.txt` |
| `EconomyNodeLedgerClient.SubmitTransactionAsync` | Broadcasts signed transaction body without private key fields/material | `transcripts/03-api-and-signing-contract-tests-green.txt`, `sign-broadcast-network.json` |
| Browser WebCrypto harness | WebCrypto P-256 signs/verifies and POSTs signed transaction-shaped body | `browser-signing-harness-result.json`, `signed-transaction-result.png`, `sign-broadcast-network.png` |

## Negative Fixture Proof

| Fixture | Expected Result | Evidence |
| --- | --- | --- |
| Draft assembly with guarded private key source | Succeeds without private key access | `UnsignedDraftAssembly_DoesNotRequirePrivateKeyMaterial` in `BusinessObjectTransactionAssemblySigningSplitTests` |
| Signing with guarded private key source | Fails because signing is the only private-key step | `UnsignedDraftAssembly_DoesNotRequirePrivateKeyMaterial` |
| Broadcast private key material | Absent from body | `SignedBrowserTransaction_BroadcastPayloadContainsNoPrivateKeyMaterial`, `sign-broadcast-network.json` |
| Browser broadcast private-key fields | Absent from captured POST body | `sign-broadcast-network.json` |
| Static browser API key/bearer token | Not present in browser-safe SDK registration | `EconomyNodeAccountAppApiContractTests`, `transcripts/05-sb04-stub-secret-key-audit.txt` |

## Command Transcripts

| Transcript | Result |
| --- | --- |
| `transcripts/01-failing-transaction-assembly-signing-split-tests.txt` | Expected red compile: missing `IAccountTransactionSigner` |
| `transcripts/02-transaction-assembly-signing-split-tests-green.txt` | SB04 focused tests: 4 passed |
| `transcripts/03-api-and-signing-contract-tests-green.txt` | SB03/SB04 API and signing contract tests: 6 passed |
| `transcripts/04-browser-signing-harness.txt` | Browser WebCrypto sign/verify/POST harness passed |
| `transcripts/05-sb04-stub-secret-key-audit.txt` | No production stub/static-secret/auth markers; captured POST has no private key field and no unsigned draft |
| `transcripts/06-full-ledger-tests-after-sb04.txt` | Full `CanDoItAll.Ledger.Tests`: 185 passed |
| `transcripts/07-git-diff-check-after-sb04.txt` | `git diff --check` exit `0`; LF-to-CRLF warnings only |
| `transcripts/08-workbook-regeneration-after-sb04.txt` | XLSX regenerated; formula error scan matched 0 entries |

## Browser And Network Artifacts

| Artifact | Result |
| --- | --- |
| `browser-signing-harness.html` | Production WebCrypto module harness |
| `browser-signing-harness-result.json` | `success=true`, `signatureByteLength=64`, `verified=true`, `privateKeyLeaked=false`, `broadcastStatus=200` |
| `sign-broadcast-network.json` | Captured POST `/api/v1/ledger/transactions`, `containsPrivateKeyField=false`, `containsUnsignedDraft=false` |
| `signed-transaction-result.png` | Browser screenshot after signing and POST |
| `sign-broadcast-network.png` | Browser screenshot for network proof artifact |

## Validation Commands

```powershell
dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj --filter FullyQualifiedName~BusinessObjectTransactionAssemblySigningSplitTests --no-restore
dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj --filter "FullyQualifiedName~BusinessObjectTransactionAssemblySigningSplitTests|FullyQualifiedName~EconomyNodeAccountAppApiContractTests" --no-restore
$root='C:\Users\dell\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\node_modules'; $env:NODE_PATH="$root;$root\.pnpm\node_modules"; & 'C:\Users\dell\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe' 'bundles/2026-06-29-account-wasm-pwa-demo/proof/SB04/run-browser-signing-harness.cjs'
dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj --no-restore
git diff --check
node bundles/2026-06-29-account-wasm-pwa-demo/workbook/build-planning-workbook.mjs
```

## Known Non-SB04 Blockers

- Full solution build blockers documented in SB01 still apply outside SB04. The full ledger test project is green.
- SB05 still owns shared UI host-neutral adapter work.
- SB06 still owns the real WASM PWA app route and end-to-end app wiring.

## Changed File Hashes

- See `changed-file-hashes.txt`.
