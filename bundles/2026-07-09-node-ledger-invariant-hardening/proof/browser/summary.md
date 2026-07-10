# Large-screen AccountApp browser proof

Tool: in-app Browser controlled through the Playwright-compatible MCP interface.

Viewport: 1600x1000 only. Small and medium viewports were intentionally not exercised.

## Confirmed flows

1. Imported/unlocked the seeded AccountApp identities against the rebuilt Docker Node.
2. Alice created Post `Ledger hardening post 20260709` from `bo.posts` supply.
   - Output 0: `bo.posts` amount 1, main 0, BO metadata present.
   - Output 1: `bo.posts` change 99,998,999, main 0.
3. Alice transferred the Post BO to Bob.
   - The input/output carrier remained `bo.posts` amount 1 and main 0.
   - Owner changed from Alice to Bob under Transfer semantics.
4. Bob split 1,000 main currency into 200 + 200 + 200 + 400.
5. Bob merged those four outputs back to 1,000; UI confirmed ledger sequence 11.
6. A fresh Bob browser profile imported its key, reloaded to the unlock screen, entered the password, and unlocked with one click. No `Password is required` error remained.
7. After the final Node rebuild and an authorized treasury mint, Bob refreshed at 1600x1000 and displayed main balance 1,001, four UTXOs, and six transactions.

## Screenshots

- `alice-post-token-split-transaction.png`
- `bob-received-post-transfer-v2.png`
- `bob-post-transfer-carrier-conservation.png`
- `bob-utxo-split-merge-confirmed.png`
- `accountapp-one-click-unlock.png`
- `bob-treasury-main-issuance-refresh.png`

The browser viewport override was reset after proof collection. Temporary proof-only AccountApp containers were stopped; the normal Alice/Bob/Lab compose services remain running.
