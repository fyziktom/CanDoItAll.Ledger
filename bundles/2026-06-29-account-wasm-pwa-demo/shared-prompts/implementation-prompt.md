# Implementation Prompt

```text
Implement only the assigned subbundle from bundles/2026-06-29-account-wasm-pwa-demo.

Before editing:
- Read the root README, requirements, current-state analysis, phase plan, traceability matrix, execution report, and your subbundle README.
- Verify all prerequisites and progression gates from upstream subbundles.
- Do not implement later subbundles early.
- Preserve existing user changes and unrelated files.

During implementation:
- Keep browser private keys inside the AccountApp boundary.
- Do not add NBitcoin or BouncyCastle to the AccountApp/WASM path.
- Prefer existing ledger domain, SDK, and Razor component patterns.
- Update the workbook checklist if the implementation changes readiness, scope, risk, or validation status.
- Capture proof under proof/SBxx/ and reference it in reviews/01-execution-report.md.

Before closing:
- Run the required commands and browser checks from the subbundle README.
- Update the subbundle status, proof references, dependency impact notes, and execution report row.
- Stop and mark blocked if the progression gate cannot honestly pass.
```
