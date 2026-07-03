# SB04 Semantic Invariants

- Invariant ID: `SB04-ENVELOPE-COMPATIBILITY`
- Source raw note: Raw tx display must not show escaped nested JSON, and BO readers must remain compatible with old and new envelope shapes.
- Expected behavior: New secure messages write object-valued `stateJson`; shared readers accept legacy string `stateJson`, object `stateJson`, and `state` alias inputs.
- Disallowed shallow implementation: Pretty-printing only in the raw transaction tab while read-model and in-process BO readers diverge is not acceptable.
- Failing-first test: N/A process exemption; compatibility tests now cover all supported envelope shapes.
- Passing test: `bundle://proof/SB04/transcripts/envelope-compatibility-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectPayload.cs`, `repo://src/CanDoItAll.Economy.BusinessObjects/InProcessLedgerGateway.cs`, `repo://src/CanDoItAll.Economy.Node/EconomyLedgerReadModelService.cs`, `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionDetailsDialogContent.razor`
- Production assertions: Tests parse legacy string, object-valued, and alias envelopes; component tests assert raw tab output has structured nested data and no escaped secure-message state.
- Red-team negative case: Legacy escaped `stateJson` input remains supported so existing transactions do not disappear from BO views.
- Downstream dependency check: AccountApp build and raw transaction component tests cover the shared BO projection/display path.
