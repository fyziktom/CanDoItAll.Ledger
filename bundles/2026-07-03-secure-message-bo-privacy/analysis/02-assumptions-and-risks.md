# Assumptions And Risks

## Working Assumptions

- Public ledger routing will still expose transaction inputs/outputs, sender signature public key, receiver output address, and BO type.
- Secure-message confidentiality target is message content plus attachment metadata/locator, not hiding that two ledger addresses participated in a transaction.
- Existing v1 messages in local/dev ledgers must remain readable because users may already have test data.
- General public BO ids may remain readable for now; this bundle changes secure-message id creation and documents broader BO naming risk.

## Critical Path Risks

- Moving attachment locators into encrypted content changes the open-attachment flow; tests must cover text-only and attachment messages for both sender and receiver.
- Changing BO envelope writing globally could break projections, rule executor state queries, gallery media parsing, and history views.
- Raw tx UI improvements can accidentally mask persisted data; tests must assert persisted transaction strings, not just rendered JSON.

## Validation Risks

- Unit tests using fake storage may pass while Docker IPFS/API integration fails.
- Identical timestamps can hide transaction ordering defects; history lists must continue ordering by ledger sequence/newest first.
- Browser screenshots can prove layout only if the target transaction is opened and the raw tab is visible.

## Reopen Triggers

- Any new secure-message tx contains subject/body/attachment file name/CID/URI in public raw JSON.
- Alice or Bob cannot decrypt a new attachment after Docker rebuild.
- Existing v1 secure-message fixture cannot be parsed/decrypted.
- BO history or node projections fail to show state after the envelope reader changes.
