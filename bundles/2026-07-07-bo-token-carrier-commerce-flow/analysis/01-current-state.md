# Current State

## New Ledger

- `BusinessObjectTransactionBuilder.BuildUnsignedCreateAsync` already supports a `TokenTransfer` on the BO output and returns token-source change.
- `BusinessObjectCreationWizard` initializes `selectedTokenKey` to `Main`, so the first reusable token source is not selected by default.
- WASM `WasmAccountWorkspaceAdapter.GetWorkspaceAsync` creates its starter BO draft without token fields. The node session already uses the first reusable source token for its starter draft.
- `BuyBo.razor` creates payment BO drafts with main payment amount but no token.
- `BusinessObjectCommerceDispatcher` creates receipt and purchase BO drafts with no token.
- Copy-sale dispatch creates clone drafts with no token; `CloneBusinessObjectRequest` has no token/source fields.
- Closing a token-backed BO already returns the selected BO token on a non-BO output, which is the current destroy path.

## VEFramework Reference

- `NFTHelpers.GetNFTPaymentData` creates payment NFTs with `Amount = 1`, sends the buyer payment amount in the same transaction, and directs the token output to the seller.
- `PaymentNFT` records original NFT UTXO/index, sender, original payment transaction id, and match/return status.
- `ReceiptNFT` records original NFT UTXO/index, buyer, seller, original payment tx, payment UTXO, and sold price.
- `NFTHelpers.GetTxDataForOrderedNFT` consumes both the sold NFT UTXO and payment NFT UTXO when dispatching an original sale.
- `NFTHelpers.GetTokenTxDataCopy` consumes the payment UTXO and mints/copies one token-backed artifact for the buyer.
- `AccountSider.razor` exposes separate `NFTPaymentsList` and `NFTReceiptsList` tabs on the account home surface.

## CodeAnalytics

The codeanalytics snapshot attempt failed before analysis because its MSBuild host resolved sibling `C:\repositories\CanDoItAll\global.json`, requiring SDK `10.0.200` while installed SDKs include `10.0.300`. The local repo `global.json` uses feature roll-forward, so CLI validation remains the primary proof path unless the MCP host SDK resolution is repaired.

