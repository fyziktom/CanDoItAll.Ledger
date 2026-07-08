# C# Testability Plan

## Negative Tests

- `TransactionShapeValidator` rejects any output with both `Token` and positive `MainAmount`.
- `TransactionShapeValidator` rejects normal `TransactionDataItemType.BusinessObject` BO output with no token.
- `TransactionShapeValidator` rejects normal BO output with token amount other than `1`.

## Builder Tests

- Create BO with token and main payment produces separate BO token output and plain main output.
- Token transfer with requested main amount produces separate token and main outputs.
- Payment/purchase/receipt commerce outputs remain token-backed with `0 MAIN`.

## Integration Proof

- Full test project.
- Docker compose build.
- Real browser AccountApp purchase proof with screenshots and API assertions.

