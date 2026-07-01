# Current State

## Repository Observations

- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs` contains the typed BO family catalog and `BusinessObjectPayloadBuilder`. `CreatePayload(...)` accepts `text` and serializes it into `BusinessObjectMediaPayload.Text`, and `GenericTypedBusinessObjectView.Text` is populated from parsed payload text.
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` includes a Long description `TextArea` bound to a local `text` field and passes that into `BusinessObjectPayloadBuilder.CreatePayload`. The current problem may be component binding, stale model reset, or display/raw-state visibility; implementation must prove real round-trip.
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor` currently renders the short `Description` in overview and only falls back to `Payload.Text` when description is empty. Long body has no dedicated visible section, so long descriptions can appear missing even when serialized.
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectSetPriceDialogContent.razor` only captures price amount and asset. It emits `BusinessObjectPriceRequest` with no sale mode, max clone count, share link, or idempotency metadata.
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs` is the browser-signing adapter. It already supports workspace loading, cached transaction details, node queries, create/update/transfer/clone transactions, price update, and refresh snapshots.
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs` mirrors several adapter operations for SSR/node signing, but its price update currently wraps prior state as string under `previous` and `price`.
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs` supports BO create, transfer, update, clone, split payment, and set price. Existing builders can support commerce as a chain of signed transactions without changing ledger consensus rules.
- `repo://src/CanDoItAll.Economy.Sdk/EconomyNodeLedgerClient.cs` and `repo://src/CanDoItAll.Economy.Node/EconomyNodeApiEndpoints.cs` already expose query by object id and transaction query endpoints needed by a Buy page.
- `repo://src/CanDoItAll.Economy.Persistence.EFCore/EfBusinessObjectProjectionMapper.cs` shows that a transaction has one payload envelope. For multi-BO transactions, an updated BO can retain its previous state when the transaction data hash does not match its output; new BO creates consume the transaction envelope. Commerce design must avoid needing two new independent BO states in one transaction.
- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspaceAdapterHost.razor` already has a 30-second refresh loop and notification service; dispatch automation should run from this host/adapter path after workspace refresh, without rendering notification text as page content.

## VEFramework Flow Observations

- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Components\NFTs\Actions\SetNFTPrice.razor` sets price and `SellJustCopy` through an owner self-send/update.
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Pages\BuyNFT.razor` loads NFT data by shared link, prompts unlock when local key exists, validates sale state, and sends an NFT payment to the seller.
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\NFT\CommonNFT.cs` stores `Price`, `PriceActive`, `SellJustCopy`, `SoldInfo`, and long `Text` in metadata.
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\NFT\PaymentNFT.cs` records sold NFT UTXO/index, buyer/sender, returned/already-sold flags, and payment transaction id.
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\NFT\ReceiptNFT.cs` records buyer, seller/sender, original NFT UTXO/index, payment UTXO, and sold price.
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\Neblio\NeblioAccountBase.cs` refreshes NFT payments, checks payment amount, dispatches original or minted copy, and refreshes payments/receipts.

## Component Gap Table

| Component | Current behavior | Gap |
| --- | --- | --- |
| Post wizard/details | `Text` exists in payload model, but details hide it behind short description. | Prove capture and render long description as first-class content and raw JSON. |
| Family catalog | Payment/purchase/receipt are generic families. | Need commerce-specific structured sale, payment, purchase, receipt contracts and parser helpers. |
| Price dialog | Simple price update only. | Need sale mode, max copies, clear sale, and share/buy link copy. |
| Gallery/details | Actions include Set Price and Send. | Need sale badge, buy link, related payment/receipt click-through. |
| Buy page | No AccountApp buy route. | Need route that queries node by object id and creates signed payment BO. |
| Refresh loop | Refreshes workspace when transaction probe changes. | Need seller-side commerce dispatcher after refresh and idempotent result handling. |
| Tests | Existing account/workspace and BO transaction tests. | Need commerce lifecycle and browser validation coverage. |
