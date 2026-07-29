# Ecosystem Role And Boundaries

## Ledger Ownership

CanDoItAll.Ledger owns the contracts and implementations that determine
financial transaction truth:

- accounts, assets, amounts, UTXOs, transactions, blocks, and chain state;
- hashing, signing, signature verification, and key-handling contracts;
- transaction validation, deterministic execution, replay, and verification;
- account rules and explicit authority checks;
- ledger persistence and relational projections;
- node APIs, authentication integration, administration, and health;
- Ledger SDK, CLI, reusable components, examples, and test vectors;
- experimental project-finance simulation contracts and hosts.

## Integration Boundary

The Ledger is one foundation within the wider CanDoItAll ecosystem:

| Integration | Boundary |
| --- | --- |
| CanDoItAll Components | Published UI packages provide shared presentation primitives; Ledger owns finance-specific components and workflows |
| CanDoItAll IPFS | Published IPFS packages provide content-addressed storage capabilities; Ledger stores content identities and transaction evidence |
| Agent and project applications | Applications decide when to propose actions; Ledger decides whether submitted transactions satisfy Ledger contracts |
| Finance-oriented products | Products add domain workflows, approvals, reporting, and compliance around confirmed Ledger facts |

Normal builds consume Components and IPFS from NuGet.org. Product projects must
not depend on sibling source paths or unpublished local feeds.

## What The Ledger Does Not Own

The repository does not define:

- agent planning, prompting, or task orchestration;
- legal identity or external payment custody;
- bank, card, exchange, or tax-provider integrations;
- general-purpose content storage implementation;
- the shared CanDoItAll component design system;
- jurisdiction-specific accounting, tax, compliance, or investment decisions.

Those systems may integrate through explicit adapters without weakening Ledger
validation or changing persisted transaction truth.

## Dependency Rules

- Runtime dependencies point inward toward Ledger contracts.
- Hosts, providers, and user interfaces remain replaceable composition layers.
- External packages must be versioned and restored through NuGet.org or another
  reviewed package source.
- A provider may store or display Ledger data but must not silently reinterpret
  accepted transaction semantics.
- Confirmed Ledger facts and Simulator projections remain separate evidence
  classes.

## Experimental Simulator Boundary

`CanDoItAll.Ledger.Simulator*` models projects, assumptions, planned financial
events, scenario interventions, projections, and reconciliation candidates.

The Simulator is work in progress. It is not production-ready, and its output is
not authoritative accounting, forecasting, tax, legal, compliance, or
investment evidence. Simulator projects remain non-packable until their public
contracts and Ledger observation adapters are stable.
