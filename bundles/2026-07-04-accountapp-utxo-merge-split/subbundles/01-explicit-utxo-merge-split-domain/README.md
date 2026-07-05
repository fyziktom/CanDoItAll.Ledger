# SB01 Explicit UTXO Merge Split Domain

## Status

- `Completed`

## Objective

- Add explicit selected-UTXO merge/split transaction semantics to the account domain and adapter/session layers so later UI work can submit correct vin/vout shapes.

## Success Criteria

- Domain tests prove Merge consumes exactly selected eligible UTXOs and creates one output.
- Domain tests prove Split consumes selected eligible UTXOs and creates equal or specific outputs with correct change behavior.
- Negative tests reject BO, token, permission, rule, duplicate, missing, empty, zero, and overspending selections.
- SSR and WASM adapter/session switch paths route new modes to the explicit UTXO builder logic.

## Covered Inputs

- R003, R004, R005, R006, R007, R008, and the transaction-semantics half of R012.

## Prerequisites

- Prepared-stage validator passed.
- No earlier implementation subbundle is required.

## Exact Source References

- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectRequests.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectAccount.cs`
- `repo://src/CanDoItAll.Economy.Components/Models/BusinessObjectTransactionDraft.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectAdvancedTransactionTests.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs`

## Deliverables

- Request/model types for explicit UTXO merge and split.
- Builder/account methods for signed and unsigned explicit UTXO merge/split.
- Draft/mode fields needed by adapters.
- SSR/WASM/development adapter support for new modes.
- Focused tests and proof transcripts.

## Dependency Impact

- SB02 depends on this subbundle for the form submit target and eligible-output helper semantics.
- SB03 depends on this subbundle for real transaction proof. If SB01 is wrong, browser proof could submit transactions with misleading labels only.

## Validation Depth

- Critical foundation with Semantic Adequacy Gate and artifact-backed proof manifest.

## Implementation Steps

1. Add failing tests for selected-UTXO Merge and Split semantics before production changes.
2. Add request/model shapes for explicit selected inputs and split outputs.
3. Implement eligible UTXO resolution that rejects BO/token outputs and duplicates.
4. Implement unsigned/signed merge and split transaction builder methods.
5. Add account API methods and adapter/session routing for new modes.
6. Run targeted tests, source assertions, anti-stub audit, and record proof.

## Scope Exceptions

- UI selectors, reusable Razor components, docker proof, and screenshots are owned by SB02/SB03.
- Token and BusinessObject UTXO merge/split are out of scope.

## Do Not Do

- Do not change existing BO merge/split request semantics.
- Do not add UI-only state without domain proof.
- Do not allow token, BO, permission, or rule outputs as eligible inputs.

## Acceptance Checklist

- [x] Failing-first transcript exists for tests that fail before production changes.
- [x] Passing transcript exists for the same tests after implementation.
- [x] Source assertions show explicit selected-input resolution in production code.
- [x] Anti-stub audit passes for production code touched by this subbundle.
- [x] `proof/SB01/manifest.md` and `proof/SB01/semantic-invariants.md` exist and cite real artifacts.

## Proof Required

- `dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj --filter "FullyQualifiedName~BusinessObjectAdvancedTransactionTests|FullyQualifiedName~AccountWorkspaceAdapterTests"`.
- Source assertion transcript showing request types, builder methods, adapter switch cases, and tests.
- Anti-stub audit transcript covering touched production files.
- `bundle://proof/SB01/manifest.md`.
- `bundle://proof/SB01/semantic-invariants.md`.

## Browser Validation Logging

- N/A for this domain-only subbundle. Browser proof is required downstream in SB02/SB03.

## Progression Gate

- SB02 may start only after targeted tests pass and SB01 manifest/semantic invariants cite failing-first, passing, source assertion, and anti-stub transcripts.

## Suggested Agent Prompt

```text
Implement SB01 only. Add explicit plain UTXO merge/split transaction semantics, prove selected vin/vout behavior and disallowed-output rejection with failing-first and passing tests, update proof artifacts, and stop before UI changes.
```
