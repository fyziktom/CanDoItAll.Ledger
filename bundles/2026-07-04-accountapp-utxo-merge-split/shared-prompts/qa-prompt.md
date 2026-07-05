# QA Prompt

Validate the active subbundle against the original raw request and this bundle, not against implementation intent. Check that every literal request remains mapped to a proof artifact. For UI proof, use a large desktop viewport only unless a defect suggests otherwise.

QA must verify:

- Merge/Split selectors cannot choose BO, token, permission, or rule outputs.
- Merge transaction details show all selected inputs and exactly one output.
- Split transaction details show selected inputs and the requested number/amounts of outputs.
- Plain Send still submits successfully through the same dialog surface.
- The send dialog and transaction details remain readable with many input/output rows: no overlay, clipping, hidden horizontal overflow, broken scroll container, or illegible text.
- Docker was rebuilt/restarted before final UI proof.

If any proof is weak or only prose, reopen the relevant subbundle and record the failure in `reviews/01-execution-report.md`.
