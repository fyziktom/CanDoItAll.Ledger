# SB06 Dependency Audit

## Result

- Status: `Passed`
- Date: `2026-06-29`

## Checks

| Check | Result | Evidence |
| --- | --- | --- |
| AccountApp has no `Microsoft.AspNetCore.App` framework reference | Passed | `CanDoItAll.Economy.AccountApp.csproj` uses Blazor WASM packages and project references only. |
| AccountApp browser path has no NBitcoin or BouncyCastle dependency | Passed | `rg "NBitcoin|BouncyCastle" examples/CanDoItAll.Economy.AccountApp src/CanDoItAll.Economy.Cryptography.Browser src/CanDoItAll.Economy.BusinessObjects` returned no matches. |
| Browser build does not depend on local IPFS engine/server hosting | Passed | BusinessObjects IPFS storage is disabled for browser builds and AccountApp project references pass `EnableBusinessObjectIpfsStorage=false`. |
| AccountApp does not compile static API key or bearer token values | Passed | `appsettings.json` contains node/API/ledger configuration only; SDK auth options remain optional and unset in AccountApp registration. |
| Browser crypto is isolated to WebCrypto/IndexedDB interop | Passed | `CanDoItAll.Economy.Cryptography.Browser` references only the shared crypto abstractions, DI abstractions, and JS interop. |
| Remaining `System.Security.Cryptography` references are non-WASM/server compatibility paths | Passed | BusinessObjects keeps .NET crypto for non-WASM import/backup/payload paths; AccountApp uses browser-driver import/backup flows. |

## Notes

- The dependency scan finds visible field names such as `PrivateKeyMaterialBase64` in browser interop DTOs and AccountApp input state. The final browser proof verifies those values are cleared after use and are not persisted raw to IndexedDB, local storage, session storage, or node API requests.
