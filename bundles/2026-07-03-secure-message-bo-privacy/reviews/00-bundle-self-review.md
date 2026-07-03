# Bundle Self Review

## Preparation Checklist

- [x] Raw user complaint preserved with relevant raw tx excerpt.
- [x] Requirements normalized into testable privacy and compatibility invariants.
- [x] Affected code surfaces identified across business objects, WASM adapter, projections, raw tx UI, and tests.
- [x] Dependency map and critical subbundles documented.
- [x] XLSX impact map required and linked.
- [x] Docker/browser proof explicitly required before closure.

## Open Review Items

- Secure-message create transactions now write object-valued `stateJson`; readers also accept legacy string `stateJson` and `state` aliases.
- New secure-message public payloads write empty public attachments; decrypted content contains the attachment locator.
- Legacy v1 messages remain parseable through the fallback attachment path.
