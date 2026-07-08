# Requirements

| Id | Requirement |
| --- | --- |
| BO-TOKEN-001 | When an account has a reusable token source, new normal BO creation should default to one unit of that token instead of `Main`/no-token. |
| BO-TOKEN-002 | Payment BOs must be one-unit token outputs and must also carry the main payment amount. |
| BO-TOKEN-003 | Receipt and purchase BOs created by dispatch automation must be one-unit token outputs. |
| BO-TOKEN-004 | Copy-sale dispatch must give the buyer clone a fresh one-unit token output without stripping the seller's original BO token. |
| BO-TOKEN-005 | Explicit main-only creation must remain available when no reusable token source exists or the caller chooses main. |
| BO-TOKEN-006 | Commerce UI must expose payments separately from receipts/purchases so sales and buy records are easy to inspect. |
| BO-TOKEN-007 | Tests and docker browser proof must demonstrate real transaction details where BO outputs show token amount `1`, not `none`. |

