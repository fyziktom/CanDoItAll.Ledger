# Normalized Requirements

| ID | Requirement | Acceptance Evidence |
| --- | --- | --- |
| `REQ-01` | New secure-message BO IDs must be opaque after the `bo:secure-message:` type prefix and must not include subject/name text. | Unit/integration test inspects transaction output object id and external ref. |
| `REQ-02` | New secure-message public payload must not contain attachment DataItem hash, URI, file name, description, size, or content type. | Red-team test searches raw tx JSON for attachment metadata and CID. |
| `REQ-03` | Message subject/body and attachment metadata/locator must be available only after decrypting message content. | Alice/Bob open-message and open-attachment tests pass. |
| `REQ-04` | Existing v1 secure messages with public attachment records must still parse and open. | Backward-compatibility unit test uses v1 payload shape. |
| `REQ-05` | Raw tx tab must expand nested JSON state for inspection and avoid `stateJson` escape noise where parsing succeeds. | Component test and browser screenshot on raw tx tab. |
| `REQ-06` | Ledger transaction history must keep newest-to-oldest ordering after messaging changes. | Existing transaction ordering tests continue to pass. |
| `REQ-07` | Docker WASM account apps must rebuild/restart and support Alice-to-Bob and Bob-to-Alice secure messages with and without attachments. | Docker compose build/up, API health, and browser/API validation proof. |
| `REQ-08` | Affected BO/transaction surfaces must be mapped in XLSX with checklist status and references. | `inventories/secure-message-privacy-impact-map.xlsx` exists and matches bundle traceability. |
