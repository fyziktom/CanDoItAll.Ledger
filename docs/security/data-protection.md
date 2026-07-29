# Data Protection Key Handling

ASP.NET Core Data Protection keys protect framework and application payloads
such as authentication cookies, antiforgery state, and other values created
through the Data Protection APIs.

They are not Ledger account private keys, transaction-signing keys, database
credentials, or transaction history.

## Local Simulator

`CanDoItAll.Ledger.Simulator.App` stores its generated key ring under:

```text
examples/CanDoItAll.Ledger.Simulator.App/artifacts/dataprotection/simulator-app
```

The repository ignores every `artifacts/` directory. Generated keys therefore
remain local and cannot become package or source inputs accidentally.

On Windows, the Simulator protects key-ring entries with machine-scoped DPAPI.
The encrypted XML is usable only by a process with access to the corresponding
Windows machine protection context. On other operating systems, protect the
directory through filesystem permissions or configure an appropriate external
key-protection mechanism before using the host beyond disposable development.

Override the location through:

```text
DataProtection:KeyRingPath
```

Relative values are resolved from the Simulator App content root. Production
or shared deployments must use a durable, access-controlled path outside
repository artifacts.

## Regeneration

For a disposable local Simulator host:

1. Stop the host.
2. Remove only the exact ignored
   `artifacts/dataprotection/simulator-app` directory.
3. Start the host again.
4. Confirm a new `key-*.xml` file appears in that directory.

Regeneration invalidates cookies, antiforgery tokens, or other application data
protected by the previous key ring. It does not alter Ledger transactions,
balances, account signing keys, or PostgreSQL data.

Do not delete a production key ring unless invalidating all protected state is
an explicit, reviewed operation.

## Docker Node

The Compose node stores its key ring in the `node-dpkeys` named volume mounted
at `/var/lib/candoitall-ledger/dpkeys`. Normal `docker compose down` preserves
that volume. `docker compose down --volumes` deletes it and must be treated as a
destructive development reset.

The Docker key ring provides application authentication continuity. Back up the
authoritative PostgreSQL Ledger independently.

## Repository Rules

- Never commit generated Data Protection keys.
- Never use `artifacts/` as a production key store.
- Do not confuse Data Protection keys with Ledger signing keys.
- Keep local keys, certificates, passwords, and account credentials out of
  packages and container images.
- Document and test key-ring persistence, encryption, rotation, and recovery
  for every production deployment.
