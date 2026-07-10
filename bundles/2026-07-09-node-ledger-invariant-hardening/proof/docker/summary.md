# Docker and deployed-node proof

The development compose stack was rebuilt from the hardened sources and is running:

- PostgreSQL 16 (healthy), host port 54329;
- Economy Node, host port 8080;
- AccountApp Alice/Bob/Lab, host ports 5081/5082/5083.

## Readiness

Endpoint: `/api/v1/health/readiness`

Final relevant values:

- status: `ok`;
- storage: `Npgsql.EntityFrameworkCore.PostgreSQL`;
- ledger sequence: 12;
- ledger hash: `f8143957062dd5292a14a9cf142266520c33bfda8b648fe70755c2aba2ac77dc`;
- verification: `valid_payload_only`, zero issues;
- projection: `valid`, zero issues;
- projection hash: `81cc98344389e39de44d062357ab8028d8ef78c9f414a273e219f5dc58a1302f`;
- security profile: `Development`.

## Negative submissions

| Probe | Observed response |
| --- | --- |
| One output mixes positive main and token amounts | `accepted: false`; `invalid_transaction_shape` |
| Reuse the four outputs already consumed by Bob's merge | `accepted: false`; four `spent_input` errors |
| Mint a new token without treasury/permission authority | `accepted: false`; `unauthorized_token_creation` |
| Valid oversized JSON sent with chunked transfer | HTTP 413 |
| Supported treasury main issuance to Bob | Accepted: tx `411bd126ae47bcbc0593965f8780e5720adb80c1a7c232648a4eaadb1ba60b36`, sequence 12; Bob UI refreshed to 1,001 main |

The compose configuration is deliberately a development/demo profile. Its public ports, credentials, reset, and relaxed authorization are not production-security evidence.
