# C# Dependency Direction

## Current Project References

- `CanDoItAll.Economy.Components` references:
  - `CanDoItAll.Economy.BusinessObjects`
  - `CanDoItAll.Economy.Cryptography`
  - `CanDoItAll.Economy.Ledger`
  - `CanDoItAll.Economy.Sdk`
  - BaseLib/Charts/Mermaid/QRCode/WebGL component packages or sibling projects.

## Target Project References

- No new project references.
- Tests continue referencing components and business objects.

## Forbidden References

- `CanDoItAll.Economy.BusinessObjects` must not reference `CanDoItAll.Economy.Components`.
- Renderer components must not introduce node/API/persistence dependencies.
- UI components must not call external network services for layout decisions.

## Cycle Risk

Low. Work stays within existing UI project and tests.

## New Contract Projects Needed

None.

## Build/Test Proof Required

- `dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj --no-restore --filter "FullyQualifiedName~BusinessObjectDetailRendererLayoutTests|FullyQualifiedName~BusinessObjectCommerceUiComponentTests|FullyQualifiedName~BusinessObjectLongDescriptionComponentTests|FullyQualifiedName~BusinessObjectTicketMintingComponentTests"`
- `dotnet build examples/CanDoItAll.Economy.AccountApp/CanDoItAll.Economy.AccountApp.csproj --no-restore`
- Docker compose build for app/node services.

