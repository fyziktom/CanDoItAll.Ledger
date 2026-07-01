# Final Docker Validation

Date: 2026-07-01

## Commands

- `dotnet test tests\CanDoItAll.Ledger.Tests\CanDoItAll.Ledger.Tests.csproj --filter "FullyQualifiedName~BusinessObjectCreateIdentityTests|FullyQualifiedName~LedgerCriticalInvariantTests" --logger "console;verbosity=minimal"`
- `docker compose build`
- `docker compose up -d --force-recreate`
- `docker compose ps`
- `dotnet test tests\CanDoItAll.Ledger.Tests\CanDoItAll.Ledger.Tests.csproj --no-build --logger "console;verbosity=minimal"`
- `npx --yes --package @playwright/cli playwright-cli -s bo-identity-final-wasm run-code --filename bundles\2026-07-01-bo-create-identity-uniqueness\proof\SB03\scripts\duplicate-image-name.playwright.js`
- `npx --yes --package @playwright/cli playwright-cli -s bo-identity-final-ssr run-code --filename bundles\2026-07-01-bo-create-identity-uniqueness\proof\SB03\scripts\ssr-demo-default-draft.playwright.js`

## Results

- Focused identity/invariant tests passed: 13/13.
- Full test project passed: 227/227.
- Docker build and recreate passed for `postgres`, `economy-node`, `accountapp-alice`, `accountapp-bob`, and `accountapp-lab`.
- Final compose status: all five services up; Postgres healthy; ports `8080`, `5081`, `5082`, and `5083` exposed.
- HTTP readiness passed: node root `200`, Alice app `200`, Bob app `200`, Lab app `200`.
- Source audit found no remaining unsafe name-only/timestamp-only create ID patterns and no create branch that mutates `draft.ObjectId` through `UniqueObjectId`.

## Browser Proof

- WASM Alice duplicate image title proof passed with two distinct object IDs:
  - `bo:image:duplicate-image-bo-1782924375633:8ded79048c2af50-0003-c4c455a5ad6b`
  - `bo:image:duplicate-image-bo-1782924375633:8ded7904b3d2530-0005-8ae7967b8132`
- SSR default message draft proof passed with two distinct object IDs that projected exactly, without timestamp suffix mutation:
  - `bo:ui-message:message-from-alice:8ded790b9ab1323-000f-64c62f414d17`
  - `bo:ui-message:message-from-alice:8ded790bace3712-0011-efdf791d8849`

## Screenshots

- `proof/SB03/screenshots/alice-duplicate-image-name-gallery.png`
- `proof/SB03/screenshots/ssr-default-draft-unique-messages.png`
