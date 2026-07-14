# Implementation Plan

1. [x] Introduce canonical outpoint identity and explicit legacy lineage-key contracts without changing V1/V2 hashes.
2. [x] Standardize BO payload envelopes and typed source references.
3. [x] Remove target IDs from generic create/clone/merge production requests and generate opaque lineage keys inside the builder.
4. [x] Resolve source selections by current outpoint and keep stable lineage keys only for wire transitions.
5. [x] Derive holding/version IDs at in-process, EF, Node, and SDK read boundaries.
6. [x] Update AccountApp drafts/forms/reviews to show pending identity before confirmation and canonical IDs afterward.
7. [x] Migrate commerce, authority, console, simulator, demo, batch, raw-transaction, and direct-lookup paths.
8. [x] Add characterization, negative validation, unit, integration, Node-contract, persistence, commerce, and UI tests.
9. [x] Rebuild/restart Docker after the final public read-contract and reference-integrity repairs; verify readiness and live canonical transaction/spendable JSON.
10. [x] Complete final in-app WASM clone picker/details/review interaction and screenshot validation without broadcasting a transaction.
