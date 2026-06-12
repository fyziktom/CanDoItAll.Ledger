# Simple Economy Package Readiness

## Package Boundaries

The simple economy surface is split so generic packages can be consumed without cognitive-memory, UI, ledger, or BusinessObject dependencies.

| Package | Purpose | Expected references |
|---|---|---|
| `CanDoItAll.Economy.Accounts` | Account, asset, balance, reservation contracts. | none |
| `CanDoItAll.Economy.Markets` | Market, order, clearing, trade contracts and price calculation. | Accounts |
| `CanDoItAll.Economy.Sandbox` | Generic scenario definitions, isolated run scopes, demo provider contracts, and general demo provider. | Accounts, Markets |
| `CanDoItAll.Economy.Memory` | Cognitive-memory economy DTOs, score vectors, policies, resource maps, and driver. | Accounts, Markets |
| `CanDoItAll.Economy.Memory.Sandbox` | Adapter from memory-economy plans to generic simple-economy scenarios and memory demo provider. | Memory, Sandbox |
| `CanDoItAll.Economy.Investments` | Claim, obligation, position, lifecycle, activation coordination, fund subscription, and tax batch contracts for the investment V1.1 runtime. | Accounts, Markets |
| `CanDoItAll.Economy.Investments.Persistence.EFCore` | EF Core investment store adapter for contracts, activation records, lifecycle events, and tax batches. | Investments, EF Core |
| `CanDoItAll.Economy.Persistence.EFCore` | EF Core account, market, clearing, and scenario-run persistence. | Accounts, Markets, Sandbox |
| `CanDoItAll.Economy.Persistence.PostgreSql` | PostgreSQL provider setup for EF Core persistence. | Persistence.EFCore |
| `CanDoItAll.Economy.Components` | Legacy aggregate Blazor package. | Includes legacy ledger UI plus simple market/memory demo UI. |

`CanDoItAll.Economy.Components` remains an aggregate package for now. Backend package-boundary tests prevent the generic packages from inheriting its ledger or BusinessObject dependencies.

## Account Operation Foundation

Mutating account operations are operation/journal backed. `IssueAsync`, `TransferAsync`, `ReserveAsync`, `CommitReservationAsync`, `ReleaseReservationAsync`, market order reservation, and market settlement create durable `AccountOperation` records and `JournalLine` rows.

The operation envelope carries operation type, status, idempotency key, source module, requested-by, correlation/causation IDs, external reference, metadata JSON, and timestamps. Journal lines project the balance movement by account, asset, balance bucket, direction, and amount.

Use `AccountCommandContext` with an `IdempotencyKey` when a caller may retry a command:

```csharp
var context = new AccountCommandContext(new IdempotencyKey("workflow-42-issue"));
await accounts.IssueAsync(new IssueAssetRequest(account.Id, credit.Id, 100m, "seed", CommandContext: context));
await accounts.IssueAsync(new IssueAssetRequest(account.Id, credit.Id, 100m, "seed", CommandContext: context));
```

The replay returns the original operation result instead of applying the balance change again. Calls without a command context are ordinary non-idempotent operations.

V2/V3 hardening adds scoped command fingerprints. `CanonicalAccountCommandFingerprintCalculator` exposes the same canonical payload hashing used by persistence. For commands that carry an idempotency key, the persistence layer stores source module, scope, canonical fingerprint algorithm/hash/version, replays exact matches, and rejects same-scope/same-key submissions whose normalized payload differs. Rejected conflicts are written as rejected audit operations without mutating value. The same key can be reused only when the caller deliberately changes scope, such as a different tenant, scenario, account, or market namespace.

Use `IAccountJournalVerifier` to audit the journal as the source of accounting truth:

```csharp
JournalVerificationResult result = await verifier.VerifyAsync(
    new JournalVerificationRequest(RebuildBalanceProjection: true));
```

The verifier checks operation journal shape, transfer balance, reservation bucket movement, reservation amount invariants, and whether balance projections match replayed journal lines. When `RebuildBalanceProjection` is true, the result includes reconstructed available/reserved balances so operators can compare stored state to journal replay.

## Lifecycle, Issuance, and Reservations

Accounts expose `Active`, `Suspended`, and `Closed` lifecycle status. Closing an account rejects non-zero balances, active reservations, and open market orders. Closed and suspended accounts remain queryable but cannot receive issue, transfer, or reservation mutations.

Issuance is policy controlled through `IAssetIssuancePolicy`. In-memory/demo registration uses `PermissiveTestAssetIssuancePolicy`; PostgreSQL registration installs `RestrictiveAssetIssuancePolicy` unless the host registers a more specific policy first. Assets can declare `AssetSupplyMode.AuthorizedIssuer` with explicit issuer account IDs for future claim or position assets.

Assets can also declare `MaxSupply`. EF persistence tracks issued supply on the asset row and rejects over-cap issues under the same mutation transaction. Rejected issue attempts with command context are recorded as rejected operations so audit readers can see why value did not move.

Reservations include investment-ready metadata: kind, owner operation ID, correlation ID, optional expiration, committed amount, released amount, remaining amount, purpose, and linked order or contract IDs. `ReservationQuery` provides filtered reads by account, asset, kind, linked contract, expiry, and active status. Collateral reservations require a linked contract or correlation context, and fund-subscription commits require matching linked-contract context. The enforced accounting invariant is `OriginalAmount = RemainingAmount + CommittedAmount + ReleasedAmount`.

`ExpireReservationsAsync` explicitly processes expired reservation holds and writes journal-backed expiry operations. This keeps scenario time deterministic: market clearing uses the cycle end time for order expiry, while account reservation expiry is driven by a caller-supplied cutoff.

## In-Memory Registration

```csharp
builder.Services.AddSimpleEconomyInMemoryPersistence("simple-market-sandbox-demo");
builder.Services.AddSimpleEconomySandbox();
builder.Services.AddMemoryEconomySandbox();
```

This registers account and market handlers, the scenario runner, the general demo provider, and the memory demo provider. Scenario runs are isolated by default and return the account, asset, and market IDs that a UI should display.

## PostgreSQL Registration

```csharp
builder.Services.AddSimpleEconomyPostgreSqlPersistence(
    builder.Configuration,
    "Economy:SimpleEconomy:PostgreSql");
builder.Services.AddSimpleEconomySandbox();
builder.Services.AddMemoryEconomySandbox();
```

The PostgreSQL provider configures `SimpleEconomyDbContext`, relational retry options, EF Core handlers, mutation coordination, and scenario-run persistence.

Investment persistence is registered separately so account storage and investment lifecycle storage stay explicit:

```csharp
builder.Services.AddInvestmentEfCoreStore(options =>
    options.UseNpgsql(connectionString));
```

PostgreSQL remains the target production relational provider. Local tests include a SQLite relational runtime fallback that applies the EF model, writes operation/journal rows, and proves duplicate idempotency submissions replay without duplicate balance changes. PostgreSQL hardening validation starts a Docker PostgreSQL container and fails unless relational concurrency, idempotency, settlement, and journal-corruption evidence passes.

## Demo Modes

Use `IDemoScenarioProvider` to discover package-level modes.

```csharp
IEnumerable<IDemoScenarioProvider> providers = services.GetRequiredService<IEnumerable<IDemoScenarioProvider>>();
IDemoScenarioProvider general = providers.Single(provider => provider.Descriptor.Id == "general");
IDemoScenarioProvider memory = providers.Single(provider => provider.Descriptor.Id == "memory");
```

The `general` mode runs the WHEAT/CREDIT market. The `memory` mode builds a cognitive-memory economy plan with scarce resources, internal/external/rule commons actors, maintenance-tax flows, commons-investment flows, attention orders, and assertions.

The sandbox UI now includes operation/journal transparency: recent account operations, journal lines, trade settlement groups, and settlement batch IDs are visible in the market workbench. Memory mode exposes policy version IDs, evaluation IDs, and capital proposal types for proof without moving memory-specific logic into generic packages. This is diagnostic output for demos and proof; backend packages remain UI-free.

## Scenario Isolation

`ScenarioResetPolicy.NewIsolatedRun` is the default. It creates a unique run namespace, keeps asset definitions reusable when identical, scopes accounts by owner ID, scopes markets by symbol, and records `ScenarioRunResult.AccountIds`, `AssetIds`, `MarketIds`, and `PrimaryMarketId` for display filtering.

`ScenarioResetPolicy.ReuseExistingByNaturalKey` is available for explicit compatibility scenarios. UIs should not use it as the default because repeated seeding can intentionally accumulate state.

## Memory Economy Governance Model

The memory driver models Cognitive Memory as an economic commons, not as a vector database fixture. The plan carries:

- scarce resources such as attention, context window, verification capacity, storage, consolidation time, rule complexity budget, and governance energy;
- internal Cognitive Memory actors, external Agent/Workflow actors, and Rule Commons actors as separate maps;
- score vectors instead of scalar-only ranking;
- nonlinear maintenance tax and attention-bid calculations that penalize rule complexity, conflict risk, enforcement energy, and collapse risk;
- typed maintenance, commons investment, and attention market policies;
- typed thought, commons, rule, and policy-version identifiers;
- structured tax, grant, attention-bid, and collapse-risk evaluation results.
- capital proposal signals for future loan, revenue-share, or fund-subscription adapters without executing those investments.

Rules and policies remain DTO-owned and package-local. Promote them to broader governance only with owner, lifecycle, validation cost, enforcement cost, simplification potential, and retirement conditions.

The memory sandbox adapter preserves governance purpose, policy version, and evaluation references as generic scenario metadata. The generic sandbox remains memory-agnostic; memory-specific display logic lives in the Components package.

## Investment-Readiness Contracts

`CanDoItAll.Economy.Investments` intentionally does not implement fund NAV, redemption, liquidation, underwriting, amortization engines, or automated payout schedulers. V1.1 implements only the hardening needed for safe account-backed activation, replay, lifecycle evidence, and tax batch idempotency:

- `Claim` for ownership, revenue-share, debt, governance, and collateral claims;
- `Obligation` for repayment, interest, revenue-share, maintenance, and collateral-return duties;
- `CashFlowSchedule` for recurring transfer intent;
- `InvestmentPosition`, `LoanTerms`, `FundTerms`, and `RevenueShareTerms` as contract skeletons.
- `InvestmentContractDraft`, claim/obligation/schedule drafts, lifecycle statuses, `InvestmentValidationRequest`/`InvestmentValidationResult`, and `DefaultInvestmentContractValidator` as validation-only primitives.
- `InvestmentActivationRecord`, `IInvestmentActivationCoordinator`, and lifecycle events for contract-idempotent activation and recovery.
- `TaxAssessmentBatch` and `ITaxAssessmentBatchStore` for replay-safe commons assessments.

Future thought-to-thought investment should reserve value through `ReservationKind.FundSubscription`, `LoanCommitment`, or `Collateral`, then settle through account operations and journal lines. Do not model long-term returns as one immediate transfer unless the economic contract truly has no ongoing claim or obligation.
