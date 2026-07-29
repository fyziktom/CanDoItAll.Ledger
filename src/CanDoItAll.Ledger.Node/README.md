# CanDoItAll.Ledger.Node

HTTP node and operator console for secure, auditable CanDoItAll Ledger state.

The Node composes transaction validation and execution, persistence,
cryptography, business-object services, health checks, authentication, typed
SDK endpoints, and an embedded Blazor operator UI. Run it when applications,
agents, or project tools need a shared confirmation boundary and inspectable
financial history.

## Runtime Surfaces

| Surface | Default path |
| --- | --- |
| Embedded operator UI | `/` |
| Versioned API | `/api/v1` |
| Liveness | `/api/v1/health` |
| Readiness and ledger verification | `/api/v1/health/readiness` |
| Bootstrap and security metadata | `/api/v1/node/bootstrap` |

The development compatibility path `/api` can be enabled, but production
configuration requires it to be disabled.

## Storage Choice

Ledger-state persistence has two providers:

- `InMemory` for tests and disposable local runs;
- `PostgreSql` for durable shared state.

There is no SQLite ledger-state provider. SQLite is available only for optional
ASP.NET Identity/JWT metadata. Production rejects in-memory ledger storage, so
the Node Docker image and Compose quickstart use PostgreSQL.

## Run Locally

The checked-in launch profile uses disposable in-memory ledger and identity
storage:

```powershell
dotnet run `
  --project src\CanDoItAll.Ledger.Node\CanDoItAll.Ledger.Node.csproj `
  --launch-profile CanDoItAll.Ledger.Node
```

The console prints the selected HTTP and HTTPS addresses. The launch profile
also enables local development administrator defaults; never reuse those
credentials outside a disposable workstation run.

## Run With Docker

From the repository root:

```powershell
Copy-Item .env.example .env
# Replace POSTGRES_PASSWORD and LEDGER_ACCOUNT_BACKUP_PASSWORD in ignored .env.

docker compose --env-file .env config --quiet
docker compose --env-file .env up -d --build --wait --wait-timeout 300 `
  postgres node accountapp
```

Open:

- Node operator UI: `http://127.0.0.1:8080`
- AccountApp: `http://127.0.0.1:5081`

Verify the API:

```powershell
Invoke-RestMethod http://127.0.0.1:8080/api/v1/health
Invoke-RestMethod http://127.0.0.1:8080/api/v1/health/readiness
```

Normal teardown preserves PostgreSQL and Data Protection volumes:

```powershell
docker compose --env-file .env down --remove-orphans
```

## Build The Node Image

```powershell
docker build `
  --file docker\node\Dockerfile `
  --tag candoitall-ledger-node:dev `
  .
```

The canonical image runs as a non-root user on container port `8080`. Its root
filesystem is read-only in Compose, and its Data Protection key ring is mounted
at `/var/lib/candoitall-ledger/dpkeys`.

## Main Configuration

.NET configuration keys use `__` instead of `:` in environment variables.

| Setting | Purpose |
| --- | --- |
| `Ledger__Persistence__Provider` | `InMemory` or `PostgreSql` |
| `Ledger__PostgreSql__ConnectionString` | Durable Ledger database |
| `Ledger__Persistence__SchemaManagementMode` | `EnsureCreated`, `Migrate`, or `None` |
| `Ledger__Security__Profile` | `Development` or `Production` |
| `Ledger__Auth__Api__WriteMode` | Write API authorization mode |
| `Ledger__Auth__Api__ReadMode` | Read API authorization mode |
| `Ledger__Api__Key` | API key when an API-key mode is selected |
| `Ledger__Auth__Ui__Mode` | `Disabled` or cookie-protected operator UI |
| `Ledger__Auth__Persistence__Provider` | Identity metadata: `InMemory`, `Sqlite`, or `PostgreSql` |
| `Ledger__Auth__DataProtection__KeyRingPath` | Durable framework key ring |
| `Ledger__Api__MapLegacyRoutes` | Enables the compatibility `/api` path |
| `Ledger__Api__AllowLedgerReset` | Enables the destructive development reset endpoint |

The full option mapping lives in `LedgerNodeHost.ResolveOptions` and
`LedgerNodeHostOptions`.

## Two Different Identities

- A Node operator identity is an ASP.NET Identity user used for a protected
  operator UI and JWT administration.
- A Ledger account is an address and signing-key pair used to authorize ledger
  transactions.

Creating a Ledger account does not register a Node UI user. Public Node identity
registration is disabled. The AccountApp stores its Ledger account locally in
the browser and talks to the versioned API.

## First Protected Operator Setup

For a protected operator console, configure cookie UI authentication and a
durable auth store before the first start:

- set `Ledger__Auth__Ui__Mode=Cookie`;
- set `Ledger__Auth__Persistence__Provider=PostgreSql` and supply
  `Ledger__Auth__PostgreSql__ConnectionString` for a dedicated auth database;
- for built-in first-time provisioning, point that connection at a separate,
  initially empty database and set
  `Ledger__Auth__Persistence__SchemaManagementMode=EnsureCreated`;
- persist `Ledger__Auth__DataProtection__KeyRingPath`;
- enable `Ledger__Auth__Ui__SeedAdmin` for the first start and supply
  `CANDOITALL_LEDGER_ADMIN_EMAIL` and
  `CANDOITALL_LEDGER_ADMIN_PASSWORD` through secrets.

Do not point auth `EnsureCreated` at the Ledger database: the existing Ledger
tables prevent the auth context from creating its Identity tables. Use
`SchemaManagementMode=None` for an externally provisioned auth schema.
`Migrate` is appropriate only when the deployed Node build contains reviewed
auth migrations.

After startup, sign in at `/Account/Login`. To expose `/admin/jwts`, also set
the read or write API mode to `RequireJwtBearer`, and supply a secret
`Ledger__Auth__Jwt__SigningKey` of at least 32 bytes. Mixed
`RequireApiKeyOrJwt` mode additionally requires `Ledger__Api__Key`. An
administrator can then create and revoke scoped bearer tokens at `/admin/jwts`.

After the administrator exists, disable seeding and remove the bootstrap
password from the environment. In VPS mode, set
`Ledger__Auth__Ui__AllowExistingAdmin=true` when starting from an already
provisioned auth database.

For local source runs, an ignored path such as
`artifacts/dataprotection/node` is suitable for regenerated Data Protection
keys. Compose uses the durable `node-dpkeys` volume instead. These framework
keys protect cookies, antiforgery state, and ASP.NET Identity/Data Protection
tokens. JWT bearer tokens use `Ledger__Auth__Jwt__SigningKey`; neither key type
is a Ledger account signing key.

SQLite remains available for auth metadata in non-container local tools. The
Compose Node has a read-only root filesystem, so SQLite requires an explicit
writable mounted database path. PostgreSQL is the recommended container auth
store.

## Production Security Profile

`Ledger__Security__Profile=Production` fails startup unless the host supplies
production-safe settings. Among other checks, production requires:

- authenticated read and write API modes;
- explicit PostgreSQL storage;
- migration or externally managed schema handling, not `EnsureCreated`;
- disabled legacy routes and ledger reset;
- configured Ledger-admin signing material;
- non-default treasury keys and a finite bootstrap amount;
- enforced token-creation policy;
- no development administrator defaults.

VPS mode additionally requires cookie UI authentication, JWT API
authentication, durable non-memory identity storage, and an explicit Data
Protection key-ring path. Secrets and private signing keys belong in the
deployment platform's secret store, not source, image layers, or committed
Compose files.

## API Clients

Use
[`CanDoItAll.Ledger.Sdk`](https://www.nuget.org/packages/CanDoItAll.Ledger.Sdk)
for typed .NET integration. The SDK supports versioned paths, API keys, bearer
tokens, health/bootstrap reads, account queries, and transaction submission.

See:

- [Node and AccountApp first setup](https://github.com/fyziktom/CanDoItAll.Ledger/blob/main/docs/getting-started/node-and-account-app.md)
- [SDK and driver integration](https://github.com/fyziktom/CanDoItAll.Ledger/blob/main/docs/development/sdk-and-drivers.md)
- [Container operations](https://github.com/fyziktom/CanDoItAll.Ledger/blob/main/docs/operations/containers.md)
- [Data Protection](https://github.com/fyziktom/CanDoItAll.Ledger/blob/main/docs/security/data-protection.md)
