# Original request

The user asked for a detailed analysis of the actual Node and Ledger implementation, not a documentation-only review.

The intended product is:

- a single-node, database-backed, UTXO ledger;
- not a distributed blockchain;
- runnable with durable database persistence or in-memory for simulations;
- main currency plus token layers;
- permissioned creation of new token types by addresses authorized from treasury/admin;
- token supply that can be split, transferred, merged, and combined with other assets in one transaction;
- BusinessObjects represented by unique token-carrier outputs;
- token source outputs with amount greater than one used to create BO carrier outputs of amount one;
- BO close/recycle flows that merge the carrier token back into a same-token source;
- transactions that may contain separate main-currency and token outputs, but never one output carrying both;
- node-side rejection of spent inputs, malformed transactions, invalid signatures, unauthorized issuance, imbalance, and other protocol violations.

The user requested:

- a large phased bundle with detailed checklists and references;
- an XLSX execution/coverage checklist;
- C# architecture guidance and deeper refactoring where justified;
- implementation of the prepared plan;
- automated tests including unhappy paths;
- Docker AccountApp testing through Playwright MCP;
- large-screen browser proof only.

The older bundle workflow is inspiration, not a rigid gate system. Progress gates must preserve evidence and dependency order without blocking useful work through ceremony.

