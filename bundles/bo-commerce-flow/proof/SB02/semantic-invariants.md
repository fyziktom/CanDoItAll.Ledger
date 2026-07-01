# SB02 Semantic Invariants

## Typed Commerce Contract

- Invariant ID: `BusinessObjectCommerceValidator`
- Source raw note: `RN-02`, `RN-03`, and `RN-07` required VEFramework-style BO/NFT commerce semantics without copying VEFramework implementation.
- Expected behavior: Sale terms, payment, purchase, and receipt records are typed metadata on `BusinessObjectMediaPayload.Commerce`, validate buyer/seller/target/amount/status links, and preserve manual family creation.
- Disallowed shallow implementation: Storing payment/purchase/receipt state as opaque strings, bypassing validation, or introducing dependencies on VEFramework NFT classes.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-commerce-contract-tests.txt` fails before implementation because the commerce validator, builder, records, and payload `Commerce` property are missing.
- Passing test: `bundle://proof/SB02/transcripts/passing-commerce-contract-tests.txt` passes sale, record, deterministic-id, and typed-casting contract tests.
- Changed source files: `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectCommerce.cs`, `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectDataItems.cs`, `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs`, `repo://src/CanDoItAll.Economy.BusinessObjects/ServiceCollectionExtensions.cs`, and `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectCommerceContractTests.cs`.
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions.txt` verifies typed contracts, builders, caster registration, and no VEFramework dependency names.
- Red-team negative case: `bundle://proof/SB02/transcripts/anti-stub-audit.txt` confirms no TODO, NotImplemented, VEFramework copy marker, or reported Alice object/hash hard-code.
- Downstream dependency check: `bundle://proof/SB02/transcripts/regression-commerce-casting-and-long-description.txt` proves existing casting plus SB01 long-description behavior still pass before SB03/SB04 consume the contracts.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Structured sale metadata state | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectCommerce.cs` | `bundle://proof/SB02/transcripts/passing-commerce-contract-tests.txt` | `bundle://proof/SB02/transcripts/regression-commerce-casting-and-long-description.txt` | `bundle://proof/SB02/transcripts/failing-first-commerce-contract-tests.txt` |
| Commerce payment/purchase/receipt records | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectCommerce.cs` | `bundle://proof/SB02/transcripts/source-assertions.txt` | `bundle://proof/SB02/transcripts/regression-commerce-casting-and-long-description.txt` | `bundle://proof/SB02/transcripts/anti-stub-audit.txt` |
| Deterministic clone/purchase/receipt id lifecycle | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectCommerce.cs` | `bundle://proof/SB02/transcripts/passing-commerce-contract-tests.txt` | `bundle://proof/SB04/transcripts/dispatch-focused-tests.txt` | `bundle://proof/SB02/transcripts/failing-first-commerce-contract-tests.txt` |

## Progression Gate

- SB03 can use `BusinessObjectCommercePayloadBuilder.WithSaleTerms` for Set Sale.
- SB03 can use `CreatePaymentRecord` and `CreatePaymentPayload` for Buy BO.
- SB04 can use deterministic clone/receipt/purchase ids and validators for idempotent dispatch.
