# Requirement Traceability

| Requirement | Subbundle | Source Files | Test / Proof |
| --- | --- | --- | --- |
| `REQ-01` | `SB02` | `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`, `repo://src/CanDoItAll.Economy.Components/BusinessObjectCreateIdFactory.cs` | `AccountWorkspaceAdapterTests` privacy assertions |
| `REQ-02` | `SB03` | `repo://src/CanDoItAll.Economy.BusinessObjects/SecureLedgerMessages.cs` | `SecureLedgerMessageTests` raw payload negative assertions |
| `REQ-03` | `SB03` | `SecureLedgerMessages.cs`, `EconomyAccountWorkspaceAdapters.cs` | Alice/Bob send/open/attachment tests |
| `REQ-04` | `SB03` | `SecureLedgerMessages.cs` | Legacy v1 parse/open regression test |
| `REQ-05` | `SB04`, `SB05` | `BusinessObjectPayload.cs`, `InProcessLedgerGateway.cs`, `EconomyLedgerReadModelService.cs`, `TransactionDetailsDialogContent.razor` | Component raw JSON test and browser screenshot |
| `REQ-06` | `SB05` | Transaction list components and `LedgerTransactionOrdering.cs` | Existing ordering tests |
| `REQ-07` | `SB06` | Docker compose services for economy node and account apps | Docker build/up, health, browser/API proof |
| `REQ-08` | Prepared bundle | `bundle://inventories/secure-message-privacy-impact-map.xlsx` | Workbook exists and is referenced by closure report |
