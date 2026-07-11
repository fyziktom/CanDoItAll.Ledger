# Architecture Checkpoints

## ACP-01 After SB02

- Catalog covers every enum member.
- No project reference or partial class added.
- Extracted validation and selector components have direct tests.
- Composer is a thin coordinator and does not gain new mode-specific markup.
- Decision: unlock SB03/SB04 only on pass.

## ACP-02 After SB04

- BO origin filtering/selection logic lives outside composer.
- Clone/Merge/Split Payment forms are directly testable.
- Existing domain builders remain the sole transaction construction path.
- Decision: unlock AccountApp closure only on pass.

## ACP-03 Final

- Project diff confirms dependency direction unchanged.
- No partial/service locator/TODO/NotImplemented production path.
- Browser proof and component tests agree on supported/delegated mode coverage.

