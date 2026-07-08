# Architecture Checkpoints

## After SB01

- Shared card component exists and is used by `BusinessObjectLedgerGallery`.
- No new custom CSS exists.
- Empty DataItems branch is not a duplicate renderer system.
- Media DataItems branch still works.

## After SB02

- Provider priority keeps rule/settings first where relevant, commerce/typed family before media, media before generic.
- Related BO lookup stays UI-projection-only and does not introduce API calls.
- Tests prove extracted behavior without constructing the full app.

## After SB03

- Browser screenshots confirm the first tab uses available large-screen space intentionally.
- Docker AccountApp rebuild validates production packaging.
- C# architecture review gate is updated with actual changed files and proof.

