# Implementation Prompt

Implement this bundle one subbundle at a time.

- Read the root README, current subbundle README, `plan/01-phase-plan.md`, and traceability rows before editing.
- Preserve user-owned unrelated changes.
- Use shared `BusinessObjectAccount` and `BusinessObjectTransactionBuilder` APIs for transaction behavior.
- Capture failing-first proof before production changes when behavior changes.
- Record transcripts under `proof/SBxx/transcripts/` with command, working directory, timestamp or run label, exit code, and output.
- Update `reviews/01-execution-report.md`, subbundle status, proof manifests, semantic invariants, and raw-note closure while proof is fresh.
- Stop and repair the bundle if implementation requires new project references, partial classes, or demo-local raw transaction assembly.
