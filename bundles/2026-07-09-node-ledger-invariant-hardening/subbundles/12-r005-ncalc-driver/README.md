# SB12 — NCalc driver isolation

Status: Complete — 2026-07-10

## Owned requirement

RQ-029 / R-005: retain NCalc now but isolate it behind a replaceable contract/driver.

## Acceptance checklist

- `IAccountRuleExpressionEvaluator` and result contracts remain repository-owned;
- BusinessObjects has no NCalc package or NCalc type reference;
- dedicated driver project is the only project with the NCalc package;
- rule execution receives the contract instead of constructing NCalc statically;
- SDK/composition registers the NCalc driver;
- compatibility vectors preserve arithmetic, comparison, coercion, error, and missing-variable behavior;
- a fake evaluator can drive rule orchestration without NCalc.

## C# Architecture Impact

New `CanDoItAll.Economy.Rules.NCalc` driver project implements the BusinessObjects-owned contract. Rule orchestration becomes instance-based.

## Boundary Ownership

BusinessObjects owns rule semantics and result shapes; the driver owns external parser/evaluator adaptation only.

## Dependency Direction

Rules.NCalc -> BusinessObjects; SDK/composition -> Rules.NCalc. BusinessObjects must not reference the driver. Cycle risk: none if the contract remains inward.

## Pattern Decision

Adapter for external SDK isolation. Rejected: service locator, global mutable evaluator, wrapper that leaves NCalc construction in BusinessObjects, or immediate replacement.

## Testability Contract

Direct driver compatibility vectors, fake-evaluator orchestration proof, DI resolution smoke, and package/reference assertions.

## Partial Class Policy

No new partial class.

## Architecture Proof Required

Before/after references, package graph, direct tests, source assertion, anti-stub audit, architecture review.

## Progression gate

SB13 may close only if NCalc exists solely in the driver and existing account-rule flows pass.

## Closure evidence

- `CanDoItAll.Economy.BusinessObjects` contains the evaluator/result contracts and a fail-clear unavailable implementation, with no NCalc package or type reference.
- `CanDoItAll.Economy.Rules.NCalc` is the only project declaring the `ncalc` package and implements the adapter plus DI extension.
- Account rule execution receives the evaluator through constructors; fake-evaluator orchestration passes without NCalc.
- Compatibility vectors and driver DI resolution pass. NU1701 remains an explicitly accepted driver compatibility warning until a replacement is selected.
