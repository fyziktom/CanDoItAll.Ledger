# Target Solution

## Architecture

- Add a shared BO schema/catalog layer in `CanDoItAll.Economy.BusinessObjects` that defines supported generic families, aliases, field descriptors, sample defaults, and parse/build helpers.
- Keep `BusinessObjectMediaPayload` as the common base for display metadata and attachments, but layer typed domain details on top through extensible extra fields rather than one class per UI branch.
- Register typed casters through the existing `IBusinessObjectCaster` and `IBusinessObjectCastRegistry` path so AccountApp, SSR demos, tests, gallery, details, and renderers consume the same parsed data.
- Add a WASM-compatible IPFS upload service in AccountApp or a WASM-safe shared area. It must use `HttpClient` and `MultipartFormDataContent` against a configured API base URI, returning `BusinessObjectDataItem` with IPFS CID metadata.
- Add AccountApp options for `IpfsApiBaseUri`, `IpfsGatewayBaseUri`, and `EnableIpfsUploads`.
- Extend the AccountApp docker nginx config and entrypoint to expose `/ipfs-api/` to the IPFS docker service and write appsettings for the browser.
- Extend the wizard so object type selection and type-specific fields build typed payloads without hard-coding gallery-only parsing.
- Extend gallery/details/renderers to read typed BO view data and show family-specific facts while preserving shared media/attachment preview behavior.

## Boundaries

- Do not move ledger private keys or signing out of WASM.
- Do not add VEFramework dependencies.
- Do not implement domain-specific VEFramework NFTs excluded in `inventories/01-scope-inventory.md`.
- Do not treat inline sample data as browser proof; final proof needs signed AccountApp transactions.

## Proof Strategy

- Unit tests prove schema aliases, payload builder/parser behavior, typed caster selection, malformed fallback behavior, and IPFS upload service request/response handling.
- Component tests prove wizard/gallery/detail rendering for all in-scope families.
- Docker proof proves IPFS and ledger app stacks are running.
- Playwright proof creates real BOs in Alice AccountApp, uploads at least one image attachment through IPFS, sends/transfers at least one BO to Bob, refreshes Bob, and verifies gallery/detail data.
