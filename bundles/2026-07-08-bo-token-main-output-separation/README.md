# BO Token/Main Output Separation

Status: Implemented and proof captured

## Raw Request

- Transaction `f4dfd6b3cbc207ef8a92e88d1434cc3855071955625b3f439cadadc30c928207` created a BO output with `1 MAIN` and no token. That is invalid for user-facing BOs.
- BO outputs must be token carriers: `1` token amount and `0` main amount.
- Token source outputs can carry larger token amounts, but still `0` main amount.
- Main-currency payments must be separate plain outputs, never mixed with token outputs.
- The node, drivers/builders, UI, and tests must protect this.
- Rebuild and test dockered AccountApp flows with real transactions and screenshots.

## Scope

- Node validation: reject mixed token/main outputs and reject normal BO outputs that are not unique token carriers.
- Drivers/builders: split token outputs from main outputs, and require carrier tokens for normal BO creation.
- UI/adapters: stop creating main-backed BO drafts or profile/message artifacts.
- Commerce proof: demonstrate BO payment, purchase, receipt, and delivered clone outputs remain unique token carriers and main payment is separate.

## Gates

- Unit and component tests passed: `dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj --no-restore` completed with `292/292` passing.
- Focused invariant tests passed for BO builder, ledger shape, and transition validation paths: `33/33` passing.
- Docker build passed for `economy-node`, `demo-seeder`, `accountapp-alice`, and `accountapp-bob`; transcript: `proof/SB01/transcripts/docker-build-cached.txt`.
- Browser proof passed on rebuilt Docker apps; proof JSON: `proof/SB01/accountapp-bo-token-main-separation-proof.json`.
- Screenshot proof is stored under `proof/SB01/screenshots/`.

## Docker Proof Highlights

- Created BO transaction: `46109481485e59a36f27af9735feb3cb48dfc505adab1d1be698e406111f712d`, output `0`, token `bo.posts`, token amount `1`, main amount `0`.
- Payment transaction: `fcb1c0a2f7e897605952d0cbb43bb26303f220947254fb227f7025f0b629cdc6`.
- Payment BO carrier output: output `0`, token `bo.posts`, token amount `1`, main amount `0`.
- Separate seller payment output: output `1`, main amount `7.25`, no token/BO metadata.
- Receipt, purchase, and delivered clone outputs all carried token amount `1` and main amount `0`.
- Receipt recycle merge transaction: `8bb8c83a53c3f2c61e522deaf3002fe467b42ffa09a7006932b9b68616b9e5f2`, merged carrier token back into a non-BO token source.

## Validation Gap

The CodeAnalytics MCP snapshot could not load the solution because it resolved `C:\repositories\CanDoItAll\global.json` and required SDK `10.0.200`. Architecture validation was completed manually through the C# bundle guard and review-gate checklist, with compile/test/browser proof captured here.
