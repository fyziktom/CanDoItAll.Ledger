# SB05 Semantic Invariants

## Invariant: Docker Alice Bob Secure Messaging

- Invariant ID: `SB05-DOCKER-ALICE-BOB`
- Source raw note: `N010`, `N011`, `N014`
- Expected behavior: Rebuilt Docker WASM AccountApps exchange secure messages both directions, including Bob to Alice with encrypted attachment and Alice to Bob without attachment.
- Disallowed shallow implementation: Passing local unit tests without browser proof, using plaintext attachment upload, or opening an older message instead of the accepted transaction.
- Failing-first test: `bundle://proof/SB04/scripts/docker-secure-messaging-browser-proof.mjs` fails if attachment plaintext appears before decrypt, if the accepted transaction row is missing, if API errors occur, or if horizontal overflow exists.
- Passing test: `bundle://proof/SB04/docker-secure-messaging-browser-proof.json` has `passed: true`.
- Changed source files: `repo://src/CanDoItAll.Economy.Components/Components/Account/SecureMessagingWorkspace.razor`, `bundle://proof/SB04/scripts/docker-secure-messaging-browser-proof.mjs`.
- Production assertions: Browser proof uses real Docker Alice/Bob backups, real ledger submit responses, real tx ids, and explicit attachment decrypt.
- Red-team negative case: The proof checks attachment plaintext is absent before explicit decrypt and selects rows by accepted tx id to avoid false positives.
- Downstream dependency check: Final compose status confirms rebuilt AccountApp and node containers remain running against healthy Postgres.
