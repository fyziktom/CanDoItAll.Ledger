# SB01 Root Cause And Reference Model

## Status

- `Complete`

## Objective

Prove whether the duplicate error comes from ledger validation, name checking, or transaction assembly.

## Success Criteria

- The exact current code path that reuses the object ID is documented.
- VEFramework/NFT reference behavior is mapped to BO identity requirements.
- The progression gate states whether the ledger invariant should remain.

## Exact Source References

- `C:\repositories\CanDoItAll.Ledger\src\CanDoItAll.Economy.Ledger\BusinessObjectTransitionValidator.cs`
- `C:\repositories\CanDoItAll.Ledger\src\CanDoItAll.Economy.Components\Components\BusinessObjects\BusinessObjectCreationWizard.razor`
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\NFT\CommonNFT.cs`
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\NFT\NFTFactory.cs`

## Proof Required

- Root-cause transcript in `proof/SB01/transcripts/01-root-cause.txt`.

## Progression Gate

Proceed only if the collision is traced to create-time ID generation and not to transaction hash generation.
