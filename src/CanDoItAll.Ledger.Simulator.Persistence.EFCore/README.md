# CanDoItAll.Ledger.Simulator.Persistence.EFCore

Entity Framework Core providers for experimental Simulator projections and
graph state.

The providers are registered by the local Simulator host, but the current run
lifecycle does not write every project, run, and graph through them. Do not
treat this layer as a production audit store.

Simulator persistence remains separate from confirmed Ledger transaction
persistence. This project is work in progress and intentionally non-packable.
