# SB04 Proof Manifest

## Subbundle

- Id: SB04
- Status: Completed
- Owned raw notes: RN-02, RN-06
- Owned requirements: REQ-07
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`
- Changed-file hash manifest: `bundle://proof/changed-file-hashes.md`
- Changed-file SHA-256 sample: `83c5027fd4f57b3e9d045ba8dba719b9e04ef4049164c2a842cec9f732513ce1`

## Command Transcripts

- Full ledger tests: `bundle://proof/SB04/transcripts/01-full-tests.txt`
- Docker rebuild/restart/health: `bundle://proof/SB04/transcripts/02-docker-rebuild-restart-health.txt`
- Alice generic family browser matrix: `bundle://proof/SB04/transcripts/03-playwright-alice-create-matrix.txt`
- Bob receive/refresh browser proof: `bundle://proof/SB04/transcripts/04-playwright-bob-receive-refresh.txt`
- Anti-stub audit: `bundle://proof/SB04/transcripts/05-anti-stub-audit.txt`

## Screenshots

- `bundle://proof/SB04/screenshots/alice-imported-expanded-menu.png`
- `bundle://proof/SB04/screenshots/alice-ipfs-image-gallery.png`
- `bundle://proof/SB04/screenshots/alice-all-generic-types-gallery.png`
- `bundle://proof/SB04/screenshots/bob-payment-gallery.png`
- `bundle://proof/SB04/screenshots/alice-transfer-message-details-before-send.png`
- `bundle://proof/SB04/screenshots/alice-transfer-message-sent.png`
- `bundle://proof/SB04/screenshots/bob-received-transferred-message.png`
- `bundle://proof/SB04/screenshots/alice-upload-timeout.png`
- `bundle://proof/SB04/screenshots/alice-upload-retry.png`

## Failing-First And Negative Proof

- Failing-first transcript: N/A - process negative proof uses browser screenshots and separate-account verification for this UI validation subbundle.
- The initial browser upload failure is captured in the upload timeout/retry screenshots, proving a shallow upload implementation was not accepted.
- Bob receive proof rejects a sender-only happy path by importing Bob in a separate AccountApp instance and verifying the transferred Message BO after refresh.
- Anti-stub audit in `bundle://proof/SB04/transcripts/05-anti-stub-audit.txt` rejects fixture-only or template-only completion.

## Passing Proof

- Passing transcript: `bundle://proof/SB04/transcripts/03-playwright-alice-create-matrix.txt`
- Full tests passed 202/202.
- Alice import, expanded menu, IPFS Image creation, and all generic family cards were verified in Playwright.
- Bob import, Bob Payment creation, Alice-to-Bob Message transfer, and Bob refresh/gallery verification were completed in Playwright.
- Current IPFS health and proxy proof after the volume repair is in `bundle://proof/SB01/transcripts/02-ipfs-health-add-cat.txt`.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `AliceCreatedGenericBusinessObjects` | `bundle://proof/SB04/transcripts/03-playwright-alice-create-matrix.txt` | `bundle://proof/SB04/screenshots/alice-all-generic-types-gallery.png` | Ledger node and AccountApp docker health in `bundle://proof/SB04/transcripts/02-docker-rebuild-restart-health.txt` | `bundle://proof/SB04/screenshots/alice-upload-timeout.png` and anti-stub transcript |
| `BobReceivedTransferredMessage` | `bundle://proof/SB04/transcripts/04-playwright-bob-receive-refresh.txt` | `bundle://proof/SB04/screenshots/bob-received-transferred-message.png` | Account refresh and node API transaction path in the browser transcript | Separate Bob import and refresh rejects Alice-only local state |
