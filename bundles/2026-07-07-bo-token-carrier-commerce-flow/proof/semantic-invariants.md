# Semantic Invariants

Status: passed

Final docker proof: `proof/SB03/transcripts/23-accountapp-real-transaction-proof-after-commerce-token-fallback.txt`

Final full test sweep: `proof/SB02/transcripts/09-full-ledger-tests-final.txt` (`287` passed).

Validated outputs from `proof/SB03/accountapp-bo-token-carrier-proof.json`:

| Artifact | TxId | Output | Token | Main |
| --- | --- | ---: | --- | ---: |
| Created BO | `a862eb070bd15df1f4d6bb9ecf07156703b7b800e9cc3daefcbfb4fd73923fd7` | 0 | `1 bo.posts` | 0 |
| Payment BO | `53800520408f8f2532ab03c0364788741db3a8baaa9aa9bc6355b0e3d7f95427` | 0 | `1 bo.posts` | 7.25 |
| Receipt BO | `f5649d7ba059cda203c668381d098fec3dee6e79d289e3e4f05af95bbfd874c3` | 0 | `1 bo.posts` | 0 |
| Purchase BO | `a5e00139a47f2a10cc3b6db4f211d56d8947052afc98223625fffb2613135505` | 0 | `1 bo.posts` | 0 |
| Delivered clone | `835e8ed9ea6cd5109360d297f16b231b089f719c59bbf0669a427263169700b5` | 1 | `1 bo.posts` | 0 |

Screenshots visually checked:

- `proof/SB03/screenshots/alice-created-bo-token-detail.png`
- `proof/SB03/screenshots/alice-receipt-token-detail.png`
- `proof/SB03/screenshots/bob-purchase-token-detail.png`
- `proof/SB03/screenshots/alice-dispatch-dashboard.png`

Warnings observed:

- Existing `ncalc` `NU1701` restore/build compatibility warnings.
- Existing `SQLitePCLRaw.lib.e_sqlite3` `NU1903` warning in test/node restore paths.
- Docker Blazor publish falls back without `wasm-tools` workload.
