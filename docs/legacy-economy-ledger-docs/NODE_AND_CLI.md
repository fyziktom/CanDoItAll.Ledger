# Node and CLI

## Node responsibilities

The node owns operational state:

- known-address metadata and any future admission registry;
- admin signing key access;
- transaction submission;
- transaction confirmation;
- projection maintenance;
- chain verification;
- snapshot creation;
- archive compaction;
- simulation ledger execution.

The node must not allow direct writes to projection tables as business operations.

## CLI responsibilities

The CLI provides a minimal operator interface over storage for local development, migration rehearsals, and small deployments. The current Gate B command surface focuses on verification, projection rebuild, and signed snapshots; transaction drafting/submission commands remain future SDK/operator work.

## API boundary

The node maps public API routes under `/api/v1`. `EconomyNodeLedgerClient` now uses `/api/v1` by default and can be configured back to legacy `/api` with `EconomyNodeLedgerClientOptions.ApiBasePath = "api"`. Keep `EconomyNodeHostOptions.MapLegacyApiRoutes` enabled only while older tools still need compatibility routes.

State-changing endpoints can require an API key, JWT bearer token, or either credential by setting `EconomyNodeHostOptions.ApiSecurityMode` to `RequireApiKey`, `RequireJwtBearer`, or `RequireApiKeyOrJwt`. Read endpoints use `ReadSecurityMode` with the same shape. The default API-key header is `X-CDA-Economy-Key`; the SDK sends it when `ApiKey` is configured and sends `Authorization: Bearer <token>` when `BearerToken` is configured.

`EconomyNodeHostOptions.UiAuthMode = EconomyNodeUiAuthMode.Cookie` enables ASP.NET Core Identity cookie login for the browser console. Admin account management is available through `/Account/Manage`, authenticator setup through `/Account/Manage/EnableAuthenticator`, and admin JWT creation through `/admin/jwts/` when JWT API auth is enabled. `RequireTwoFactorAuthentication` redirects signed-in admins without 2FA to authenticator setup before they use the console.

For local development, `Economy:Auth:Ui:UseDevelopmentAdminDefaults=true` enables cookie UI auth, seeds the admin user, and fills missing credentials with `admin@admin.com` / `Admin@1234`. In environment-variable form, use `Economy__Auth__Ui__UseDevelopmentAdminDefaults=true`. Production validation rejects this flag and rejects the development password even if it is supplied manually.

`EconomyNodeHostOptions.SecurityProfile = EconomyNodeSecurityProfile.Production` is a fail-closed production gate. It rejects development write auth, public read auth, in-memory storage, default PostgreSQL credentials, `EnsureCreated` schema management, legacy route mapping, development ledger-admin signing, default treasury key material, unlimited treasury bootstrap supply, and enabled reset. If `VpsHostingMode` is also true, production validation additionally requires cookie UI auth, JWT-capable read/write API auth, non-in-memory auth persistence, and a persistent data-protection key ring path. Development remains the default for local examples.

`/api/v1/health` is a cheap liveness surface. `/api/v1/health/readiness` performs chain and projection verification, reports trusted-admin verification posture, and should be protected/bounded in production. Transaction detail reads omit inline JSON and external references unless callers explicitly request `includeData=true` and the read policy allows it.

## Commands

```text
economy verify chain [--provider postgres|inmemory] [--connection <connection>] [--database <name>] [--ledger <id>] [--scenario <id>]
economy verify projections [--provider postgres|inmemory] [--connection <connection>] [--database <name>] [--ledger <id>] [--scenario <id>]
economy projections rebuild [--provider postgres|inmemory] [--connection <connection>] [--database <name>] [--ledger <id>] [--scenario <id>]
economy snapshot create [--provider postgres|inmemory] [--connection <connection>] [--database <name>] [--ledger <id>] [--scenario <id>]
economy snapshot verify --snapshot-id <id> [--provider postgres|inmemory] [--connection <connection>] [--database <name>]
```

`--provider postgres` is the default and uses `CANDOITALL_ECONOMY_CONNECTION` when `--connection` is omitted. The fallback development connection string is `Host=localhost;Port=54329;Username=economy;Password=economy;Database=candoitall_economy`.

`--provider inmemory` is intended for CLI smoke tests and examples. It accepts `--database <name>` even though data is process-local.

`verify chain` runs `LedgerChainVerifier` over confirmed transaction history. If `CANDOITALL_ECONOMY_LEDGER_ADMIN_PUBLIC_KEY` is present, chain verification uses strict trusted-admin-key cryptographic signature verification; otherwise it reports payload-only verification. Production node readiness must not be treated as healthy unless trusted admin verification is configured and valid.

`verify projections` calculates deterministic hashes for persisted UTXO, spent, address, token-type, BusinessObject current/version, and full projection state and reports corruption such as address projection mismatches.

`projections rebuild` rebuilds derived rows from confirmed transaction history. Use it only on a backup, restored copy, migration rehearsal, or stopped-writer maintenance window.

`snapshot create` requires `CANDOITALL_ECONOMY_LEDGER_ADMIN_PUBLIC_KEY` and `CANDOITALL_ECONOMY_LEDGER_ADMIN_PRIVATE_KEY`; optional `CANDOITALL_ECONOMY_LEDGER_ADMIN_KEY_ID` and `CANDOITALL_ECONOMY_LEDGER_ADMIN_ALGORITHM` override signer metadata. Snapshot creation refuses to sign if projection verification is invalid.

`snapshot verify` verifies the stored snapshot hashes and signature against current persisted projection state.

## UI path

The node ships a small browser console. It is public only when `UiAuthMode=Disabled`; when `UiAuthMode=Cookie`, static console assets are protected before they are served. The UI can read projections directly for performance if needed, but all state-changing operations must go through the node or SDK transaction submission path.

See `docs/VPS_HOSTING.md` for the VPS profile, proxy guidance, data-protection key-ring requirements, JWT rotation/revocation notes, and the explicit rule that the CLI is not reverse-proxied as a server surface.

## Key management

For MVP, keys can be file-based PEM files. For production, move admin keys to a secure secret store or HSM-compatible provider. The database must never contain admin private keys.

`EconomyNodeHostOptions.LedgerAdminAccount` lets the node use a configured ledger-admin key instead of `DevelopmentLedgerAdminSigner`. Configuration keys are `Economy:LedgerAdmin:KeyId`, `Economy:LedgerAdmin:Algorithm`, `Economy:LedgerAdmin:PublicKey`, and `Economy:LedgerAdmin:PrivateKey`; matching environment fallbacks use the `CANDOITALL_ECONOMY_LEDGER_ADMIN_*` prefix. Bootstrap responses expose only public signer posture and public treasury bootstrap material.

Rotation policy for production should publish old and new trusted admin public keys during rollover, verify the ledger with both where appropriate, then revoke the old key after the final transaction signed by that key is outside the rollback window. Do not log private key material, API keys, or raw sensitive transaction payloads.
