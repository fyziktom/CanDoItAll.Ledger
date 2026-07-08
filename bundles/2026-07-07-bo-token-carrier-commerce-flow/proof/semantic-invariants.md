# Semantic Invariants

Status: passed; follow-up repair passed

Final docker proof: `proof/SB03/transcripts/29-accountapp-commerce-recycle-real-transaction-proof-nodepath.txt`

Final full test sweep: `dotnet test tests\CanDoItAll.Ledger.Tests\CanDoItAll.Ledger.Tests.csproj` (`289` passed).

Validated outputs from `proof/SB03/accountapp-bo-token-carrier-proof.json`:

| Artifact | TxId | Output | Token | Main |
| --- | --- | ---: | --- | ---: |
| Created BO | `27dfff6e028aec40df99a567935124cf1c1f67de6045e9ee286af9ed580cef64` | 0 | `1 bo.posts` | 0 |
| Payment BO | `e7e2118b1b56229e6d014e720c2445a854de2d34c9ae45e2c97303a7ee7fa0c1` | 0 | `1 bo.posts` | 7.25 |
| Receipt BO | `298cc7b48855fb968a171f836d632bd8c983ebe63d199e13a7cf21e563b6381b` | 0 | `1 bo.posts` | 0 |
| Purchase BO | `671ed701a621418e4e72ffb94e6c2c19bf01bb0bd38df53c67ea61cdb23d5aea` | 0 | `1 bo.posts` | 0 |
| Delivered clone | `659e1b55278d6015de439ebf303809f3bdbc7e3029f858f77fd1de828fe6586b` | 1 | `1 bo.posts` | 0 |
| Receipt recycle merge | `8df2cf3db1214b1d77d1406d2b91601856c321da8babb095d6b6e58ffbe96851` | 0 | `99998989 bo.posts` | 0 |

Receipt recycle invariant:

- Input 0 is the receipt BO output with `1 bo.posts`.
- Input 1 is the same-token reusable source output with `99998988 bo.posts`.
- Output 0 has no BO metadata and carries `99998989 bo.posts`.

Screenshots visually checked:

- `proof/SB03/screenshots/alice-created-bo-token-detail.png`
- `proof/SB03/screenshots/alice-receipt-token-detail.png`
- `proof/SB03/screenshots/bob-purchase-token-detail.png`
- `proof/SB03/screenshots/alice-dispatch-dashboard.png`
- `proof/SB03/screenshots/alice-payment-related-bo-detail.png`
- `proof/SB03/screenshots/alice-receipt-related-bo-detail.png`
- `proof/SB03/screenshots/alice-receipt-recycle-merge-transaction.png`

Warnings observed:

- Existing `ncalc` `NU1701` restore/build compatibility warnings.
- Existing `SQLitePCLRaw.lib.e_sqlite3` `NU1903` warning in test/node restore paths.
- Docker Blazor publish falls back without `wasm-tools` workload.
