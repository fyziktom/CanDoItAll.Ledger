# C# architecture review gate — second wave

Status: Passed on 2026-07-10

## Decision

R-001 through R-005 use genuine boundaries with executable seams. No fake partial split, cyclic project reference, service-locator shortcut, domain-to-persistence leak, or external SDK type remains in the BusinessObjects rule contract.

## Boundary audit

| Boundary | Owner | Gate result |
| --- | --- | --- |
| Immutable token definition | Core | Pass: no EF/Node types; versioned contract only. |
| Token canonical bytes/hash | Cryptography | Pass: display metadata excluded from ID; v1 path remains separate. |
| Token and replay semantics | Ledger | Pass: store contracts only; no concrete storage/EF dependency. |
| Migration abstraction | Persistence.EFCore | Pass: provider-neutral seam. |
| PostgreSQL adoption/DDL | Persistence.PostgreSql | Pass: provider SQL and migrations stay in adapter. |
| Readiness/JWT policy | Node | Pass: outer host/composition concern. |
| Rule expression contract | BusinessObjects | Pass: repository-owned result types and injected interface. |
| NCalc adaptation | Rules.NCalc | Pass: only project declaring NCalc package/types. |

## Dependency direction

- `Rules.NCalc -> BusinessObjects`; BusinessObjects does not reference the driver.
- `Sdk -> Rules.NCalc` is an outer default-composition dependency; pre-registering another evaluator wins because driver registration uses `TryAddScoped`.
- `Node -> Storage.InMemory` is an outer replay-store composition dependency; Ledger does not know the implementation.
- `Persistence.PostgreSql -> Persistence.EFCore`; EFCore remains Npgsql-free.
- Direct `.csproj` inspection and successful compilation show no cycle.

## Testability contract

- Disposable PostgreSQL databases exercise migration adoption and concurrent startup.
- Semantic replay accepts an arbitrary `ILedgerStore` and tests use isolated in-memory state.
- Token canonicalizer/hasher/policy are independently constructible.
- JWT handler/policies are directly tested and also exercised through a live ASP.NET host.
- Rule orchestration accepts a fake evaluator; driver compatibility is tested independently.

## Rejected designs

- SQL or migration history manipulation in Node.
- Applying current rules to all historical versions.
- Including mutable display fields in token identity or changing v1 canonical bytes.
- Endpoint-local scope string checks.
- Constructing NCalc statically inside BusinessObjects or resolving it via `IServiceProvider`.

## Tool limitation

CodeAnalytics snapshot `snap-20260710110637-cfb21d28-failed` is not used as evidence. Its host resolved repository SDK 10.0.200 while this machine has SDK 10.0.300, producing zero analyzed projects. Manual graph/source evidence plus green build/tests is the declared substitute.

