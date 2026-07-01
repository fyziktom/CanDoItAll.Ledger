# SB02 Proof Manifest

## Status

- Subbundle: `SB02 Commerce Contracts And Parsers`
- Closure decision: `Passed`
- Closed at: `2026-06-30`
- Owned requirements: `REQ-002`, `REQ-003`, `REQ-007`, `REQ-009`
- Owned raw notes: `RN-02`, `RN-03`, `RN-07`

## Production Changes

- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectCommerce.cs`
  - Added typed sale, payment, purchase, and receipt records.
  - Added builder/parser helpers for sale terms, commerce payloads, deterministic derived object ids, and commerce facts.
  - Added validators for sale terms and payment/purchase/receipt records.
  - Added a commerce typed view and caster for `payment`, `purchase`, and `receipt` BOs.
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectDataItems.cs`
  - Added structured `commerce` metadata to `BusinessObjectMediaPayload`.
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs`
  - Preserved normalized commerce metadata during media payload parse.
- `repo://src/CanDoItAll.Economy.BusinessObjects/ServiceCollectionExtensions.cs`
  - Registered the commerce caster ahead of the generic typed family caster.
- `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectCommerceContractTests.cs`
  - Added contract tests for sale terms, commerce records, deterministic ids, and typed payment casting.

## Evidence

- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-commerce-contract-tests.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/passing-commerce-contract-tests.txt`
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- Semantic invariants: `bundle://proof/SB02/semantic-invariants.md`

| Evidence | Path | Result |
| --- | --- | --- |
| Failing-first contract tests | `bundle://proof/SB02/transcripts/failing-first-commerce-contract-tests.txt` | Failed before implementation because commerce contracts and payload `Commerce` did not exist. |
| Passing targeted tests | `bundle://proof/SB02/transcripts/passing-commerce-contract-tests.txt` | `Passed!` 4 commerce contract tests. |
| Regression tests | `bundle://proof/SB02/transcripts/regression-commerce-casting-and-long-description.txt` | `Passed!` 19 tests covering commerce, existing casting, and SB01 long description. |
| Source assertions | `bundle://proof/SB02/transcripts/source-assertions.txt` | Shows typed contracts/builders/caster and no VEFramework dependency names. |
| Anti-stub audit | `bundle://proof/SB02/transcripts/anti-stub-audit.txt` | No TODO, NotImplemented, VEFramework copy markers, or reported Alice object/hash hard-codes in SB02 changed scope. |
| Changed-file hashes | `bundle://proof/SB02/transcripts/changed-file-hashes.txt` | Before/after SHA-256 recorded; `BusinessObjectCommerce.cs` after hash `db053e9d6fe0ef2a06da8f15a568e397d7a6646ff335c3021e5d48231a589ea2`. |

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Structured sale metadata state | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectCommerce.cs` | `bundle://proof/SB02/transcripts/passing-commerce-contract-tests.txt` | `bundle://proof/SB03/transcripts/api-sale-payment-state.json` | `bundle://proof/SB02/transcripts/failing-first-commerce-contract-tests.txt` |
| Payment/purchase/receipt records | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectCommerce.cs` | `bundle://proof/SB02/transcripts/source-assertions.txt` | `bundle://proof/SB02/transcripts/regression-commerce-casting-and-long-description.txt` | `bundle://proof/SB02/transcripts/anti-stub-audit.txt` |
| Deterministic derived ids | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectCommerce.cs` | `bundle://proof/SB02/transcripts/passing-commerce-contract-tests.txt` | `bundle://proof/SB04/transcripts/dispatch-focused-tests.txt` | `bundle://proof/SB02/transcripts/failing-first-commerce-contract-tests.txt` |
| VEFramework-free implementation | `bundle://proof/SB02/transcripts/source-assertions.txt` | `bundle://proof/SB02/transcripts/anti-stub-audit.txt` | `bundle://proof/SB03/manifest.md` | `bundle://proof/SB02/transcripts/anti-stub-audit.txt` |

## Closure Notes

- Manual payment, purchase, and receipt creation remains supported through the existing family catalog. The commerce caster adds typed facts when commerce metadata exists.
- Main-currency prices are represented as `MAIN` when no token id is present. Token id fields are structured but token-priced purchase execution remains optional until UI/adapter support is proven.
- SB03 and SB04 may now consume sale terms and commerce records.
