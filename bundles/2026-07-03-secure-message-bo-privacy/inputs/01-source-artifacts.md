# Source Artifacts

| Artifact | Path / Source | Role |
| --- | --- | --- |
| User raw transaction report | Conversation input on 2026-07-03 | Primary privacy defect report |
| Secure-message builder | `repo://src/CanDoItAll.Economy.BusinessObjects/SecureLedgerMessages.cs` | Current encrypted content and public attachment payload |
| WASM workspace adapter | `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs` | Message send/open path and BO id creation |
| BO payload envelope | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectPayload.cs` | Persisted `stateJson` envelope writer |
| In-memory ledger projection | `repo://src/CanDoItAll.Economy.BusinessObjects/InProcessLedgerGateway.cs` | Local read model for BO holdings |
| Node read model | `repo://src/CanDoItAll.Economy.Node/EconomyLedgerReadModelService.cs` | Docker/node projection reader |
| Raw tx dialog | `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionDetailsDialogContent.razor` | Raw tx structured JSON UI |
| Existing tests | `repo://tests/CanDoItAll.Ledger.Tests/SecureLedgerMessageTests.cs` and `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs` | Messaging/security regression proof |
| Impact workbook | `bundle://inventories/secure-message-privacy-impact-map.xlsx` | Detailed checklist and affected-surface map |
