# SB06 Semantic Invariants

- Invariant ID: `SB06-DOCKER-WASM-E2E`
- Source raw note: The secure-message privacy changes must work in rebuilt Docker WASM apps with Alice and Bob sending messages in both directions, with and without attachments.
- Expected behavior: Docker images build, services start, the node health endpoint responds, Alice/Bob backups import, secure messages confirm on-chain, and UI opens decrypted messages on large screen.
- Disallowed shallow implementation: Local unit-only success without rebuilt containers, browser proof, and API raw transaction inspection is not acceptable.
- Failing-first test: N/A process exemption; Docker validation ran after implementation and included adversarial raw transaction scans.
- Passing test: `bundle://proof/SB06/transcripts/docker-browser-validation.txt`
- Changed source files: `repo://src/CanDoItAll.Economy.BusinessObjects/SecureLedgerMessages.cs`, `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`, `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionDetailsDialogContent.razor`
- Production assertions: Browser/API proof found confirmed message txids, empty public attachment arrays, object-valued nested data, and no forbidden plaintext values.
- Red-team negative case: API scan checks subjects, bodies, attachment file metadata, IPFS route text, and attachment plaintext against new raw transactions.
- Downstream dependency check: Rebuilt AccountApp Alice/Bob/Lab and Economy Node services were started and queried.
