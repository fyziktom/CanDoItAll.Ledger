# QA Prompt

Validate the bundle from the user's perspective:

- Can Alice upload an image/file through AccountApp and get an IPFS CID-backed BO data item?
- Can Alice create every in-scope generic BO family and see correct type-specific card/detail data?
- Can at least one BO be sent/transferred from Alice to Bob and appear after Bob refreshes without manual database edits?
- Does the gallery show attachment previews after reload?
- Are dialog overlays readable, unclipped, and above surrounding UI?
- Do tests reject malformed or missing typed payload data rather than pretending it is a valid family object?
