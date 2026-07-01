# Source Artifacts

| Artifact | Location | Notes |
| --- | --- | --- |
| Raw user request | `bundle://inputs/00-original-request.md` | Primary source for RN-01 through RN-07. |
| Existing AccountApp home | `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` | Hosts the unlocked AccountApp workspace and imports. |
| WASM account adapter | `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs` | Browser-signing workspace, transaction submission, refresh snapshot, cache. |
| SSR/node adapter | `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs` | Server-signing parity path and current price update JSON wrapper. |
| BO family and parser catalog | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs` | Existing typed BO families, media payload parser, generic caster. |
| BO create wizard | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` | Current long-description input and stage transaction path. |
| BO details dialog | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor` | Current details/raw-state rendering and action buttons. |
| BO price dialog | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectSetPriceDialogContent.razor` | Current price-only self-update dialog. |
| Ledger transaction builder | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs` | Existing create, transfer, clone, update, split, set-price transaction builders. |
| Node query APIs | `repo://src/CanDoItAll.Economy.Node/EconomyNodeApiEndpoints.cs` | BO and transaction query endpoints used by Buy page. |
| SDK node client | `repo://src/CanDoItAll.Economy.Sdk/EconomyNodeLedgerClient.cs` | Browser node API client for BO/transaction queries and broadcast. |
| EF BO projection | `repo://src/CanDoItAll.Economy.Persistence.EFCore/EfBusinessObjectProjectionMapper.cs` | Shows one transaction data payload and current-state projection rules. |
| VEFramework buy page | `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Pages\BuyNFT.razor` | UX source for shared buy link, login/unlock, payment submit, success state. |
| VEFramework set price | `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Components\NFTs\Actions\SetNFTPrice.razor` | UX source for price and sell-copy toggle. |
| VEFramework NFT action control | `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Components\NFTs\Actions\NFTActionControl.razor` | UX source for details actions. |
| VEFramework share NFT | `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Components\NFTs\Actions\ShareNFT.razor` | UX source for share links. |
| VEFramework common NFT model | `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\NFT\CommonNFT.cs` | Price, sell-copy, sold info, long text, metadata mapping. |
| VEFramework payment NFT | `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\NFT\PaymentNFT.cs` | Payment record fields. |
| VEFramework receipt NFT | `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\NFT\ReceiptNFT.cs` | Receipt record fields. |
| VEFramework dispatch logic | `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\Neblio\NeblioAccountBase.cs` | Payment discovery and dispatch/cloning behavior to remap cleanly. |
