# SB01 Proof Manifest

| Proof | Artifact | Result |
|---|---|---|
| Root-cause transcript | `proof/SB01/transcripts/01-root-cause.txt` | Collision traced to wizard create ID generation |
| Ledger invariant review | `src/CanDoItAll.Economy.Ledger/BusinessObjectTransitionValidator.cs` | Guard remains correct and unchanged |
| Transaction builder review | `src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs` | Builder forwards request ID; no hash duplication found |
| VEFramework local reference | `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\NFT\CommonNFT.cs` | Name/Text are metadata; Utxo/outpoint data carries identity/tracking |
