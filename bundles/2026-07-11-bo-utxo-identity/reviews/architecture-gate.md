## C# Architecture Gate Result

Status: Pass with follow-up

### Findings

| Severity | Finding | Evidence | Required action |
|---|---|---|---|
| Resolved blocker | A `previous-version` reference could name a different consumed BO lineage from the Update/Transfer target. | `BusinessObjectReferenceValidator` now binds the canonical reference to the data-bound target lineage; positive and adversarial raw-transaction tests cover it. | None. Keep the regression test in the protocol gate. |
| Resolved blocker | A structured BO transaction could silently consume an unrelated BO that was neither a source nor re-emitted. | Structured-reference validation now requires every consumed BO to be a required canonical source or to have the same lineage re-emitted. Permission/rule-style pass-through is explicitly covered. | None. Preserve legacy unstructured replay until V3. |
| Resolved blocker | SDK transaction-summary mapping trusted the node-supplied BO `objectId` without independently binding it to the containing output. | Every transaction-summary output now validates `objectId == BusinessObjectId.FromOutPoint(txId, outputIndex)` and fails closed; an adversarial SDK test supplies a different valid canonical outpoint and verifies rejection. All three client transaction-summary methods share this mapper. | None. Keep the adversarial boundary test with the public-contract suite. |
| Follow-up | V1/V2 still serializes the historical `ObjectId` property as an internal lineage key. | `BusinessObjectRef.LineageKey` owns the value while the JSON property name and obsolete alias preserve historical transaction hashes. Canonicalizer compatibility tests prove byte/hash parity. | Remove the obsolete alias and caller-selected request constructors only with a versioned V3 wire/API migration. |
| Follow-up | The legacy Node mint/transfer marker is still named `businessObjectId` and can carry unstructured hashed JSON. | It generates opaque internal lineage, never becomes a public ID, and has no current UI/CLI production consumer. | In V3, rename/remove the marker and require the structured type/schema envelope universally. |
| Follow-up | Commerce retry recovery searches the workspace's latest 100 transactions. | Commerce dispatcher retry tests cover partial clone/original delivery and receipt/purchase recovery, but recovery visibility is intentionally bounded. | Replace the bounded scan with an indexed external-reference query when the read API gains that capability. |
| Informational | CodeAnalytics could not produce a snapshot because the sibling Components workspace requested unavailable SDK `10.0.200`. | Direct project-file inspection found no changed `.csproj`, `.props`, `.targets`, `ProjectReference`, or `PackageReference`; the full solution build completed with zero errors. | Repair the sibling SDK pin separately; it does not block this dependency-neutral migration. |

### Dependency direction

Pass. Core owns the canonical outpoint conversion, internal lineage value, and structured payload contract. BusinessObjects owns opaque lineage construction and transaction request/build semantics. Ledger owns reference validation. Persistence and Node derive public identity at read/projection boundaries. SDK and Components consume canonical IDs and do not reference persistence implementations. No project references changed, no cycle was introduced, and domain code gained no service-locator dependency.

### Partial-class policy

Pass. No new partial class or partial file was added. Existing host partial declarations were not expanded as an architecture boundary.

### Testability proof

- Pure identity and wire-compatibility tests cover canonical `txid:index` round trips, malformed identifiers, and unchanged V1/V2 canonical hashes.
- Reference-validator tests use an isolated `ILedgerStore` and cover malformed, missing, duplicate, non-canonical, wrong-type, wrong-schema, wrong-hash, wrong-operation, consumed-clone, unconsumed-update, merge, close, cross-lineage previous-version, unrelated silent consumption, and valid same-lineage pass-through cases.
- Node and EF read-model tests prove current IDs come from output outpoints. Public history follows canonical `previous-version` references; trusted in-process summaries may use lineage only as a compatibility fallback, and no public query accepts lineage as a BO ID.
- Explicit Node/SDK transaction-read DTOs omit lineage and validate/derive `objectId` from transaction/output position on all three summary routes. The SDK independently fails closed when a transaction-summary BO ID differs from its containing outpoint. Spendables carry protocol correlation only under the separately named `lineageKey`, after validating canonical `objectId` against the outpoint.
- Commerce tests cover confirmed payment/purchase/receipt/delivery IDs and retry recovery.
- The final review-panel contract names the canonical source `Clone-source reference` and the optional free-form value `External reference`, so the two concepts cannot be mistaken for each other.
- Component/source-contract checks prove create/clone/merge have no target-ID input and review pending identity as “Assigned after confirmation”; the live WASM flow visually verified the same behavior before the final wording-only review-label refinement.
- Final automated result: 483 passed, 0 failed, 0 skipped. The full serial solution build completed with 0 errors and 63 existing warnings; BusinessObjects itself builds with 0 warnings.

### Closure decision

The UTXO-identity migration may proceed. Public production paths no longer fabricate BO IDs from names, types, slugs, or caller input; clone lineage is represented by a canonical source reference in BO data. Remaining follow-ups are explicit V3 compatibility/scale work and do not weaken current public identity correctness.
