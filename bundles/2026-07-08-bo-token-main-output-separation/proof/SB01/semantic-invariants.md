# Semantic Invariants

## Invariants

- `INV-TOKEN-MAIN-001`: Any output with a token has `MainAmount == 0`.
- `INV-BO-CARRIER-001`: Any normal BO output carries a token with amount exactly `1`.
- `INV-BO-CARRIER-002`: Permission and rule authority outputs remain governed by their existing authority validators.
- `INV-PAYMENT-SPLIT-001`: Main-currency payment outputs are plain outputs without token or BO metadata.

## Production Behavior Artifact Matrix

| Invariant | Producer | Consumer | Negative proof |
|---|---|---|---|
| `INV-TOKEN-MAIN-001` | Node shape validator | Ledger executor | `LedgerCriticalInvariantTests` rejects a token output carrying non-zero main amount. |
| `INV-BO-CARRIER-001` | Node shape validator | Ledger executor | `LedgerCriticalInvariantTests` rejects a normal BO output without a token amount of exactly `1`. |
| `INV-PAYMENT-SPLIT-001` | Business object transaction builder | AccountApp proof | Docker payment tx `fcb1c0a2f7e897605952d0cbb43bb26303f220947254fb227f7025f0b629cdc6`: BO output `0` is token-only; seller payment output `1` is main-only. |

## Positive Runtime Proof

- Created BO carrier: tx `46109481485e59a36f27af9735feb3cb48dfc505adab1d1be698e406111f712d`, output `0`, token amount `1`, main amount `0`.
- Payment carrier: tx `fcb1c0a2f7e897605952d0cbb43bb26303f220947254fb227f7025f0b629cdc6`, output `0`, token amount `1`, main amount `0`.
- Plain seller payment: same tx, output `1`, main amount `7.25`, no token metadata.
- Receipt carrier: tx `e15b98a81b0318883b4bdf059e33db7dee98dfdc448578b8c7295c7e607e111e`, output `0`, token amount `1`, main amount `0`.
- Purchase carrier: tx `84b05b4b1667a13f88b48c5162bd45f36da5abbb04edc68cea809f4bb42da629`, output `0`, token amount `1`, main amount `0`.
- Delivered clone carrier: tx `103f45fb0884f544f2417dcf84e825c2909dfffbaff67f8cca01977f6381e19f`, output `1`, token amount `1`, main amount `0`.
