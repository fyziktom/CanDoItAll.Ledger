# Requirement Traceability

| Raw note | Exact wording | Requirements | Owner | Planned proof | Closure |
| --- | --- | --- | --- | --- | --- |
| N001 | `remove from tags Neblio. this has nothing to do with neblio.` | RQ01 | SB01 | Source diff and source search transcript. | Pending |
| N002 | `messaging works if it is possible to find on api some transaction of receiver that will contains receiver public key` | RQ03, RQ04 | SB02 | Unit tests for discovery from real transaction summaries and negative no-key/mismatched-key cases. | Pending |
| N003 | `assure that signed tx contains signer pubkey` | RQ02 | SB02 | Transaction signing tests and source assertions for `PublicKeyMaterial`. | Pending |
| N004 | `message BO content must be encrypted with shared secret generated with ECDH` | RQ05, RQ06 | SB02, SB03 | Browser/.NET ECDH compatibility proof; encrypted BO state tests. | Pending |
| N005 | `receiver ... has public key of sender (directly in tx) and own private key to calc hash and encrypt the data` | RQ02, RQ05, RQ06 | SB02, SB03 | Receiver decrypt tests using sender public key from transaction signature and receiver private key. | Pending |
| N006 | `Same loaded content to ipfs must be encrypted same way.` | RQ07 | SB03, SB05 | Attachment encryption tests and Docker IPFS plaintext-negative proof. | Pending |
| N007 | `UI simplified so people understand it as messaging ... email/chat style` | RQ11, RQ12 | SB04 | Playwright screenshots and visual review. | Pending |
| N008 | `It cannot decrypt all messages` | RQ08, RQ09 | SB03, SB04 | Lazy-decrypt service/component tests and browser proof. | Pending |
| N009 | `if user open message then it is decrypted` | RQ09 | SB03, SB04 | Component and Playwright open-message proof. | Pending |
| N010 | `attachments are decrypted also only on request` | RQ10 | SB03, SB04, SB05 | Component and Docker attachment decrypt-on-request proof. | Pending |
| N011 | `use ... imagegen ... create proposals of messaging UI` | RQ11 | SB04 | UI proposal images saved under `bundle://inputs/ui-proposals/`; not acceptance proof. | Pending |
| N012 | `deeply analyze our implementation of EDCH and solve it for wasm via webcrypto browser api` | RQ05, RQ15 | SB02 | Current-state analysis, WebCrypto implementation, browser harness proof. | Pending |
| N013 | `must not just somehow "hack" it ... fully working and secure p2p e2e encrypted communication` | RQ02-RQ07, RQ15 | SB02, SB03, SB05 | Semantic adequacy manifests, negative tests, anti-stub audits, Docker E2E. | Pending |
| N014 | `validate ... docker wasm apps ... bob and alice and back. with and without attachments` | RQ13, RQ14 | SB05 | Docker rebuild/restart/smoke and Playwright Alice/Bob proof. | Pending |
