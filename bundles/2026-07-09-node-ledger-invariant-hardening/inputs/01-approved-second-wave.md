# Approved second implementation wave

Literal user direction on 2026-07-10:

> R-001 to R-005 are good findings repair/improve it.

> make ncalc isolated. we will not remove it directly because I am not sure about good replacement yet, but it makes sense to isolate it under some contract/driver so we can replace ncalc later.

Scope interpretation:

- implement every residual R-001 through R-005 rather than leaving an ADR-only proposal;
- retain NCalc behavior for now;
- remove NCalc package/type leakage from the BusinessObjects project;
- preserve canonical v1 hashes and existing human token IDs;
- make version/protocol upgrades explicit and test both compatibility and fail-closed behavior.
