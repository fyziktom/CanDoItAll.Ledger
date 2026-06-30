# Source Artifacts

## Required Local Sources

- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectDataItems.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectCasting.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectIpfsStorage.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectDataItemStorageService.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/ServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerGallery.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Rendering/DefaultBusinessObjectRendererRegistry.cs`
- `repo://examples/CanDoItAll.Economy.AccountApp/Program.cs`
- `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppOptions.cs`
- `repo://docker/accountapp/nginx.conf`
- `repo://docker/accountapp/docker-entrypoint-accountapp.sh`
- `repo://docker-compose.yml`

## External Local Analysis Sources

- `C:/repositories/CanDoItAll.IPFS/docker-compose.yml` as local context for the docker stack and ports.
- `C:/repositories/CanDoItAll.IPFS/src/CanDoItAll.IPFS.Engine/HttpApi/V0/FileSystemController.cs` as local context for IPFS add/cat API behavior.
- `C:/repositories/VirtualEconomyFramework/VirtualEconomyFramework/VEDriversLite/NFT` as local context for NFT type inventory and metadata shape.
- `C:/repositories/VirtualEconomyFramework/VirtualEconomyFramework/VEBlazor/Components/NFTs` as local context for gallery/detail/action flow.
- `C:/repositories/VirtualEconomyFramework/VirtualEconomyFramework/VEBlazor/Components/Minting` as local context for upload/data-info/finish minting flow.

## Tooling Notes

- `candoitall_components` MCP lookup was attempted for Grid, Stack, FormRow, and gallery/editor layout guidance; transport closed, so local shared component usage is the fallback.
- `playwright` CLI is available through `npx`.
