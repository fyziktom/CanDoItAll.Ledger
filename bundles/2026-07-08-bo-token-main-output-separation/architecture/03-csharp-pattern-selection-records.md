# C# Pattern Selection Records

## Existing Validator Pattern

- Problem force: the node must reject malformed output shapes regardless of driver or UI path.
- Selected pattern: existing validator pipeline in `TransactionShapeValidator`.
- Rejected alternative: UI-only checks, because API callers could still submit invalid transactions.
- New types/projects: none.
- Test seam: direct validator tests and executor rejection tests.
- Proof required: negative tests for token/main mixing and token-less normal BO outputs.

## Existing Builder Pattern

- Problem force: account drivers must construct separated outputs consistently.
- Selected pattern: existing transaction builder methods.
- Rejected alternative: a separate post-build rewriter, because it would hide domain decisions and make tests less direct.
- New types/projects: none.
- Test seam: unsigned builder shape tests before submission.

