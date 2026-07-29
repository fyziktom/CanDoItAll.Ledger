# CanDoItAll.Ledger.Simulator

Experimental project-finance runtime for workspaces, business units, accounts,
assumptions, planned events, deterministic scenarios, interventions,
projections, issues, and reconciliation inputs.

## Status

This project is work in progress. Its APIs, storage behavior, domain model, and
user workflows may change without compatibility guarantees. It is intentionally
excluded from public NuGet packaging.

Simulator output is not authoritative accounting, forecasting, tax, legal,
compliance, economic, or investment evidence.

## Truth Boundary

Planned and simulated values are projections. Confirmed Ledger transactions are
facts. A future observation adapter will relate them through traceable actuals
and variances without rewriting either history.

The browser host currently keeps project workspaces, runs, and generated graphs
in memory. EF Core projection providers exist, but the active lifecycle does
not persist all runtime state through them.

See [Project Finance Simulator Vision](../../docs/architecture/ledger-simulator-vision-and-refactoring.md).
