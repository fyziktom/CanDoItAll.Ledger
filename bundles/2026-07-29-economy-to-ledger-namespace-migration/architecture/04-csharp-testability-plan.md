# C# Testability Plan

## Characterization

- Capture the pre-change project graph and active identity counts.
- Run the existing build/test entry points before or immediately after the
  atomic rename if no compilable intermediate state exists.

## Positive Proof

- Build `CanDoItAll.Ledger.slnx` in Release.
- Run all `CanDoItAll.Ledger.Tests`.
- Assert expected target assemblies/namespaces/project paths exist.
- Start the renamed Node/DemoSeeder/AccountApp Compose graph and verify real
  AccountApp routes.

## Negative Proof

- The identity validator fails against a deliberate fixture containing
  `namespace CanDoItAll.Economy.Node;`.
- No unapproved old namespace, assembly, project path, config prefix, service, or
  active file name remains.
- No `CanDoItAll.Ledger.Ledger` output is produced.
- Before/after normalized project edges are equal and cycle-free.

## Composition Smoke

- Compose configuration resolves.
- The Node is healthy and answers a Ledger API/health route.
- Alice and Bob AccountApps load at `1600x1000` without rename-related failed
  requests or console errors.
