# SB11 — JWT read/write/admin scopes

Status: Complete — 2026-07-10

## Owned requirement

RQ-028 / R-004: make JWT scope a least-privilege endpoint authorization boundary.

## Acceptance checklist

- named read, write, and admin policies exist;
- scope claims support standard whitespace-separated `scope` and `scp` values;
- admin implies write/read and write implies read;
- legacy `node.api` translation is explicit;
- missing/unknown/read-only scopes deny write/admin endpoints;
- development and API-key modes retain their documented behavior;
- token issuance normalizes and validates allowed scopes.

## C# Architecture Impact

Node owns claim interpretation and endpoint policy mapping. Ledger/domain projects remain unaware of HTTP authorization.

## Boundary Ownership

Authorization policy/handler is independently testable; endpoint filters authenticate then authorize the required named policy.

## Dependency Direction

No project-reference change.

## Pattern Decision

ASP.NET named policy with a custom requirement/handler. Rejected: handler-local string contains checks or role substitution for API scopes.

## Testability Contract

Direct handler tests plus actual host endpoint tests for read/write/admin allow/deny cases.

## Partial Class Policy

No new partial; existing host partial organization is not expanded for domain behavior.

## Architecture Proof Required

Policy registration, endpoint mapping source assertion, HTTP status transcripts, anti-stub audit.

## Progression gate

All deny-by-default tests pass before production security claims are updated.

## Closure evidence

- Named read/write/admin policies and one scope requirement handler are registered in Node composition.
- Admin implies all access; write implies read; read cannot write/admin; legacy `node.api` is explicit compatibility.
- Live ASP.NET tests prove read GET 200, insufficient scope 403, allowed write reaches the handler, and admin reset requires admin.
- Unknown issued scopes are rejected; default issued scope remains read+write for compatibility.
