# Product Vision

## Purpose

CanDoItAll.Ledger gives autonomous agents and project teams a common,
inspectable source of truth for financial actions.

The immediate problem is expense control. An agent may be allowed to acquire a
service, transfer funds, pay for project resources, or operate a delegated
budget. Those actions need durable evidence of:

- who requested and authorized the action;
- which account, asset, project, or business object was affected;
- which rules were evaluated;
- what value moved and where it went;
- how the resulting state was derived;
- whether the transaction history remains internally consistent.

The Ledger turns those questions into explicit transaction, signature, rule,
state-transition, persistence, and audit contracts.

## Product Goals

### Accountable agent spending

Agents should operate within explicit financial authority rather than relying
on application-local balances or informal logs. The Ledger supplies the
building blocks for delegated accounts, transaction signing, approval rules,
spending constraints, traceable transfers, and later review.

### Auditable project finances

Projects should be able to associate expenses, income, assets, liabilities,
settlements, and business-object changes with durable project and account
identities. Confirmed activity should be queryable and reproducible from the
transaction history.

### Reusable financial infrastructure

The Ledger is not limited to one user interface or one agent runtime. Its
contracts, execution engine, persistence providers, node, SDK, and components
can serve as a base for:

- project expense and budget-control systems;
- approval and treasury workflows;
- internal settlement and value-transfer applications;
- asset, entitlement, or token tracking;
- business-object ownership and commerce;
- audit and reconciliation tools;
- finance-specific applications with their own domain rules.

### Financial planning and analysis

The experimental Simulator is intended to model project assumptions, planned
events, interventions, and financial outcomes. Its long-term value comes from
reconciling those projections with confirmed Ledger activity without mixing
projected values with actual transaction facts.

## Product Principles

- **Evidence before convenience.** Important state changes should have
  inspectable transaction and authorization evidence.
- **Deterministic execution.** The same valid input and prior state should
  produce the same ledger result.
- **Explicit authority.** Accounts, keys, rules, and approvals should make
  financial authority visible and testable.
- **Stable history.** Persisted transaction and format identities are
  compatibility contracts.
- **Provider isolation.** Storage, content addressing, user interfaces, and
  hosting are replaceable around stable ledger contracts.
- **Truth separation.** Confirmed ledger facts, derived views, and simulated
  projections must remain distinguishable.
- **Defense in depth.** Cryptographic checks complement access control,
  operational security, review, and domain-specific compliance.

## Non-Goals

The Ledger does not by itself provide legal identity, bank custody, regulatory
approval, tax treatment, generally accepted accounting, or investment advice.
Applications built on it remain responsible for those domain and jurisdictional
requirements.

The Simulator is not currently a production forecasting or accounting system.
Its models and persistence contracts may change while the product direction is
validated.
