# BusinessObject Account Layer

The BusinessObject account layer sits above the ledger and treats the ledger as the canonical database-like substrate. It owns account ergonomics, key-backed signing, BO transaction assembly, BO casting, renderer selection, and reusable Blazor UI surfaces. Domain simulation libraries should build on this layer rather than calling ledger internals directly.

## Projects

```text
src/CanDoItAll.Economy.BusinessObjects
src/CanDoItAll.Economy.Components
examples/CanDoItAll.Economy.Components.Demo
integrations/CanDoItAll.Modules.Economy
```

`CanDoItAll.Economy.BusinessObjects` references Core and Ledger only. `CanDoItAll.Economy.Components` references the BO layer and `CanDoItAll.Components.BaseLib`; the BaseLib reference is a project reference until it is available as a NuGet package.

## Services

Register the generic BO/account services with:

```csharp
services.AddBusinessObjectLedgerLayer();
```

This registers:

- `ILedgerGateway` for submitting assembled transactions.
- `ILedgerReadClient` for address-scoped UTXOs, balances, BO holdings, and transaction summaries.
- `BusinessObjectTransactionBuilder` for deterministic transaction assembly.
- `IAccountInputSigner` for account-key input signatures.
- `IEconomyAccountHandler` for replaceable account refresh orchestration.
- `IBusinessObjectCastRegistry` with default generic and account-settings casters.

`InProcessLedgerGateway` is the current local/test adapter. A node/API adapter should implement the same gateway/read contracts without changing account or UI code.

## Accounts

`BusinessObjectAccount` wraps an `IAccountKeySource`, exposes the account address, assembles signed BO transactions, submits them through `ILedgerGateway`, and reads address state through `ILedgerReadClient`.

Supported helper flows:

- create/mint BO
- transfer BO
- update BO
- close BO
- clone BO
- merge BOs
- split payment with the BO carried by the first receiver output
- subaccount access under a parent account

The layer keeps the no-fee ledger model. Token-bearing merge is intentionally rejected until a token merge policy exists. Invalid helper requests such as negative amounts, same-id clone, duplicate-source merge, and non-positive split-payment outputs are rejected before assembly/signing.

## Account Auto-Refresh

Applications can start a cancellable account refresh loop from the account itself or through the replaceable `IEconomyAccountHandler`.

```csharp
var options = new AccountRefreshOptions
{
    Interval = TimeSpan.FromSeconds(2),
    MaxTransactionCount = 100,
    IncludeTransactionData = true
};

IEconomyAccountHandler handler = serviceProvider.GetRequiredService<IEconomyAccountHandler>();
handler.NewTransactionsObserved += (_, args) =>
{
    foreach (LedgerTransactionSummary transaction in args.NewTransactions)
    {
        Console.WriteLine($"New transaction for {args.Account.Address}: {transaction.TxId}");
    }
};

await using IAccountRefreshHandle refresh = await handler.StartAutoRefreshAsync(
    account,
    castRegistry,
    options,
    cancellationToken);
```

`StartAutoRefreshAsync` performs the first refresh before returning, so the caller can await startup and read `account.CurrentRefreshSnapshot`. The returned `IAccountRefreshHandle` exposes `RunningTask`, `Options`, and `StopAsync`; disposing the handle cancels the loop. A single refresh tick is available through `BusinessObjectAccount.RefreshAsync(...)` or `IEconomyAccountHandler.RefreshAsync(...)`.

The initial refresh seeds the transaction baseline without reporting historical transactions by default. Set `EmitExistingTransactionsOnInitialRefresh = true` when a consumer intentionally wants the first snapshot to be emitted as new.

`IEconomyAccountHandler` is registered with `TryAddScoped`, so applications can register their own implementation before calling `AddBusinessObjectAccountLayer` or `AddEconomySdkWithNodeDriver`. The default handler manages one running refresh per account identity, can stop a single account or all accounts, and routes subaccount refresh events through the parent account.

## Account Rules

Account rules are SDK-level "semi smart contracts". They are not node-executed scripts. The node validates only that a rule token was created with the correct permission source and output shape; an application must explicitly opt into execution through account refresh options.

Rule data is stored as a BusinessObject payload with `TransactionDataItemType.Rule`, content type `application/vnd.candoitall.rule+json`, and a managed `rule` token on the same output. The rule token must be output `0` of a permissioned rule-token mint transaction. Rule creation is authorized by a `permission` token whose action is `create_rule_token`; if the permission amount is greater than one, output `1` returns the remaining permission amount to the source address.

```csharp
var rule = new AccountRuleDefinition(
    "cashpool-sweep",
    "rules.company",
    Enabled: true,
    new AccountRuleTrigger(AccountRuleTriggerKind.AccountSnapshot),
    new Dictionary<string, string> { ["limit"] = "1000" },
    new[] { "account_main > limit" },
    new[]
    {
        new AccountRuleAction(
            AccountRuleActionKind.Payment,
            ReceiverAddress: cashpoolAddress.Value,
            AmountExpression: "account_main - limit")
    },
    AccountRuleProcessingOptions.Default);

await account.CreateRuleTokenAsync(new RuleTokenCreateRequest(rule), cancellationToken);

AccountRefreshResult result = await account.RefreshAsync(
    new AccountRefreshOptions
    {
        RuleExecution = new AccountRuleExecutionOptions
        {
            Enabled = true,
            MaxActionsPerRefresh = 5
        }
    },
    cancellationToken);
```

The NCalc context uses flat variable names. Common variables include `account_address`, `account_main`, `utxo_count`, `business_object_count`, `transaction_count`, `now_unix`, `rule_id`, `rule_namespace`, `rule_execution_id`, rule variables such as `limit`, token balances as `token_<token-id-with-safe-characters>`, and trigger BO state as both `bo_<property>` and `trigger_<property>`. JSON property names are normalized to rule-friendly snake case, so a BO field named `supplierAddress` is available as `bo_supplier_address`. Missing variables fail the rule evaluation instead of silently becoming zero.

`AccountRuleTriggerKind.NewTransaction` is supported by account refresh deltas. Transaction rules receive variables such as `tx_id`, `tx_kind`, `tx_status`, `tx_unix`, `tx_output_main`, `tx_account_output_main`, `tx_account_input_count`, and `txdata_<property>` when transaction data is included.

Supported action kinds are `Payment`, `TokenTransfer`, `TransferBusinessObject`, `UpdateBusinessObject`, and `MarkProcessed`. Receiver fields can be literal ledger addresses or variable names such as `bo_supplier_address`. BO update and transfer state can use JSON templates with unquoted placeholders, for example `{"paidAtUnix":{{now_unix}},"processedByRuleId":{{rule_id}}}`. Object updates merge template output into the existing BO state, preserving original fields unless explicitly overwritten.

Trigger BO rules skip objects whose processed property is already true. `MarkProcessed` now writes the processed flag plus `processedAtUnix`, `processedByRuleId`, and `ruleExecutionId`; the submitted transaction id remains in `AccountRuleExecutionResult.TxId`. If `MarkTriggerProcessed` is enabled, a successful trigger rule updates the BO back to the same account unless the rule already ran an explicit `MarkProcessed` action or a trigger BO update/transfer action. Set `KeepProcessedBusinessObject` to `false` when the trigger BO should be closed after it is marked processed.

## Refresh Cache Semantics

Refresh caches BusinessObject views by a ledger-state key made from object id, type, schema version, version, owner, operation, data hash, item type, and current outpoint. If the key is unchanged, the existing `IBusinessObjectView` instance is reused and casters are not called again. When a BO is spent or transferred away, it disappears from the refreshed snapshot; when a new or changed BO appears, it is cast once and added to the snapshot.

## Casting

BO casting is DI-driven. Higher layers can register custom `IBusinessObjectCaster` implementations with a priority above the default generic caster:

```csharp
services.AddBusinessObjectCasting();
services.AddScoped<IBusinessObjectCaster, PurchaseOrderCaster>();
```

Casters receive `BusinessObjectCastSource` with object type, schema version, owner, version, data hash, data JSON or external reference, operation, ledger context, and created time. Typed views implement `IBusinessObjectView`.

Default cast targets:

- `GenericBusinessObjectView`
- `AccountSettingsBusinessObjectView`

## Time Provider

All transaction timestamps and BO view `ViewedAt` values come from `ITimeProvider`. Simulation libraries can inject a manual or scenario-specific time provider to move ledger/account behavior forward without relying on the system clock.

UI models also carry time explicitly through `BusinessObjectWorkspaceModel.ViewedAtUtc`, `BusinessObjectEditorModel.EffectiveTimeUtc`, and renderer `ViewedAtUtc` parameters.

## Components

Register renderer services with:

```csharp
services.AddBusinessObjectComponentRenderers();
services.AddScoped<IBusinessObjectRendererProvider, PurchaseOrderRendererProvider>();
```

Use the workspace component from a host page:

```razor
<EconomyAccountWorkspace Workspace="workspace" />
```

The component library includes account summary, BO list/detail, BO editor shell, transaction composer/history, subaccount list, and renderer host components. Components use BaseLib layout/action primitives first and avoid server-only APIs so the same RCL can be hosted from SSR or WASM.

## Renderer Injection

`BusinessObjectRendererHost` resolves an `IBusinessObjectRendererRegistry` from DI and falls back to default generic/settings renderers if no registry is registered. Custom renderer providers return a component type through `BusinessObjectRendererDescriptor`; the host uses `DynamicComponent`, so new BO renderers do not require edits to generic components.

The demo app proves:

- custom purchase-order renderer selection
- account-settings renderer selection
- generic fallback renderer selection
- wide and narrow responsive layouts

## Styling

The component RCL has a Tailwind entry point at:

```text
src/CanDoItAll.Economy.Components/Tailwind/input.css
```

BaseLib output CSS is provided by the `CanDoItAll.Components.BaseLib` NuGet package static web assets. App hosts should include `_content/CanDoItAll.Components.BaseLib/css/output.css` before the Economy stylesheet. Economy-specific classes remain layered under `cda-economy-*`, and runtime proof CSS is available at:

```text
src/CanDoItAll.Economy.Components/wwwroot/css/economy-components.css
```
