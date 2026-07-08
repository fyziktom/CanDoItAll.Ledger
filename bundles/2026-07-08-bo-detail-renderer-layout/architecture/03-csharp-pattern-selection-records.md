# C# Pattern Selection Records

## Pattern Selection Record: Shared Gallery Card Component

### Context

`BusinessObjectLedgerGallery` owns the gallery card markup. The detail dialog needs the same card for related BO previews.

### Forces

- extension growth: more detail surfaces will need BO preview cards.
- multiple implementations: duplicate cards would drift.
- construction complexity: low.
- external SDK isolation: none.
- runtime selection: none.
- testability: bUnit can assert a single component renders expected card fields.
- dependency direction: UI-only component depends on domain interfaces.

### Selected pattern

Simple extracted component.

### Rejected alternatives

- simpler class: helper methods alone would not share markup.
- partial class: blocked; would hide component growth.
- switch statement: not relevant.
- service locator: not needed.
- direct duplication: rejected because it would not satisfy "same card".

### New types and projects

| Type | Project | Responsibility |
|---|---|---|
| `BusinessObjectGalleryCard` | `CanDoItAll.Economy.Components` | Render one BO card with thumbnail/icon, badges, facts, tags, meta, and open action. |

### Test plan

| Test | Behavior proven |
|---|---|
| Gallery card component test | Related detail card and gallery card share the component surface. |
| Existing gallery tests | Ordering/filtering/hidden artifacts remain intact. |

## Pattern Selection Record: Renderer Provider Strategy

### Context

Default component renderers currently fall back from rule/settings to generic media/generic behavior. Event/ticket/commerce BOs need specific display affordances without changing external app renderer registrations.

### Forces

- extension growth: new BO families should be handled by adding providers/components, not rewriting the host.
- multiple implementations: rule, settings, media, typed family, commerce, generic.
- construction complexity: low; descriptors already exist.
- external SDK isolation: none.
- runtime selection: registry already selects providers by priority.
- testability: provider resolution can be unit-tested.
- dependency direction: provider components remain UI-only.

### Selected pattern

Strategy through existing `IBusinessObjectRendererProvider`.

### Rejected alternatives

- simpler class: not enough because the registry already models renderer selection.
- partial class: blocked.
- switch statement in renderer host: rejected because it centralizes type growth in the host.
- service locator: registry already resolves providers through DI.
- direct construction: would bypass external custom providers.

### New types and projects

| Type | Project | Responsibility |
|---|---|---|
| `CommerceBusinessObjectRendererProvider` | `CanDoItAll.Economy.Components` | Select commerce renderer for payment/purchase/receipt views. |
| `TypedFamilyBusinessObjectRendererProvider` | `CanDoItAll.Economy.Components` | Select typed family renderer for event/ticket/etc. |
| `CommerceBusinessObjectRenderer` | `CanDoItAll.Economy.Components` | Render commerce facts and relationship fields. |
| `TypedFamilyBusinessObjectRenderer` | `CanDoItAll.Economy.Components` | Render family facts and text without media-specific assumptions. |

### Test plan

| Test | Behavior proven |
|---|---|
| Provider resolution tests | Commerce and typed family providers win before `MediaBusinessObjectRendererProvider`. |
| Detail component tests | Specific BO types get usable no-DataItems layouts. |

## Proof That This Is Not Fake Separation

- Gallery card markup moves out of `BusinessObjectLedgerGallery`.
- Renderer selection remains provider-based, not a new switch in `BusinessObjectRendererHost`.
- Tests instantiate extracted components/providers directly and render the detail dialog without full app construction.

