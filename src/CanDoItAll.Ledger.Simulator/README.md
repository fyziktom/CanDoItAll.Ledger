# CanDoItAll.Ledger.Simulator

Experimental BO/project Simulator runtime for Ledger-owned project workspaces,
scenario materialization, interventions, projections, reconciliation inputs, and
development-time observation.

## Status

This project is work in progress and experimental. It is not production-ready,
and its APIs, storage behavior, domain shape, and UI workflows may change
without compatibility guarantees.

Simulator output is not authoritative financial, accounting, forecasting, or
economic evidence. Do not use it for production decisions, regulatory
reporting, audit conclusions, or claims about the separate Economy simulator.

## Boundary

The runtime uses `CanDoItAll.Ledger.Simulator` identities and may depend on
Ledger contracts. It must not import Economy simple-account drivers,
Smith-style experiments, scenario packs, or WebGL observer evidence.

The browser host currently keeps project workspaces, runs, and generated graphs
in memory. EF Core projection adapters exist, but the active run lifecycle does
not persist all runtime state through them.
