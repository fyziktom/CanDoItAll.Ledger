# CanDoItAll.Ledger.Simulator.Persistence.EFCore

EF Core persistence for the moved BO/project simulator projections.

This persistence layer belongs to Ledger because the old simulator runtime is a
BO/project workspace runtime. The new Economy simple/Smith runtime core must not
take an EF dependency.
