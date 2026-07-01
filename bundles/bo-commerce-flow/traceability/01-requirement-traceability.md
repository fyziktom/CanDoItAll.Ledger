# Requirement Traceability

| Raw note | Requirement | Subbundle | Evidence path | Closure status |
| --- | --- | --- | --- | --- |
| `RN-01` | `REQ-001` | `SB01` | `bundle://proof/SB01/manifest.md` | `Closed` |
| `RN-02` | `REQ-002`, `REQ-007` | `SB02`, `SB04` | `bundle://proof/SB02/manifest.md`, `bundle://proof/SB04/manifest.md` | `In progress` |
| `RN-03` | `REQ-003`, `REQ-004` | `SB02`, `SB03` | `bundle://proof/SB02/manifest.md`, `bundle://proof/SB03/manifest.md` | `In progress` |
| `RN-04` | `REQ-005` | `SB03`, `SB05` | `bundle://proof/SB03/manifest.md`, `bundle://proof/SB05/manifest.md` | `Not started` |
| `RN-05` | `REQ-006` | `SB04`, `SB05` | `bundle://proof/SB04/manifest.md`, `bundle://proof/SB05/manifest.md` | `Not started` |
| `RN-06` | `REQ-008` | `SB03`, `SB05` | `bundle://proof/SB03/manifest.md`, `bundle://proof/SB05/manifest.md` | `Not started` |
| `RN-07` | `REQ-009` | `SB02`-`SB05` | `bundle://reviews/01-execution-report.md` | `In progress` |

## Source-to-Requirement Coverage

- Every raw note owns at least one normalized requirement.
- Absolute wording `Each NFT/BO`, `must`, `automatically`, `always`, and `Do not do exact same implementation` is preserved as acceptance criteria or scope exception.
- Unsupported VEFramework domain-specific NFTs are explicitly out of scope in `requirements/01-normalized-requirements.md`.
