# CanDoItAll.Ledger.DemoSeeder

Development tool for preparing repeatable Ledger accounts, balances,
permissions, tokens, and BusinessObjects.

The Seeder is intended only for disposable development environments. Generated
account material is sensitive and must remain under ignored local storage.

## Run

```powershell
docker compose --env-file .env --profile seed run --rm demo-seeder
```

The Compose model requires `POSTGRES_PASSWORD` and
`LEDGER_ACCOUNT_BACKUP_PASSWORD`. Copy `.env.example` to ignored `.env`, replace
the placeholders, and never commit or share `docker/demo-keys`.

## Opt-In Signed Transaction Verification

After resetting a disposable development Ledger, verify the AccountApp Alice
signing path:

```powershell
docker compose --env-file .env --profile seed run --rm `
  demo-seeder --verify-permission-token-bo-flow
```

Verification mode grants Alice a repeatable `xray` mint permission, resolves
the exact permission output, submits Alice-signed transactions that mint an
`xray.image` supply and create an `image` BusinessObject, then checks token
change and node readiness.

Use the mode only on a freshly reset disposable Ledger because the verification
token ID is deterministic. The equivalent environment setting is:

```text
CANDOITALL_LEDGER_VERIFY_PERMISSION_TOKEN_BO_FLOW=true
```
