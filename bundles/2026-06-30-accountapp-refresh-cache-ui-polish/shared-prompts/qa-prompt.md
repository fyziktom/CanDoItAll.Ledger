# QA Prompt

Validate on the live Docker-hosted AccountApp:

- Large desktop viewport first.
- Import/unlock Alice if needed.
- Verify a transaction sent from the UI shows only popup notification behavior and no persistent inline result/refresh alert.
- Verify the full shell spans the available large-screen width.
- Verify account state updates after a new transaction without browser page refresh.
- Capture screenshots and DOM assertions in `reviews/01-execution-report.md`.
