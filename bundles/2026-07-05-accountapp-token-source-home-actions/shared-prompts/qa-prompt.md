# QA Prompt

Review the implementation against the raw request and bundle gates.

- Confirm raw notes N001, N002, and N003 are each closed or explicitly marked partial.
- Confirm the rendered home page no longer has a standalone Token Sources card.
- Confirm the Balances card shows main balance and only real token-source UTXOs.
- Confirm no unique one-amount token or BO-backed token output appears as a token source.
- Confirm the home action group has visible short labels and the top window toolbar remains icon-only.
- Review the large-screen screenshot for spacing, text fit, button label readability, no incoherent overlap, and reduced vertical card footprint.
- Confirm targeted tests and AccountApp build transcripts exist and have exit code `0`.
- Confirm critical SB01 proof has a manifest, semantic invariants, changed-file hashes, source assertions, anti-stub audit, and negative/positive proof transcripts.
