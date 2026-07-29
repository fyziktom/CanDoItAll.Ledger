# CanDoItAll.Ledger.DemoSeeder

Ledger-owned demo data seeding tool.

The tool prepares ledger demo state and should remain independent from separated simulator-lane content.

Validate with build and a demo seeding smoke when behavior changes.

## Opt-in live token/BO verification

After resetting a disposable development ledger, the seeder can also verify the real AccountApp Alice signing path:

```powershell
docker compose --env-file .env --profile seed run --rm demo-seeder --verify-permission-token-bo-flow
```

The switch leaves normal seeding unchanged. In verification mode the seeder grants Alice a repeatable `xray` mint permission, resolves that exact permission output, submits Alice-signed transactions that mint `xray.image` with a supply of `1000000000000` and create an `image` BusinessObject carrying one token, then verifies the plain token change and node readiness. Use it only on a freshly reset disposable ledger because the verification token id is intentionally deterministic.

The equivalent environment switch is `CANDOITALL_LEDGER_VERIFY_PERMISSION_TOKEN_BO_FLOW=true`.

The Compose model requires `POSTGRES_PASSWORD` and
`LEDGER_ACCOUNT_BACKUP_PASSWORD`. Copy `.env.example` to ignored `.env`, replace
the placeholders, and never commit or share the generated `docker/demo-keys`
content.
