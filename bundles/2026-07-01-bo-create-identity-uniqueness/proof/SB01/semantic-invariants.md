# SB01 Semantic Invariants

| Invariant | Proof |
|---|---|
| The reported error is not proof that transaction hashes are duplicated. | `proof/SB01/transcripts/01-root-cause.txt` shows the transition guard operates on `BusinessObjectId`. |
| The ledger should still reject a second Create for the same current object id. | `BusinessObjectTransitionValidator` source review and `LedgerCriticalInvariantTests` in `proof/SB02/transcripts/01-focused-tests.txt`. |
| User-facing metadata can repeat across records. | VEFramework local source shows `Name`, `Description`, and `Text` as metadata; wiki reference maps NFT creation to a unique UTXO/output lineage. |
