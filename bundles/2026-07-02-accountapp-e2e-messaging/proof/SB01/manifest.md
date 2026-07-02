# SB01 Manifest - Tags And Current State

## Status

- Completed

## Owned Requirements And Raw Notes

- N001/RQ01: remove `Neblio` from production default tags.
- Baseline support for N003/RQ02: signed transactions include and validate signer public key material.

## Semantic Invariant Contract

- `bundle://proof/SB01/semantic-invariants.md`

## Changed Files

- `bundle://proof/SB01/changed-file-hashes.json`

## Command Transcripts

- Source tag search: `bundle://proof/SB01/transcripts/source-search-tags.txt`
- Source assertions: `bundle://proof/SB01/transcripts/source-assertions.txt`

## Failing-First Or Adversarial Proof

- Pre-change source state contained `#Neblio` in the default tag strings named in `bundle://proof/SB01/changed-file-hashes.json`.
- Post-change search transcript proves no `Neblio` references remain in production source roots after excluding generated libraries and build output.

## Passing Proof

- `bundle://proof/SB01/transcripts/source-search-tags.txt` records `PASS: no Neblio references found in production source roots src/examples/tools/tests`.
- `bundle://proof/SB01/transcripts/source-assertions.txt` records `SB01-INV-01` public-key material source assertions.

## Source Assertions

- `repo://src/CanDoItAll.Economy.Core/Signatures.cs` keeps `SignatureEnvelope.PublicKeyMaterial`.
- `repo://src/CanDoItAll.Economy.BusinessObjects/CryptoDriverAccountInputSigner.cs` emits `keySource.PublicKeyMaterial.ToArray()`.
- `repo://src/CanDoItAll.Economy.Ledger/LedgerExecutor.cs` rejects missing public key material, public key/address mismatch, and public key hash mismatch.

## Anti-Stub Audit

- SB01 changed only default tag strings. Source assertions prove no placeholder/stub path was used for the public-key baseline.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `DefaultBusinessObjectTags` | `repo://src/CanDoItAll.Economy.Components/Components/VenftSsrEconomyApp.razor`; `repo://src/CanDoItAll.Economy.Components/Components/VenftBusinessObjectApp.razor` | `bundle://proof/SB01/transcripts/source-search-tags.txt` | Wizard default creation model uses these source strings at runtime | `bundle://proof/SB01/transcripts/source-search-tags.txt` rejects lingering production `Neblio` matches |
| `SignedTransactionPublicKeyMaterial` | `repo://src/CanDoItAll.Economy.BusinessObjects/CryptoDriverAccountInputSigner.cs` | `repo://src/CanDoItAll.Economy.Ledger/LedgerExecutor.cs` | Every browser signed transaction passes through account transaction signing and ledger validation | `bundle://proof/SB01/transcripts/source-assertions.txt` cites rejection of missing/mismatched key material |
