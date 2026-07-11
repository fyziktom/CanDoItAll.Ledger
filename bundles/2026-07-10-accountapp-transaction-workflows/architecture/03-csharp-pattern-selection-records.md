# Pattern Selection Records

## PSR-001: Catalog

- Force: the enum has twelve members, nine visible, two supported but hidden, and one separately handled.
- Selected: immutable catalog mapping every mode to family, label, icon, flow kind, and disposition.
- Rejected: another static tab list, because silent omissions already exist.
- Test seam: enumerate the enum and assert exactly one catalog entry/disposition per value.

## PSR-002: Thin Facade/Shell

- Force: the composer owns unrelated form details and shared actions.
- Selected: keep `TransactionComposerShell` as a thin compatibility facade that delegates active mode UI to cohesive components.
- Rejected: partial class and additional render-fragment methods; both retain the monolith.
- Test seam: render each form directly; shell tests cover catalog selection and submit gating only.

## PSR-003: Strategy-like Source Choice

- Force: BO selection can come from current account, entered address, or direct id.
- Selected: a closed source-strategy value inside `BusinessObjectSourcePicker`, with one filter/lookup projection per strategy.
- Rejected: interface hierarchy; three UI-local deterministic projections do not justify runtime polymorphism.
- Test seam: render each strategy with a fixed object list and assert candidates/empty/errors.

## PSR-004: Validation Result

- Force: every form must publish immediate readiness and messages to a common shell.
- Selected: immutable validation result with errors/warnings and review facts.
- Rejected: EditForm/DataAnnotations on the shared record; rules vary by mode and many fields are optional in other modes.
- Test seam: pure rule tests and DOM submit-disabled assertions.

