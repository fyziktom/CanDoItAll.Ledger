# CanDoItAll.Ledger.Simulator.Persistence.EFCore

EF Core persistence for the moved BO/project simulator projections.

This project is work in progress and experimental. Provider adapters are
registered, but the current browser-host run lifecycle does not write all
workspace and run state through them. Do not treat this layer as a
production-ready audit store.

This persistence layer belongs to Ledger because the Simulator is a BO/project
workspace runtime. The separate Economy simple/Smith runtime must not take an EF
dependency on it.
