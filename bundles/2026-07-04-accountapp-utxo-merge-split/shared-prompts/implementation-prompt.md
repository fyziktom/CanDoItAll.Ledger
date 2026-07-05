# Implementation Prompt

Implement the active subbundle only. Preserve the raw request literally: Merge and Split in the home send dialog are UTXO operations, not BusinessObject operations. Before editing, read the root bundle README, phase plan, traceability rows, and the active subbundle README. Make the smallest cohesive code changes, keep Send/Merge/Split form logic reusable, and stop if selected UTXO vin/vout semantics cannot be proven with tests and real transactions.

Required behavior: eligible Merge/Split inputs are plain main-currency UTXOs only (`BusinessObject is null`, `Token is null`). Merge creates exactly one output to self or the entered external address. Split consumes selected input UTXOs and creates equal or specific output amounts, with positive change returned to self when needed.

After implementation, update `reviews/01-execution-report.md`, write proof under `proof/SBxx/`, and run the subbundle closure gate. Do not proceed to a dependent subbundle while a critical manifest, semantic invariant, transcript, source assertion, anti-stub audit, or browser screenshot is missing.
