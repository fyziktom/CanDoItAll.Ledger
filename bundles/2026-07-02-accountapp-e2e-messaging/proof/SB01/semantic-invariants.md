# SB01 Semantic Invariants

## SB01-INV-01 - Production Tags Exclude Neblio

- Raw note: `remove from tags Neblio. this has nothing to do with neblio.`
- Expected behavior: production default BO tag strings omit `Neblio`.
- Disallowed shallow implementation: hiding `Neblio` only in one screen while another creation default still inserts it.
- Failing-first proof: HEAD default tag strings included `#VEFramework #Neblio #NET #Blazor #OpenSource` in the changed files recorded by `bundle://proof/SB01/changed-file-hashes.json`.
- Passing proof: `bundle://proof/SB01/transcripts/source-search-tags.txt`.
- Changed source files: `repo://src/CanDoItAll.Economy.Components/Components/VenftSsrEconomyApp.razor`; `repo://src/CanDoItAll.Economy.Components/Components/VenftBusinessObjectApp.razor`.
- Production assertions: source search covers `src`, `examples`, `tools`, and `tests` excluding build/generated library output.
- Red-team negative case: a lingering production `Neblio` match would fail the source-search transcript.
- Downstream dependency check: SB02 can proceed because tag cleanup no longer contaminates new messaging BO defaults.

## SB01-INV-02 - Signer Public Key Baseline Exists

- Raw note: `assure that signed tx contains signer pubkey`.
- Expected behavior: signed transactions carry signer public key material and ledger validation rejects missing or mismatched material.
- Disallowed shallow implementation: relying on user-entered public keys or accepting signatures without public key material.
- Failing-first proof: not executed in SB01; SB01 records baseline source assertions for SB02 tests.
- Passing proof: `bundle://proof/SB01/transcripts/source-assertions.txt` contains `SB01-INV-01` source assertion checks for signature public key material.
- Changed source files: none for this invariant in SB01.
- Production assertions: `SignatureEnvelope.PublicKeyMaterial`, `CryptoDriverAccountInputSigner`, and `LedgerExecutor.ValidateSignatureEnvelope`.
- Red-team negative case: missing/mismatched key material rejection paths are present in production source.
- Downstream dependency check: SB02 must add executable tests around this baseline before public-key discovery closes.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `DefaultBusinessObjectTags` | `repo://src/CanDoItAll.Economy.Components/Components/VenftSsrEconomyApp.razor`; `repo://src/CanDoItAll.Economy.Components/Components/VenftBusinessObjectApp.razor` | `bundle://proof/SB01/transcripts/source-search-tags.txt` | Runtime creation wizard model construction | `bundle://proof/SB01/transcripts/source-search-tags.txt` |
| `SignedTransactionPublicKeyMaterial` | `repo://src/CanDoItAll.Economy.BusinessObjects/CryptoDriverAccountInputSigner.cs` | `repo://src/CanDoItAll.Economy.Ledger/LedgerExecutor.cs` | Browser transaction signing and ledger submission path | `bundle://proof/SB01/transcripts/source-assertions.txt` |
