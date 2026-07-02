# Implementation Prompt

Execute the bundle subbundles in order. Before each subbundle, read the root README, phase plan, traceability rows, and that subbundle README. Do not implement later subbundle scope until the current progression gate passes.

Engineering rules:

- Use production ledger transactions/signatures for public-key discovery.
- Add WebCrypto ECDH through the existing browser crypto bridge; do not use server-side encryption for WASM users.
- Preserve signed transaction public key material.
- Encrypt message body/subject and attachment bytes before ledger/IPFS persistence.
- Keep list rendering lazy; no bulk decrypt for previews.
- Use existing CanDoItAll components and tooltip patterns where available.
- Record proof under `proof/SBxx/` for every critical subbundle.
- Run bundle validators at prepared and completed stages.
