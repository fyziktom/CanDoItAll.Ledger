# SB01 Normal BO DataItems

## Scope

- Fix structured envelope parsing in EF projection and media payload parsing.
- Normalize DataItems so exactly one item is marked as main.
- Add download actions for normal BO DataItems.

## Proof

- Unit/component tests for object-valued `stateJson`, main-item normalization, and media parsing.
- Browser screenshot of an Image BO visible in gallery with thumbnail and DataItem detail/download.
