# Implementation Prompt

Use this prompt when executing the bundle.

1. Reopen `bundle://inputs/00-original-request.md`, `bundle://requirements/01-normalized-requirements.md`, `bundle://plan/01-phase-plan.md`, and the active subbundle README.
2. Check the active subbundle entry gate and exact source references.
3. Implement only the active subbundle scope. Do not copy VEFramework code; use it only to preserve UX semantics.
4. For critical subbundles, create `bundle://proof/SBxx/manifest.md` and `bundle://proof/SBxx/semantic-invariants.md` before closure.
5. Capture failing-first proof where behavior changes, then passing proof after implementation.
6. Run targeted tests and any browser proof required by the active subbundle.
7. Update `bundle://reviews/01-execution-report.md`, traceability closure status, and root README status before moving downstream.
