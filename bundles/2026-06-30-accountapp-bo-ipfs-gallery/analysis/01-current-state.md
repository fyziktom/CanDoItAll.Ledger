# Current State

## Ledger BO Model

- `repo://src/CanDoItAll.Economy.Core/BusinessObjects.cs` stores object type, schema version, state JSON, data hash, owner, current outpoint, and version lineage.
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectPayload.cs` envelopes BO type, schema version, and state JSON into transaction data.
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectCasting.cs` currently provides `GenericBusinessObjectView`, `AccountSettingsBusinessObjectView`, and media-specific image/post views.
- `repo://src/CanDoItAll.Economy.BusinessObjects/ServiceCollectionExtensions.cs` registers settings, image, post, and generic casters only.

## IPFS/Attachment State

- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectDataItems.cs` already models data items with `Inline`, `Ipfs`, and `Url` storage plus image/media/pdf/json/html/mermaid/text detection.
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectIpfsStorage.cs` has an HTTP IPFS driver behind `#if BUSINESS_OBJECT_IPFS`.
- `repo://examples/CanDoItAll.Economy.AccountApp/CanDoItAll.Economy.AccountApp.csproj` and `repo://docker/accountapp/Dockerfile` explicitly build the WASM app with `EnableBusinessObjectIpfsStorage=false`, so the AccountApp does not currently register `IBusinessObjectDataItemUploadService`.
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` already asks DI for `IBusinessObjectDataItemUploadService`; in WASM this path reports missing IPFS storage.
- `C:/repositories/CanDoItAll.IPFS/docker-compose.yml` exposes `ipfs-node` on port 5001 and `node-control` on port 5093 with persisted named volumes.

## UI State

- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` already follows the VEFramework minting shape: upload, data info, finish.
- The data info inputs currently use `Value` without model update callbacks, so typed user input risk must be checked before browser proof.
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerGallery.razor` parses only `BusinessObjectMediaPayload` and falls back to generic object id/empty descriptions for typed non-media records.
- `repo://src/CanDoItAll.Economy.Components/Rendering/DefaultBusinessObjectRendererRegistry.cs` uses settings/rule/media/generic renderer providers, so new generic families need reusable rendering instead of one-off branches.

## VEFramework Analysis

- VEFramework generic NFT drivers include `PostNFT`, `ImageNFT`, `EventNFT`, `TicketNFT`, `PaymentNFT`, `ReceiptNFT`, `InvoiceNFT`, `OrderNFT`, `ProductNFT`, `MessageNFT`, `ProfileNFT`, and `MusicNFT`.
- VEFramework minting components separate upload, data-item metadata, core info, template loading, and finish actions.
- VEFramework NFT list/detail components provide practical flows for cards, preview images, details dialogs, send, set price, payments/receipts, and ticket event data.
- VEFramework also has many domain-specific families that this bundle must not carry forward: IoT, Xray, Coruzant, and device/source-code categories.

## Component MCP Result

- `candoitall_components` MCP was queried for layout primitives and usage examples, but the transport closed.
- Existing local component usage in the wizard/gallery already uses `Grid`, `Stack`, `Cluster`, `FormField`, `Button`, `FileUpload`, `TextBox`, `TextArea`, and dialogs, so implementation should extend those surfaces rather than introduce a second UI stack.
