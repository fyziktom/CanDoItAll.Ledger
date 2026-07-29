# Assumptions And Risks

## Assumptions

- The request authorizes a breaking source/assembly namespace rename, not an old
  namespace shim.
- `CanDoItAll.Ledger.Engine` is the permanent name for the former
  `CanDoItAll.Economy.Ledger` project.
- Current project roles and reference edges are correct and are renamed without
  architectural redistribution.
- The separate Economy repository and historical split references retain their
  real name.
- Ignored PostgreSQL/key data is user-owned state and will not be deleted.

## Critical Path Risks

- SB01 is the critical foundation. A missed path, assembly name, namespace, EF
  context identity, or project reference invalidates documentation and Docker
  proof.
- A global text replacement can incorrectly rewrite historical evidence,
  sibling-repository boundaries, durable format IDs, or produce doubled Ledger
  segments.
- Renaming Compose service DNS and application configuration in different phases
  can create a build-green/runtime-red result.

## Validation Risks

- CodeAnalytics before/after cycle proof is currently blocked by its SDK-host
  resolution; manual graph comparison plus MSBuild cycle/build proof is the
  required fallback.
- Docker validation needs the local Docker engine, sibling Components and IPFS
  sources, and may take several minutes.
- Existing local Docker data may not match the new named-volume model. Validation
  uses a uniquely named disposable Compose project or a fresh named volume and
  leaves the ignored bind-mount data untouched.
- Browser proof is required only for runtime regression; no UI redesign evidence
  is implied.

## Reopen Triggers

- Reopen SB01 if any active project/namespace/reference has an old identity, any
  `Ledger.Ledger` collision appears, build/test fails because of rename drift, or
  the dependency graph gains/loses an edge.
- Reopen SB02 if current docs/commands point to old paths, the residual guard
  misses a deliberate negative fixture, or SharedInfo validation finds an
  unrecorded standard violation.
- Reopen SB01/SB02 from SB03 if Docker build context, service DNS,
  configuration binding, API routing, or AccountApp assets fail after restart.
