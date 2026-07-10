# IPFS display URI resolution

## Decision

Resolve business-object data-item addresses at display time through a small, replaceable UI strategy. The AccountApp owns the selected IPFS gateway setting and supplies the strategy to the reusable component tree. Persisted ledger payloads remain unchanged.

## Responsibility inventory

| Responsibility | Current owner | Target owner |
|---|---|---|
| Persist a data item's storage kind, CID, and original URI | `BusinessObjectDataItem` | unchanged |
| Select the URI used by previews and actions | individual Razor components | `IBusinessObjectDataItemUriResolver` |
| Convert an IPFS CID to a path-gateway URI | ad hoc stored URI/fallback logic | `IpfsGatewayDataItemUriResolver` |
| Select the local or remote gateway | upload-time AccountApp configuration only | AccountApp composition root and `AccountApp:IpfsGatewayBaseUri` |
| Make the strategy available to nested reusable components | none | AccountApp root cascading value |

This is a local provider-isolation change in the existing UI project. It does not justify a new project: the contract has one UI-projection method, introduces no SDK or transport dependency, and follows the existing `Components -> BusinessObjects` reference direction.

## Pattern selection record

### Forces

- The gateway must be replaceable between local and remote deployments.
- Components must not read AccountApp configuration or hard-code a public gateway.
- IPFS display addresses must ignore stale persisted host URLs and use the current gateway.
- URL and inline data items must retain their existing URI behavior.
- Tests must exercise address selection without a browser, node, or network.

### Selected pattern

Strategy: `IBusinessObjectDataItemUriResolver` is the stable UI contract and `IpfsGatewayDataItemUriResolver` is the configured path-gateway strategy. A small component base consumes the optional cascaded strategy and preserves stored-URI fallback for component hosts that have not opted in.

### Rejected alternatives

- Mutating or migrating stored ledger payloads: binds immutable content to one deployment and does not support later switching.
- Static global gateway state: introduces hidden process-wide state and makes parallel tests and multi-app hosting unsafe.
- Reading configuration in each component: couples the reusable component project to AccountApp and scatters raw keys.
- A factory or provider catalog: unnecessary for one selected display strategy; DI already owns implementation selection.
- Extending the storage driver: display URI selection performs no I/O and should not acquire upload/read dependencies.

## Dependency direction

```text
AccountApp composition/configuration
  -> Components display strategy
  -> BusinessObjects data-item model and URI helper
```

No new project reference or partial class is introduced. Core and BusinessObjects remain independent from AccountApp and Razor components.

## Testability and acceptance gates

- An isolated unit test proves an IPFS item with a stale stored URL resolves through the configured gateway.
- A negative unit test proves ordinary URL items are not rewritten.
- A component smoke test proves a cascaded resolver changes rendered media links.
- AccountApp composition resolves and cascades the configured strategy.
- Production component code no longer dereferences `BusinessObjectDataItem.Uri` directly for preview/open/download behavior.
- Existing component hosts without a cascaded strategy continue using the stored URI.

## Analysis note

The requested scoped CodeAnalytics snapshot failed before workspace load because its host resolved the sibling `CanDoItAll` `global.json`, which pins .NET SDK `10.0.200`, while that service currently sees `10.0.300`. Repository project files and exact consumers were inspected directly; no SDK pin was changed for this feature.

## C# Architecture Gate Result

Status: Pass with follow-up

### Findings

| Severity | Finding | Evidence | Required action |
|---|---|---|---|
| Informational | The CodeAnalytics host could not produce the requested snapshot because of its SDK-resolution mismatch. | `APP1099` before MSBuild workspace load; exact `.csproj` and source inspection completed instead. | Align the shared analysis host with the repository SDK separately; this feature does not change project references. |

### Dependency direction

Pass. No `.csproj` or project reference changed. AccountApp already references Components, and Components already references BusinessObjects. The new composition follows those directions.

### Responsibility and partial-class policy

Pass. URI selection is isolated in one strategy, the base component only adapts that strategy for Razor consumers, and no partial or nested architecture type was introduced. Stored data, network I/O, and component rendering remain separate responsibilities.

### Testability proof

- Resolver unit tests: 4 passed, including stale-IPFS-address replacement, non-IPFS preservation, invalid configuration, and cascaded component rendering.
- Existing affected BusinessObject component regressions: 30 passed.
- AccountApp WebAssembly build: succeeded with 0 errors.

### Closure decision

The feature may proceed. The SDK mismatch belongs to the shared analysis environment and is not a code or dependency blocker for this change.
