# Economy Simulator Viewport Policy

The Economy simulator sandbox targets large-screen exploratory and evidence workflows only. Browser evidence for this sandbox must use a viewport at least `1440x1100`.

Small, medium, narrow, and mobile layouts are out of scope for this simulator path. They should not be requested, captured, or used as evidence for simulator closure. Future work that needs mobile economics viewing should create a separate product surface with its own requirements and evidence gates instead of widening this sandbox.

The executable policy lives in `EconomySimulatorViewportPolicy` and rejects legacy evidence keys such as `mobileViewport`, `smallViewport`, `mediumViewport`, `narrowViewport`, and `mobileAssertions`. It also rejects screenshot filenames tagged with mobile, small, medium, or narrow viewport tokens.

Evidence artifacts should record:

- policy id: `economy-simulator-sandbox-large-screen-only/v1`
- evidence mode: `large-screen-only`
- minimum viewport: `1440x1100`
- evidence viewport: `1440x1100` or larger
- GLB evidence: positive GLB request/model diagnostics, zero failed GLB requests, zero fallback objects, screenshots, and machine runtime snapshots

This policy is intentionally scoped to the Economy simulator sandbox. It does not create a global UI rule for other Economy, Node, ledger, account, SDK, or component surfaces.

The large-screen screenshot is observer evidence only. It must not replace headless scenario, invariant, oracle, manifest, or artifact-hash evidence.
