# Execution Report

## Summary

Status: Complete.

The duplicate create bug was caused by the AccountApp wizard deriving persistent `BusinessObjectId` from display metadata: `bo:{type}:{normalized-name}`. The ledger transition guard was correct and remains intact.

## Implemented Changes

- Added `BusinessObjectCreateIdFactory` to generate valid, bounded, unique create IDs in the components layer.
- Updated `BusinessObjectCreationWizard` so new create drafts use `BusinessObjectCreateIdFactory.CreateNewObjectId(...)`.
- Kept payload `name`, `description`, and `text` unchanged.
- Kept preview IDs as `bo:preview:...` and kept update/transfer/commerce object IDs unchanged.
- Added focused component/helper tests for same-name duplicate staging and long-name limits.

## Validation

- Focused tests: `proof/SB02/transcripts/01-focused-tests.txt` passed, 12 tests.
- AccountApp build: `proof/SB03/transcripts/01-accountapp-build.txt` passed.
- Docker AccountApp rebuild/recreate: `proof/SB03/transcripts/02-docker-accountapp-rebuild.txt`, `proof/SB03/transcripts/03-docker-accountapp-recreate.txt` passed.
- Browser proof: `proof/SB03/transcripts/04-playwright-duplicate-image-name.txt` passed and returned two distinct IDs:
  - `bo:image:duplicate-image-bo-1782921069849:8ded788966f74c0-0001-b84d49d11f4f`
  - `bo:image:duplicate-image-bo-1782921069849:8ded78897e28cc0-0002-f79bd8d47fdf`
- Full tests: `proof/SB03/transcripts/05-full-test-project.txt` passed, 226 tests.
- Final Docker validation: `proof/SB03/final-docker-validation.md` passed after auditing and fixing SSR default draft create submission:
  - Focused tests: 13/13.
  - Full tests: 227/227.
  - Docker services rebuilt and force-recreated.
  - WASM same-title image create proof passed.
  - SSR default message create proof passed and projected exact draft IDs with no timestamp suffix.

## Browser Evidence

- Screenshot: `proof/SB03/screenshots/alice-duplicate-image-name-gallery.png`
- SSR screenshot: `proof/SB03/screenshots/ssr-default-draft-unique-messages.png`
- Viewport: 1600x950
- Target: `http://localhost:5081/?view=image`

## Residual Notes

- This bundle does not redesign BO projection identity around outpoints. The current versioned object state machine still uses `BusinessObjectId`, so create IDs must remain unique before the finalized transaction hash exists.
