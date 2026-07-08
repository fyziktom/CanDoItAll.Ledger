# SB01 Token Carrier Transaction Contract

Status: completed

Scope:

- Add a reusable carrier-token policy helper.
- Extend clone requests and builder assembly to support a fresh one-unit clone carrier token.
- Preserve explicit main-only creation behavior.

Acceptance:

- Token-backed create behavior remains green.
- Clone transactions can consume source BO plus token source and produce original BO, clone BO token `1`, and token change.

Proof:

- `proof/SB01/transcripts/04-create-carrier-token-split-source-tests.txt`
- `proof/SB02/transcripts/07-focused-tests-after-commerce-token-fallback.txt`
