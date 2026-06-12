# Investment Phase 1

Investment Phase 1 adds a narrow account-backed investment runtime for demos, tests, and package validation. It is intentionally not a full lending, fund NAV, redemption, liquidation, or automated payout system.

## Package Boundary Matrix

| Package | Owns | May reference | Must not reference |
|---|---|---|---|
| `CanDoItAll.Economy.Investments` | Drafts, validation, in-memory store, activation coordinator, lifecycle events, fund subscription V1.1, tax/commons batch contracts | Accounts, Markets | EF Core, PostgreSQL, Blazor, UI components, node host, BusinessObjects ledger |
| `CanDoItAll.Economy.Investments.Persistence.EFCore` | Durable investment contracts, activation records, lifecycle events, and tax assessment batches | Investments, EF Core | UI, Memory, Ledger, BusinessObjects, node host |
| `CanDoItAll.Economy.Memory` | Memory economy signals and capital proposals | Accounts, Markets | Investments, persistence, UI |
| `CanDoItAll.Economy.Memory.Sandbox` | Demo adapter from memory capital proposals to investment drafts | Memory, Sandbox, Investments | EF Core, PostgreSQL, node host |
| `CanDoItAll.Economy.Components` | Demo UI for operations, claims, obligations, schedules, balances, and audit evidence | Accounts, Markets, Memory sandbox, Investments | Direct balance mutation or persistence writes |

## Public API Quickstart

```csharp
IInvestmentService investments = provider.GetRequiredService<IInvestmentService>();

var draft = new InvestmentContractDraft(
    ContractId.New(),
    ContractStatus.Draft,
    investor.Id,
    investee.Id,
    credit.Id,
    PrincipalAmount: 30m,
    ClaimDrafts: [new ClaimDraft(ClaimKind.RevenueShare, 0.20m, null)],
    ObligationDrafts: [new ObligationDraft(ObligationKind.RevenueSharePayment, 6m, "revenue-share", null)],
    MetadataJson: null,
    CashFlowSchedules:
    [
        new CashFlowScheduleDraft(credit.Id, 6m, TimeSpan.FromDays(30), startsAtUtc, null, "revenue-share-schedule")
    ]);

await investments.CreateDraftAsync(new CreateInvestmentDraftRequest(draft));
await investments.ApproveAsync(new ApproveInvestmentRequest(draft.ContractId, "reviewer", "approved"));
InvestmentActivationResult activation = await investments.ActivateAsync(
    new ActivateInvestmentRequest(
        draft.ContractId,
        new AccountCommandContext(new IdempotencyKey("investment-activation-1"), SourceModule: "investments")));
```

Activation reserves principal from the investor and commits it to the investee through `IAccountHandler`. V1.1 activation is contract-idempotent even without a caller key: reserve and commit operations derive deterministic per-contract account idempotency keys, and the activation coordinator rejects caller-key fingerprint conflicts before account mutation. The investment store records claims, obligations, schedules, activation evidence, lifecycle events, and account operation IDs, but never writes balances, reservations, operations, or journal lines directly.

For durable investment state, register the EF Core adapter separately from account persistence:

```csharp
builder.Services.AddInvestmentEfCoreStore(options =>
    options.UseNpgsql(connectionString));
```

Fund subscriptions require either an explicit `ContractId` or a command context idempotency key. When a key is supplied, the service derives the same contract id for replays and rejects same-key/different-payload requests before creating a new draft.

Tax commons V1.1 uses `TaxAssessmentBatch` with `(Scope, PeriodKey)` idempotency. Batch transfer plans derive account idempotency keys from the batch id and assessment index so re-executing the same batch replays operations instead of double-charging.

## Economic Resource Map

Scarce resources: principal, maintenance credits, attention, verification capacity, journal integrity, idempotency namespace, package complexity, and governance energy.

Actors: investor account, investee account, fund account, thought sponsor, young thought, commons treasury, maintenance sink, account handler, investment service, tax planner, UI reviewer, and journal verifier.

Incentives: investors need durable claims; young thoughts need bounded funding; the commons needs maintenance contributions; reviewers need audit evidence that value movement did not bypass account invariants.

Externalities: bad activation can duplicate money movement, strand claims without operations, or create governance debt by promoting memory-specific rules globally. V1 contains those externalities with local adapters, account idempotency, and explicit non-goals.

## Dual Commons Map

Internal Cognitive Memory commons: thought accounts, memory capital proposals, maintenance tax plans, and revenue-share claims between thoughts.

External Agent/Workflow commons: demo providers, UI, tests, account handler, investment service, and journal verifier consuming memory proposals through a sandbox adapter.

The boundary is explicit: `Memory` does not reference `Investments`; only `Memory.Sandbox` adapts a proposal into an investment draft.

## Rule Lifecycle And Complexity Budget

Investment V1 adds local rules for activation, fund subscription, and tax/commons planning. Each rule has a narrow owner and retirement path:

| Rule | Owner | Scope | Benefit | Cost | Sunset condition |
|---|---|---|---|---|---|
| Activation must reserve then commit through `IAccountHandler` | Investments package | All investment activation | Prevents direct balance mutation | Two account operations per activation | Replace only if account API gains a higher-level atomic investment primitive |
| Activation coordinator locks by contract and tracks activation fingerprint | Investments package | Activation V1.1 | Prevents concurrent duplicate movement and failed-write retries | Adds lifecycle and activation-record maintenance | Retire only if durable account/investment transaction boundary replaces it |
| Fund subscription uses deterministic contract identity, `ReservationKind.FundSubscription`, and matching contract correlation | Accounts + Investments | Fund subscription V1.1 | Prevents ambiguous fund commits and orphan replay drafts | Requires contract-id/idempotency discipline | Replace with persisted fund ledger in a later NAV bundle |
| Tax assessment batches use `(scope, period)` idempotency | Investments package | Tax commons V1.1 | Prevents repeated assessments from double-charging | Requires batch lifecycle storage | Replace with governed tax policy engine in a later bundle |
| Memory proposal adapter lives in sandbox | Memory sandbox | Demo integration | Keeps Memory independent from Investments | Extra adapter type | Retire if a dedicated cross-package integration package replaces it |
| Tax/commons V1.1 plans transfers before execution | Investments package | Demo taxes and commons contributions | Makes externalities visible | Planner plus executor surface | Replace with governed tax policy engine in a later bundle |

Complexity budget: V1 accepts local vertical complexity in `Investments` and `Memory.Sandbox`, but rejects horizontal coupling into `Memory`, EF Core, PostgreSQL, Blazor internals, node host, or BusinessObjects ledger.

## Nonlinear Scoring Notes

Memory capital proposals keep score-vector inputs from Cognitive Memory. The adapter does not collapse those vectors into a universal investment score. It uses proposal kind, principal, expected return, and risk metadata as gated inputs, with hard gates supplied by investment validation and account idempotency.

## Collapse Early-Warning Indicators

- investment activation creates account operations without journal lines;
- claims exist without reserve and commit operation IDs;
- memory-specific investment policy moves from sandbox into `Memory`;
- idempotency conflicts do not write rejected audit operations;
- tax/commons plans execute without `IAccountHandler`;
- full loan servicing or fund NAV logic appears in Phase 1 code.

## Dream-Mode Regeneration Responsibilities

Dream mode should treat investment artifacts as governance debt candidates: audit claim usefulness, stale schedules, rule overlap, proposal fragmentation, and whether local adapter rules should be merged, parameterized, or retired before any global memory policy is introduced.
