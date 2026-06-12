# Namespace Compatibility Plan

The physical repository split preserves imported `CanDoItAll.Economy.*` namespaces temporarily. This is a compatibility liability, not a statement of current ownership.

## Compatibility Map

| Current namespace/project | Target namespace/project | Public API impact | Migration plan | Owner | Sunset milestone |
| --- | --- | --- | --- | --- | --- |
| `CanDoItAll.Economy.Core` | `CanDoItAll.Ledger.Core` | High: core contracts appear in downstream code. | Add package migration guide, then rename with type forwarding where packages require it. | Ledger namespace migration owner | After two-repo build/test/audit gates pass for two consecutive split releases. |
| `CanDoItAll.Economy.Cryptography` | `CanDoItAll.Ledger.Cryptography` | Medium: direct crypto driver users need imports updated. | Rename after core package plan is accepted. | Ledger namespace migration owner | Same release as core rename or immediately after. |
| `CanDoItAll.Economy.Ledger` | `CanDoItAll.Ledger.Runtime` | High: runtime package identity is public. | Introduce target package identity, document type forwards, then rename source. | Ledger runtime owner | After SDK and node consumers pass compatibility tests. |
| `CanDoItAll.Economy.BusinessObjects` | `CanDoItAll.Ledger.BusinessObjects` | High: object workflow clients may compile against it. | Publish consumer migration notes and test package compatibility. | Ledger API owner | After external client inventory is complete. |
| `CanDoItAll.Economy.Sdk` | `CanDoItAll.Ledger.Sdk` | High: client-facing package and namespace. | Create package alias or migration facade before source rename. | Ledger SDK owner | After runtime rename plan is complete. |
| `CanDoItAll.Economy.Node` | `CanDoItAll.Ledger.Node` | Medium: host startup and appsettings references change. | Rename host after local and AppHost smoke tests are stable. | Ledger node owner | After node smoke is part of the regular validation lane. |
| `CanDoItAll.Economy.Persistence.EFCore` | `CanDoItAll.Ledger.Persistence.EFCore` | Medium: persistence registrations and migrations move names. | Rename with migration assembly guidance and compatibility tests. | Ledger persistence owner | After database migration strategy is documented. |
| `CanDoItAll.Economy.Persistence.PostgreSql` | `CanDoItAll.Ledger.Persistence.PostgreSql` | Medium: registration package and migrations are visible. | Rename with EF Core persistence lane. | Ledger persistence owner | Same milestone as EF Core persistence. |
| `CanDoItAll.Economy.Components` | `CanDoItAll.Ledger.Components` | Medium: UI consumers need imports updated. | Rename only after shared component dependency direction is verified. | Ledger UI owner | After UI package consumers are inventoried. |
| `CanDoItAll.Economy.Simulation.Abstractions` | `CanDoItAll.Ledger.Simulation.Abstractions` or neutral package | Medium: used only for ledger-backed simulation contracts. | Keep only if independent from the separated simulator lane; otherwise split to a neutral helper package. | Ledger simulation owner | After ledger-backed simulation ownership decision is reviewed. |
| `CanDoItAll.Economy.Simulation.Ledger` | `CanDoItAll.Ledger.Simulation` | Medium: adapter namespace rename. | Rename after simulation abstractions target is chosen. | Ledger simulation owner | Same milestone as simulation abstractions. |
| `CanDoItAll.Economy.Storage.InMemory` | `CanDoItAll.Ledger.Storage.InMemory` | Low: mostly local tests and demo runtime. | Rename after runtime/core rename. | Ledger storage owner | After runtime rename. |
| `CanDoItAll.Economy.Cli` | `CanDoItAll.Ledger.Cli` | Medium: executable and docs rename. | Rename executable after SDK/runtime target names stabilize. | Ledger CLI owner | After SDK rename. |

## New-Code Guidance

- New internal Ledger-only types should use `CanDoItAll.Ledger.*` where they do not extend an existing compatibility namespace.
- New public APIs inside compatibility projects may stay in the existing namespace only when required to avoid mixed public API shape during the migration window.
- New docs must describe compatibility namespaces as temporary shims with owner, cost, and sunset condition.
- New code must not use compatibility names to imply ownership of the separated Economy simulator lane.

## Optional Neutral Helper Rule

Ledger may reference a neutral helper package only when the package has no simulator host, scenario-pack, transaction runtime, persistence, WebGL, UI, or node dependency. The dependency must document owner, expected benefit, complexity delta, validation cost, enforcement cost, simplification potential, and retirement condition.
