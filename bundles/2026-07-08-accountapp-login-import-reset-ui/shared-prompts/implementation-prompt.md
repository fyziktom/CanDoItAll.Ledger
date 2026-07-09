# Implementation Prompt

Use this bundle to implement the next subbundle. Read the root README, plan, traceability, and selected subbundle README before editing.

Keep the implementation scoped to AccountApp account-entry UI, import steps, local-vault reset, and validation. Use CanDoItAll BaseLib `Steps`/`StepsItem`, `Grid`, `Stack`, and `FormSection` where they fit. Do not add VEF/Neblio import compatibility. Do not tune small or medium breakpoints beyond avoiding obvious regressions from shared component defaults.

Before closing a UI subbundle, capture the required browser analytics row and screenshot review answers in `reviews/01-execution-report.md`.

Stop and repair the bundle if implementation reality contradicts the current source references, if reset cannot be guarded by typed confirmation, or if the import flow cannot use BaseLib `Steps`.
