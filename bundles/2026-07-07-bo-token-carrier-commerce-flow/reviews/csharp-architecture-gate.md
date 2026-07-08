# C# Architecture Gate

Status: closed

Pre-implementation findings:

- The design keeps token-source selection in component/application services and token transaction assembly in the domain builder.
- No partial-class expansion or broad manager extraction is planned.
- No new project dependency direction is required.
- CodeAnalytics MCP snapshot was attempted but blocked by SDK resolution outside this repo; CLI tests will be used as proof.

Closure checks:

- [x] No duplicated carrier-token selection logic across UI and dispatcher.
- [x] Clone builder handles token source/change atomically.
- [x] Existing main-only APIs stay explicit and tested.
- [x] Docker proof includes real transaction screenshots, not only unit tests.

Closure notes:

- Carrier-token defaults are centralized in `BusinessObjectCarrierTokenPolicy`.
- Domain token conservation remains enforced in `BusinessObjectTransactionBuilder`; commerce fallback only chooses a token id and lets the builder validate a real source UTXO.
- CodeAnalytics MCP remained blocked by the external SDK resolution issue recorded during preparation, so closure relies on focused tests, AccountApp build, and docker proof.
