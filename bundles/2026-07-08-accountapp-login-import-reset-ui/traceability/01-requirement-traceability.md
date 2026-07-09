# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `N001` / `R001` | `requirements/01-normalized-requirements.md` | `subbundles/01-account-entry-layout-and-topbar-removal` | Source assertion, AccountApp build, unlocked Playwright screenshot/assertion. | Primary screenshot repair. |
| `N002` / `R002` | `requirements/01-normalized-requirements.md` | `subbundles/01-account-entry-layout-and-topbar-removal`, `subbundles/03-validation-docker-closure` | Large-screen locked screenshot review and DOM assertions. | Large desktop only. |
| `N003` / `R002`, `R003`, `R004` | `analysis/01-current-state.md`, `requirements/01-normalized-requirements.md` | `subbundles/01-account-entry-layout-and-topbar-removal`, `subbundles/02-import-steps-and-guarded-reset-flow` | Source assertions and import-step Playwright navigation. | VEF is a UX reference, not a compatibility requirement. |
| `N004` / `R002` | `requirements/01-normalized-requirements.md` | `subbundles/01-account-entry-layout-and-topbar-removal` | Locked no-vault browser proof shows create and import affordances. | Preserve first-run create/import choice. |
| `N005` / `R005` | `architecture/01-target-solution.md` | `subbundles/02-import-steps-and-guarded-reset-flow`, `subbundles/03-validation-docker-closure` | Targeted reset tests plus Playwright proof that reset button is disabled until confirmation. | Critical destructive-behavior guard. |
| `N006` / `R003`, `R006` | `analysis/01-current-state.md` | `subbundles/02-import-steps-and-guarded-reset-flow` | Components MCP record and source assertion for `Steps`/`StepsItem`. | Prefer shared components. |
| `N007` / `R002` | `inputs/02-structured-input.md` | `subbundles/01-account-entry-layout-and-topbar-removal`, `subbundles/03-validation-docker-closure` | Browser viewport logged as large desktop. | Small/medium explicitly scoped out. |
| `N008` / `R007` | `plan/01-phase-plan.md` | `subbundles/03-validation-docker-closure` | Playwright transcript and screenshots under `proof/SB03/browser`. | Real browser proof required. |
| `N009` / `R008` | `plan/01-phase-plan.md` | `subbundles/03-validation-docker-closure` | Docker compose build transcript. | Final closure dependency. |
