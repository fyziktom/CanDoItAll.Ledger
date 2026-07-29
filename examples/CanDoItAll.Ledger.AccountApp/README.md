# CanDoItAll.Ledger.AccountApp

Blazor WebAssembly account application for locally held Ledger keys and
transactions against a running CanDoItAll Ledger node.

AccountApp creates or imports an ECDSA P-256 account, encrypts its private key
inside a browser vault, queries account state, and prepares signed
transactions. The Node remains the authority that validates and confirms those
transactions.

## Architecture

- Standalone Blazor WebAssembly PWA
- encrypted vault records in browser IndexedDB
- `CanDoItAll.Ledger.Cryptography.Browser` for Web Crypto operations
- `CanDoItAll.Ledger.Sdk` for Node access
- `CanDoItAll.Ledger.Components` for the account workspace
- unprivileged Nginx image for static hosting and same-origin API proxying

The Docker image writes `appsettings.json` from environment variables at
container startup. In Compose, the browser calls the AccountApp origin and
Nginx proxies `/api/` to the `node` service. This avoids cross-origin setup in
the default stack. Runtime configuration stays network-only and is excluded
from the PWA integrity cache; the versioned static application shell remains
cacheable.

## Run The Docker Quickstart

From the repository root:

```powershell
Copy-Item .env.example .env
# Replace POSTGRES_PASSWORD and LEDGER_ACCOUNT_BACKUP_PASSWORD in ignored .env.

docker compose --env-file .env up -d --build --wait --wait-timeout 300 `
  postgres node accountapp
```

Open `http://127.0.0.1:5081`.

## First Account

1. Choose **Create account**.
2. Enter a display name and a new vault password.
3. Select **Create encrypted vault**.
4. Copy the account address displayed in the workspace.
5. Open the development Node UI at
   `http://127.0.0.1:8080/transactions`.
6. In **Mint**, paste the new address, enter a small main amount, and submit.
7. Return to AccountApp and refresh the workspace.
8. Export an encrypted account backup and keep it in protected storage.

Account creation is local; it does not register a user or upload the private
key to the Node. A new address appears in Ledger state only after it receives a
confirmed output.

## Import A Seeded Development Account

For a disposable local stack, the optional Seeder can create funded accounts:

```powershell
.\scripts\seed-demo.ps1 -EnvFile .env
```

It writes encrypted import files under:

```text
docker/demo-keys/accountapp-demo/imports/
```

Paste `alice-accountapp-backup.json` into **Import account**, enter the
`LEDGER_ACCOUNT_BACKUP_PASSWORD` from `.env`, and choose a new password for the
browser vault. The ignored `docker/demo-keys` directory contains sensitive
development credentials; never commit, publish, or reuse it for real accounts.

## Build From Source

Start a matching disposable Node in one PowerShell terminal:

```powershell
$env:ASPNETCORE_URLS = "http://localhost:8080"
$env:Ledger__Persistence__Provider = "InMemory"
$env:Ledger__Auth__Ui__Mode = "Disabled"
$env:Ledger__Api__CorsAllowedOrigins = "http://localhost:5045"

dotnet run --no-launch-profile `
  --project src\CanDoItAll.Ledger.Node\CanDoItAll.Ledger.Node.csproj
```

Then run AccountApp in a second terminal:

```powershell
dotnet run `
  --project examples\CanDoItAll.Ledger.AccountApp\CanDoItAll.Ledger.AccountApp.csproj `
  --launch-profile http
```

Open `http://localhost:5045`. The development configuration expects the Node at
`http://localhost:8080/`; direct browser access requires the matching CORS
origin configured above.

Build only the static-host image:

```powershell
docker build `
  --file docker\accountapp\Dockerfile `
  --tag candoitall-ledger-accountapp:dev `
  .
```

Use Compose to run it because the included Nginx proxy resolves the Node by the
Compose DNS name `node`.

## Runtime Environment

| Variable | Default or purpose |
| --- | --- |
| `ACCOUNT_APP_NODE_BASE_URI` | Browser-visible AccountApp origin used for same-origin `/api/` calls |
| `ACCOUNT_APP_API_BASE_PATH` | `api/v1` |
| `ACCOUNT_APP_LEDGER_ID` | `default` |
| `ACCOUNT_APP_SCENARIO_ID` | Optional scenario; empty or `__null__` means live Ledger state |
| `ACCOUNT_APP_VAULT_ID` | IndexedDB vault key for this app origin |
| `ACCOUNT_APP_DEFAULT_ACCOUNT_NAME` | Initial display name |
| `ACCOUNT_APP_ENABLE_IPFS_UPLOADS` | Enables the optional upload driver |
| `ACCOUNT_APP_IPFS_API_BASE_URI` | Browser-visible upload API |
| `ACCOUNT_APP_IPFS_GATEWAY_BASE_URI` | Content gateway base URI |
| `ACCOUNT_APP_IPFS_CONTENT_ENDPOINT_URI` | Optional content resolver endpoint |

IPFS uploads are disabled in the public Compose default because the stack does
not include an IPFS service.

Compose derives the browser-visible origin from `CDA_PUBLIC_HOST` and the
AccountApp port. Keep that hostname identical to the one opened in the browser;
scheme, host, and port are all part of the browser origin.

## Account Safety

- Browser vaults are scoped to scheme, host, and port. Each AccountApp port has
  separate IndexedDB state.
- Clearing site data removes the local vault. There is no third-party password
  or private-key recovery.
- Export a password-protected backup before relying on an account.
- Resetting the local vault does not delete Node history or Ledger balances.
- The running page can access an unlocked key for signing. Treat CSP, XSS
  prevention, and browser/device security as key-custody controls.
- Never place Node API keys, bearer tokens, treasury keys, or privileged
  signing material in a public WASM configuration.

The current AccountApp quickstart targets the local development Node profile.
For a production Node with authenticated APIs, use a reviewed same-origin
server/proxy design that authenticates users without shipping shared secrets to
the browser.

See the
[complete first-setup guide](https://github.com/fyziktom/CanDoItAll.Ledger/blob/main/docs/getting-started/node-and-account-app.md)
and
[SDK/driver guide](https://github.com/fyziktom/CanDoItAll.Ledger/blob/main/docs/development/sdk-and-drivers.md).
