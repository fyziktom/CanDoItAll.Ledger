# Testability Plan

- Unit-test the policy helper with main-only and reusable-token workspaces.
- Unit-test clone transaction assembly with an original token-backed BO plus a separate reusable source token.
- Unit-test commerce dispatcher drafts for copy sale, receipt, and purchase token fields.
- Unit-test wizard/dashboard rendering where feasible through existing bUnit tests.
- Run focused test filters first, then broader `CanDoItAll.Ledger.Tests` if time permits.
- Capture docker AccountApp browser proof under `proof/SB03`.

