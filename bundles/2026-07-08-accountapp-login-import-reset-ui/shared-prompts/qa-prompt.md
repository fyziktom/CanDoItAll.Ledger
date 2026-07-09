# QA Prompt

Validate the completed bundle against the raw request, requirements, and execution report.

Required checks:

- At large desktop size, the unlocked screen has no AccountApp top bar above the shared workspace.
- At large desktop size, the locked screen is readable and not a scattered card dump.
- Import is a stepped flow using BaseLib `Steps`.
- Reset/delete local account is available only in locked state and requires acknowledgement plus typed confirmation before enablement.
- Create, unlock, encrypted-backup import, and pasted-key import still route to the existing session methods.
- Browser proof is real Playwright proof with screenshots and explicit review answers.
- Dockerized app rebuild transcript exists.
- If any note is partial, mark it `Partially solved` and add a concrete blocker or follow-up instead of closing it.
