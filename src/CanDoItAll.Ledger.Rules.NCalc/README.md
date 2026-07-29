# CanDoItAll.Ledger.Rules.NCalc

Invariant-culture NCalc adapter for CanDoItAll Ledger account-rule
expressions.

Use this package when account or transaction workflows need numeric or boolean
expressions behind the `IAccountRuleExpressionEvaluator` abstraction. NCalc
types and exceptions remain isolated inside the adapter.

## Requirements

- .NET 10

## Install

```powershell
dotnet add package CanDoItAll.Ledger.Rules.NCalc
```

## Evaluate A Budget Rule

```csharp
using CanDoItAll.Ledger.Rules.NCalc;

var evaluator = new NCalcAccountRuleExpressionEvaluator();
var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
{
    ["spent"] = 72.50m,
    ["budget"] = 100m
};

var result = evaluator.EvaluateBoolean(
    "spent <= budget",
    variables);

if (!result.Succeeded)
{
    throw new InvalidOperationException(result.Error);
}
```

## Register

```csharp
using CanDoItAll.Ledger.Rules.NCalc;

builder.Services.AddNCalcAccountRuleExpressionDriver();
```

This supplies `IAccountRuleExpressionEvaluator` unless the application already
registered another implementation.

## Evaluation Notes

- Numbers and numeric strings use invariant culture.
- Parse and evaluation failures return `Succeeded == false` with an error
  message. Do not consume `Value` before checking the result.
- The adapter supports boolean and decimal result contracts; application code
  should keep rule variables explicit and typed.
- The package does not impose a time limit or expression-complexity quota.
  Bound the size and complexity of untrusted expressions before evaluation.
- Rule evaluation is one policy input. Node authorization and complete
  transaction validation still determine acceptance.

See the
[BusinessObjects package](https://www.nuget.org/packages/CanDoItAll.Ledger.BusinessObjects),
[SDK](https://www.nuget.org/packages/CanDoItAll.Ledger.Sdk), and
[full source](https://github.com/fyziktom/CanDoItAll.Ledger).

This package is part of the [CanDoItAll ecosystem](https://aicandoitall.com)
and uses the repository's MIT License.
