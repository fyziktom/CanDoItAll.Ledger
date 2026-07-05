# Implementation Prompt

Implement the current subbundle only. Read the root README, phase plan, traceability, original request, and selected subbundle README before editing. Preserve the user's literal scope: ownership verification scanning is Home-only, and each BO must be updateable from BO details through the same form style as minting.

Use the smallest change set that matches the existing Blazor component patterns. Prefer BaseLib `Dialog`, `Grid`, `Stack`, `FormField`, and `Button` usage already present in the AccountApp components. Extend `BusinessObjectCreationWizard` rather than creating a separate edit form. Stage edits as `BusinessObjectTransactionMode.UpdateObject`, not create.

For each critical subbundle, capture failing-first or red-team proof, passing tests, source assertions, anti-stub audit output, changed-file hashes, and browser proof under `proof/SBxx/`. Update `reviews/01-execution-report.md` while proof is fresh. Stop and repair the bundle if implementation reality shows a raw note was narrowed.
