# Pattern Selection Records

## Carrier Token Policy

Pattern: small stateless policy helper.

Reason: the rule is deterministic, shared by several orchestration points, and does not need lifecycle state or external dependencies.

Rejected alternatives:

- Builder-level implicit token selection: would hide caller intent and make explicit main-only create tests brittle.
- Repeating `FirstOrDefault(option => !option.IsMain && option.HasReusableSource)` in each caller: creates drift risk across payment, receipt, purchase, and wizard flows.

## Clone Token Source

Pattern: request-enriched transaction builder method.

Reason: clone token sourcing requires an extra input and token change output. That belongs in transaction assembly, not UI code.

