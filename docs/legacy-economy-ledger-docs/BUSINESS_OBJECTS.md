# BusinessObjects

## Purpose

BusinessObjects are ledger-bound domain objects. They replace NFT terminology with enterprise-friendly language.

The reusable account, casting, transaction helper, renderer, and component layer is documented in `BUSINESS_OBJECT_LAYER.md`.

## Object examples

```text
PurchaseRequest
PurchaseApproval
PurchaseOrder
Invoice
Asset
BudgetAllocation
RevenueForecast
SalaryCommitment
CloudCostCommitment
WorkPackage
RiskReserve
Contract
Subscription
License
```

## State model

Suggested generic states:

```text
Draft
Requested
Approved
Committed
Executed
Rejected
Cancelled
Expired
Closed
```

Domain-specific schemas can add fields and stricter state transitions.

## Versioning

Every BO mutation creates a new version. The transaction that creates the new version must spend the UTXO carrying the previous current version.

## Ownership

The owner is the address that controls the current BO UTXO.

## Data strategy

Small BO payloads can be stored in `business_object_versions.data_json`. Large payloads should be stored externally and referenced by hash.

## Cash-flow fields

Common fields useful for economic simulation:

```text
plannedAmount
committedAmount
actualAmount
earliestDate
latestDate
dueDate
expectedSettlementDate
recurrence
priority
canBeDelayed
confidence
costCenter
projectId
processId
```

## Projection views

The ledger can project:

- pending purchase requests by address;
- committed spend by department;
- planned spend by time window;
- forecast revenue by confidence;
- owned assets;
- cash-flow timeline.
