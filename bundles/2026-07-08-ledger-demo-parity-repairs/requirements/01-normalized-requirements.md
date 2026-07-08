# Normalized Requirements

| ID | Requirement | Observable success |
| --- | --- | --- |
| `R001` | Preserve the WASM AccountApp behavior as the reference path. | Existing WASM adapter tests continue to pass and source assertions show other demos use the same `BusinessObjectAccount` APIs. |
| `R002` | Console and SSR UTXO merge must use explicit selected plain main UTXOs as inputs and create exactly one plain output. | Tests/details assert selected input count and a single output to self or chosen receiver. |
| `R003` | Console and SSR UTXO split must use explicit selected plain main UTXOs and create requested output rows plus positive change only. | Tests/details assert requested output amounts and no BO/token outputs in split outputs. |
| `R004` | UTXO split/merge selection must exclude BO outputs, token outputs, permission outputs, and rule outputs. | Negative tests prove selected BO/token outputs are rejected or not selectable. |
| `R005` | BO create/update/merge/split outputs must keep the BO output at zero main amount with exactly one carrier token. | Tests/source assertions inspect `BusinessObjectTransactionBuilder` outputs and SSR draft token amount. |
| `R006` | Permission-holder token minting must not mint main currency. | Tests/source assertions prove `TokenMintRequest.MainAmount` is absent or zero and positive main minting is rejected. |
| `R007` | Console demo lesson 7 must teach both plain UTXO split/merge and advanced BO transactions without conflating the two. | Console lesson/helper tests and README/source assertions cite UTXO split/merge separately from BO split payment/merge. |
| `R008` | SSR components demo development fallback must use one-token BO carrier drafts like AccountApp. | Development workspace test asserts `TokenAmount == 1` for BO create draft and create output semantics remain valid. |
| `R009` | SSR components demo sample adapter route must represent UTXO split/merge modes clearly enough for component demos. | Component/sample tests assert accepted mode/result for `UtxoSplit` and `UtxoMerge` drafts and no fallback to old BO modes. |
| `R010` | Demos must compile after parity changes. | `dotnet build` or targeted project build transcripts pass. |
| `R011` | Critical behavior proof must include failing-first and passing transcripts, changed-file hashes, source assertions, and anti-stub audit. | `proof/SBxx/manifest.md` and `proof/SBxx/semantic-invariants.md` cite existing artifacts. |
| `R012` | Raw request notes must close note by note. | `reviews/01-execution-report.md` marks every `N00x` row `Solved`, `Partially solved`, or `Not solved` with proof. |
