# SB03 Semantic Invariant Contract

- Invariant ID: SB03-E2E-BROWSER-001
- Source raw note: RN-01 through RN-04 require the docker-hosted AccountApp stack to prove import, menu/dialog usability, transient notification, full width, auto-refresh, and browser cache behavior.
- Expected behavior: Three nginx-hosted AccountApp containers and the node API are running, Alice import works, Send dialog transaction submission succeeds, an independent browser transaction is detected by the original app without manual reload, and transaction detail loading writes the account workspace cache key to localStorage.
- Disallowed shallow implementation: Browser proof that only loads the landing page, uses a mocked API, skips import, or relies on a manual page refresh for new transaction visibility.
- Failing-first test: N/A process/non-production exemption; this is final integration proof for the enhancement bundle after code changes.
- Passing test: Docker health proof, full .NET test proof, and Playwright scripts prove import, send notification, auto-refresh, and localStorage cache writes.
- Changed source files: repo://bundles/2026-06-30-accountapp-refresh-cache-ui-polish/proof/SB03/scripts/import-alice.playwright.js, repo://bundles/2026-06-30-accountapp-refresh-cache-ui-polish/proof/SB03/scripts/submit-send-notification.playwright.js, repo://bundles/2026-06-30-accountapp-refresh-cache-ui-polish/proof/SB03/scripts/wait-original-auto-refresh.playwright.js, repo://bundles/2026-06-30-accountapp-refresh-cache-ui-polish/proof/SB03/scripts/check-localstorage-cache.playwright.js.
- Production assertions: Browser DOM results and screenshots prove the live AccountApp behavior against the docker node and persisted database.
- Red-team negative case: The original AccountApp must update after the second AccountApp broadcasts a signed transaction, without using a manual reload command.
- Downstream dependency check: The docker AccountApp image rebuilds, app endpoints on 5081/5082/5083 return HTTP 200, and node readiness returns HTTP 200.
