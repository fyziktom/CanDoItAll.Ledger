# Implementation Prompt

Implement only the scoped privacy hardening in this bundle.

- Preserve old v1 secure-message parsing and decryption.
- Add failing-first or red-team tests before relying on a fix.
- Search persisted raw transaction JSON, not just rendered text, for leaked subject/body/attachment metadata.
- Keep public sender/receiver ledger facts visible; do not attempt stealth routing in this bundle.
- Prefer existing components/services and avoid broad UI refactors.
- After each subbundle, update `reviews/01-execution-report.md` with command proof and unresolved risks.
