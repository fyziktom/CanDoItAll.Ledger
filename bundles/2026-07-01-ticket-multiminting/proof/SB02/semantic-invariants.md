# SB02 Semantic Invariants

## Invariant Contract

- Invariant ID: `MM-BATCH-01`
- Source raw note: User asked to integrate multimint while preserving normal minting.
- Expected behavior: Single ticket minting remains available when multimint is disabled.
- Disallowed shallow implementation: Replacing the standard wizard with batch-only behavior.
- Failing-first test: N/A process exemption; existing test suite and targeted component tests cover behavior after implementation.
- Passing test: `bundle://proof/SB02/transcripts/dotnet-test-passing.txt`.
- Changed source files: `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` hash `bf3db3e699c8c3e2d159f398b5ad856eaeb44077a61c9808f726f1c202017f2e`.
- Production assertions: Wizard only invokes batch path when ticket multimint is enabled.
- Red-team negative case: Batch-only regression would break existing BO wizard tests in the 237-test run.
- Downstream dependency check: SB03 reached standard event creation and ticket wizard before enabling multimint.

- Invariant ID: `MM-BATCH-02`
- Source raw note: User asked for own account, subaccount, different account, or recipient-list multimint.
- Expected behavior: Ticket multimint emits one `BusinessObjectTransactionDraft` per resolved recipient.
- Disallowed shallow implementation: A display-only recipient list that submits one draft.
- Failing-first test: N/A process exemption; source and component assertions prove draft counts.
- Passing test: `bundle://proof/SB02/transcripts/dotnet-test-passing.txt`.
- Changed source files: `repo://src/CanDoItAll.Economy.Components/Models/BusinessObjectTransactionDraft.cs` hash `35b6e2ce3067b2e5853664558a8970d4e24c403a5484f5fab2eccf2b26b052db`.
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions.txt`.
- Red-team negative case: Manual single recipient is repeated by count and subaccount mode asserts all owner addresses.
- Downstream dependency check: SB03 used manual Bob recipient mode for five proof tickets.

- Invariant ID: `MM-BATCH-03`
- Source raw note: User previously found duplicate BO identity bugs and required uniqueness per transaction.
- Expected behavior: Each generated draft has a unique `BusinessObjectId`.
- Disallowed shallow implementation: Name-only, timestamp-only, or shared object id generation across the batch.
- Failing-first test: N/A process exemption; uniqueness is asserted in component and browser proof.
- Passing test: `bundle://proof/SB02/transcripts/dotnet-test-passing.txt`.
- Changed source files: `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` hash `bf3db3e699c8c3e2d159f398b5ad856eaeb44077a61c9808f726f1c202017f2e`.
- Production assertions: Batch builder calls the existing unique create-id factory per ticket draft.
- Red-team negative case: Duplicate ids would fail the distinct-id assertions in unit and browser proof.
- Downstream dependency check: SB03 verified five distinct ticket object ids.

- Invariant ID: `MM-BATCH-04`
- Source raw note: User asked for practical VEFramework-style ticket multiminting.
- Expected behavior: Each generated ticket receives a sequential ticket number and batch metadata.
- Disallowed shallow implementation: Identical payloads for all generated tickets.
- Failing-first test: N/A process exemption; sequential tickets are asserted in unit and browser proof.
- Passing test: `bundle://proof/SB02/transcripts/dotnet-test-passing.txt`.
- Changed source files: `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` hash `bf3db3e699c8c3e2d159f398b5ad856eaeb44077a61c9808f726f1c202017f2e`.
- Production assertions: Ticket fields include `ticketNumber`, `batchIndex`, and `batchSize`.
- Red-team negative case: Reused ticket numbers would fail `MM-...-001` through `MM-...-005` proof.
- Downstream dependency check: SB03 JSON result shows five sequential ticket numbers.

- Invariant ID: `MM-BATCH-05`
- Source raw note: AccountApp must keep browser signing/API-only behavior.
- Expected behavior: Batch submission uses existing adapter submit paths with one final refresh and summary notification.
- Disallowed shallow implementation: Direct node helper, alternate signing, or hidden broadcast path.
- Failing-first test: N/A process exemption; adapter host test proves the production event path.
- Passing test: `bundle://proof/SB02/transcripts/dotnet-test-passing.txt`.
- Changed source files: `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspaceAdapterHost.razor` hash `eaa9958761fe8450cd47c2c07fed981fc320f02295c967f79891461e2f8961b9`.
- Production assertions: `SubmitTransactionBatchAsync` sequentially calls `Adapter.SubmitTransactionAsync`.
- Red-team negative case: Extra refresh or bypassed adapter would fail `AdapterHost_FullShellSubmitsTicketMultimintBatchWithSingleFinalRefresh`.
- Downstream dependency check: SB03 AccountApp proof submitted through the browser app.

- Invariant ID: `MM-BATCH-06`
- Source raw note: SSR and WASM should stay aligned.
- Expected behavior: Create owner semantics honor `draft.OwnerAddress` in WASM and SSR-facing sessions.
- Disallowed shallow implementation: Always assigning newly created BOs to the current signing account.
- Failing-first test: N/A process exemption; source assertion and identity tests cover owner alignment.
- Passing test: `bundle://proof/SB02/transcripts/dotnet-test-passing.txt`.
- Changed source files: `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs` hash `6766192d56b4e622dee1eaeb50a29f79e9a3df57285cdf735820f329f8fff65f`.
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions.txt`.
- Red-team negative case: Bob-targeted tickets would not appear under Bob ownership in SB03 if owner alignment failed.
- Downstream dependency check: SB03 API proof found all five tickets under Bob.
