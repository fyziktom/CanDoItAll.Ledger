# VPS Hosting Guide

This guide describes the supported shape for hosting `CanDoItAll.Economy.Node` on a VPS. Authentication remains optional so local development can keep using the node without login, but public or shared hosts should enable cookie auth for the browser UI and JWT bearer auth for the API.

## Local Development

Auth is disabled by default:

```json
{
  "Economy": {
    "Auth": {
      "Ui": { "Mode": "Disabled" },
      "Api": {
        "WriteMode": "Development",
        "ReadMode": "Public"
      }
    },
    "Persistence": {
      "Provider": "InMemory"
    }
  }
}
```

This mode is for local development only. Do not expose it through a public reverse proxy.

To use the development login while working locally, set:

```json
{
  "Economy": {
    "Auth": {
      "Ui": { "UseDevelopmentAdminDefaults": true }
    }
  }
}
```

This creates `admin@admin.com` with password `Admin@1234` and enables cookie UI auth. The production security profile rejects this switch and rejects the development password, so a Docker Compose or VPS configuration must provide a real admin secret instead.

## VPS Auth-On Profile

A public VPS should use durable ledger storage, durable Identity/JWT metadata, persistent data-protection keys, cookie-protected UI, and JWT-protected API routes:

```json
{
  "Economy": {
    "Security": { "Profile": "Production" },
    "Hosting": {
      "VpsMode": true,
      "UseForwardedHeaders": true,
      "EnableHttpsRedirection": true,
      "EnableHsts": true
    },
    "Auth": {
      "Ui": {
        "Mode": "Cookie",
        "RequireTwoFactor": true,
        "SeedAdmin": true,
        "AdminEmail": "admin@example.com"
      },
      "Api": {
        "WriteMode": "RequireJwtBearer",
        "ReadMode": "RequireJwtBearer"
      },
      "Persistence": {
        "Provider": "PostgreSql",
        "SchemaManagementMode": "Migrate"
      },
      "Jwt": {
        "Issuer": "CanDoItAll.Economy.Node",
        "Audience": "CanDoItAll.Economy.Node.Api",
        "DefaultLifetimeMinutes": 60,
        "MaxLifetimeDays": 30,
        "RequireActiveTokenRecord": true
      },
      "DataProtection": {
        "KeyRingPath": "/var/lib/candoitall-economy/dpkeys"
      }
    },
    "Api": {
      "MapLegacyRoutes": false,
      "AllowLedgerReset": false
    },
    "Persistence": {
      "Provider": "PostgreSql",
      "SchemaManagementMode": "Migrate"
    }
  }
}
```

Provide secrets outside source-controlled files:

```text
CANDOITALL_ECONOMY_CONNECTION=Host=127.0.0.1;Port=5432;Username=economy;Password=<secret>;Database=candoitall_economy
CANDOITALL_ECONOMY_AUTH_CONNECTION=Host=127.0.0.1;Port=5432;Username=economy_auth;Password=<secret>;Database=candoitall_economy_auth
CANDOITALL_ECONOMY_ADMIN_EMAIL=admin@example.com
CANDOITALL_ECONOMY_ADMIN_PASSWORD=<first-admin-password>
CANDOITALL_ECONOMY_JWT_SIGNING_KEY=<at-least-32-byte-random-secret>
```

After the first administrator is created, prefer removing `SeedAdmin` or the admin password secret and setting `Economy:Auth:Ui:AllowExistingAdmin=true`.

## Docker Compose

The repository includes a node container and Compose stack:

- `src/CanDoItAll.Economy.Node/Dockerfile` publishes the ASP.NET Core node on port `8080`.
- `examples/CanDoItAll.Economy.Components.Demo/Dockerfile` publishes the Economy component demo app.
- `tools/CanDoItAll.Economy.DemoSeeder/Dockerfile` publishes a one-shot seeder that creates Alice/Bob key files and the HreVaDa demo token state.
- `compose.yaml` runs the node plus PostgreSQL with cookie UI auth, JWT API auth, persistent data-protection keys, and local-only port bindings by default.
- `compose.demo.yaml` is the local HTTPS demo overlay. It adds a Kestrel dev certificate, switches the node API to `RequireApiKeyOrJwt`, seeds Alice and Bob, and starts the demo app after seeding succeeds.
- `compose.vps.yaml` is a production-profile overlay for a public VPS deployment.
- `docker/compose.env.example` documents configurable values; the base stack has local-only defaults, while the VPS overlay requires real secrets.

The node and app builds need sibling local dependencies:

- `CanDoItAll.IPFS`, because `CanDoItAll.Economy.BusinessObjects` references `CanDoItAll.IPFS.Client`.
- `CanDoItAll.Components`, because economy WebGL component projects use local project references.
- `CanDoItAll/ExternalPackages`, because BaseLib, Charts, Mermaid, and related UI packages are restored from the local package feed.

Keep those repositories/folders checked out side by side, or set `CDA_IPFS_CONTEXT`, `CDA_COMPONENTS_CONTEXT`, and `CDA_EXTERNAL_PACKAGES_CONTEXT` before building. `CDA_COMPONENTS_CONTEXT` should point at the Components repository `src` folder, not the whole checkout, so Docker does not send generated artifact folders as build context.

Local rehearsal:

```text
docker compose up --build
```

The default stack binds the node to `127.0.0.1:8080` and PostgreSQL to `127.0.0.1:54329`, uses development-profile schema bootstrap, and keeps local development credentials compatible with the existing CLI fallback. Pass `--env-file docker/compose.env.example` or a private env file when you want to override those defaults. Keep loopback bindings when a reverse proxy on the same VPS terminates TLS and forwards traffic to the node.

Local HTTPS customer-demo rehearsal:

```powershell
New-Item -ItemType Directory -Force docker/devcerts | Out-Null
dotnet dev-certs https --trust
dotnet dev-certs https -ep .\docker\devcerts\candoitall-economy-devcert.pfx -p candoitall-devcert-123!
docker compose -f compose.yaml -f compose.demo.yaml up --build
```

The demo overlay exposes the node at `https://localhost:8443` and the demo app at `https://localhost:8444`. The node still listens on internal HTTP port `8080` for service-to-service calls inside the Compose network; browser traffic for the node and app uses HTTPS. Runtime dev certificates under `docker/devcerts` and generated Alice/Bob keys under `docker/demo-keys` are gitignored.

The demo seeder is idempotent for the expected customer-demo state: it creates or reuses Alice and Bob account keys, mints a maximum HreVaDa source amount to admin when needed, and transfers 100 HreVaDa to Alice and 100 HreVaDa to Bob. If either account already has more than the configured transfer amount, the seeder fails rather than hiding an overfunded demo state.

Public VPS profile:

```text
docker compose --env-file /etc/candoitall-economy/env -f compose.yaml -f compose.vps.yaml up -d --build
```

The VPS overlay sets `Economy:Security:Profile=Production`, `Economy:Hosting:VpsMode=true`, `ASPNETCORE_ENVIRONMENT=Production`, and requires non-default treasury and ledger-admin key material. Keep the private keys and JWT signing key in a host secret store or locked-down environment file, not in the repository.

`compose.yaml` uses `EnsureCreated` for local schema bootstrapping. The VPS overlay defaults the ledger schema mode to `None` because formal EF migrations are still tracked as operational hardening work; initialize or migrate the ledger database through a reviewed deployment process before exposing the node publicly. After Identity/JWT tables exist, set `CDA_ECONOMY_AUTH_SCHEMA_MANAGEMENT=None` for steady-state hosting if you do not want the app to manage the auth schema at startup.

## Reverse Proxy

Terminate TLS at the proxy or in Kestrel, and forward only the intended routes. A typical proxy exposes `/` and `/Account/*` for administrators, `/admin/jwts/*` for JWT management, and `/api/v1/*` for API clients. Do not proxy the CLI as a server feature.

When TLS terminates at the proxy, set:

```json
{
  "Economy": {
    "Hosting": {
      "UseForwardedHeaders": true,
      "EnableHttpsRedirection": true,
      "EnableHsts": true
    }
  }
}
```

Restrict proxy access to `/admin/jwts/*` to trusted operator networks when possible. The app still requires an authenticated admin cookie and the Administrator role.

## Identity, 2FA, And Data Protection

The UI uses ASP.NET Core Identity cookies. Account management and authenticator setup are available under `/Account/Manage` and `/Account/Manage/EnableAuthenticator`.

Public self-registration is disabled. Create the first administrator through `Economy:Auth:Ui:SeedAdmin=true` and then switch to `Economy:Auth:Ui:AllowExistingAdmin=true` for steady-state hosting.

If `Economy:Auth:Ui:RequireTwoFactor=true`, signed-in administrators without enabled 2FA are redirected to authenticator setup before using the console. Store data-protection keys at `Economy:Auth:DataProtection:KeyRingPath` on persistent disk with permissions limited to the node process. Back up the key ring with the auth database; losing it invalidates existing cookies and some Identity tokens.

## JWT Operations

Administrators create JWTs at `/admin/jwts/` after signing in. Tokens have bounded lifetimes, are shown once, and are stored only as metadata plus a fingerprint. `Economy:Auth:Jwt:RequireActiveTokenRecord=true` makes revocation effective by requiring the `jti` to exist and remain active in auth storage.

Rotation policy:

- Create a new random `CANDOITALL_ECONOMY_JWT_SIGNING_KEY`.
- Restart nodes with the new signing key during a maintenance window.
- Reissue API client tokens from `/admin/jwts/`.
- Revoke old token records if the old key was suspected to be exposed.

If a token is exposed, revoke it from `/admin/jwts/`, review access logs, and issue a replacement with the shortest practical lifetime.

## SDK And CLI

The SDK can call JWT-protected APIs with:

```csharp
services.AddEconomySdkWithNodeDriver(
    new Uri("https://economy.example.com/"),
    options => options.BearerToken = configuration["Economy:Api:BearerToken"]);
```

If both `BearerToken` and `ApiKey` are configured on the SDK client, the bearer token is sent and the API-key header is not sent. Existing API-key mode remains available for private deployments that intentionally choose `RequireApiKey` or `RequireApiKeyOrJwt`.

The CLI remains a local/operator database tool for verification, projection rebuilds, and snapshots. It is not hosted by the node and should not be exposed through a reverse proxy.
