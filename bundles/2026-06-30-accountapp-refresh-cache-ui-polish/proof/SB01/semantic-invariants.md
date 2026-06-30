# SB01 Semantic Invariant Contract

- Invariant ID: SB01-REFRESH-CACHE-001
- Source raw note: RN-01 and RN-02 require conservative account refresh and reusable transaction detail caching.
- Expected behavior: The account host probes the newest account transaction fingerprint before doing a full workspace reload, reloads after a changed fingerprint, and uses `IAccountWorkspaceCache` for immutable transaction detail reads.
- Disallowed shallow implementation: A timer that always reloads the whole account, cache code that is never injected, or a WASM cache that does not persist through browser storage.
- Failing-first test: N/A process/non-production exemption; the baseline was user-observed stale data plus source analysis showing no background loop.
- Passing test: `dotnet test --filter AccountWorkspaceAdapterTests` proves unchanged snapshots skip reload, changed snapshots reload, and transaction details are cached.
- Changed source files: repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspaceAdapterHost.razor, repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs, repo://src/CanDoItAll.Economy.Components/Services/AccountWorkspaceCache.cs, repo://examples/CanDoItAll.Economy.AccountApp/Services/BrowserLocalStorageAccountWorkspaceCache.cs.
- Production assertions: Browser proof confirms localStorage cache writes, and bUnit proof confirms refresh skip/reload behavior through the adapter host.
- Red-team negative case: An unchanged latest-transaction probe must not call the full workspace load path.
- Downstream dependency check: AccountApp, SSR/demo adapters, node API contracts, and tests compile against the new adapter method and cache registration.
