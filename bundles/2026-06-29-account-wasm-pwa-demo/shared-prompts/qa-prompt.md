# QA Prompt

```text
Validate the assigned subbundle without doing unrelated implementation.

Check:
- The subbundle objective is fully satisfied.
- Raw requirements mapped to the subbundle have observable proof.
- Critical proof includes production source changes, tests, negative cases, command transcripts, and browser artifacts where required.
- No private key is sent to the node, logged, or stored unencrypted.
- WASM crypto proof uses a real browser/WebCrypto path, not only .NET unit tests.
- SSR behavior remains intact when shared components are refactored.
- reviews/01-execution-report.md and plan/account-wasm-pwa-planning.xlsx reflect the current state.

Reject closure if proof is missing, stubbed, or only demonstrates a fake/in-memory path when the requirement is browser behavior.
```
