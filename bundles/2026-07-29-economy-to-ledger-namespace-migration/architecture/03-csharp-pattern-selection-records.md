# C# Pattern Selection Records

## PSR-001: Direct Identity Migration

- Problem force: repository split left obsolete public project and namespace
  identities, including a future `Ledger.Ledger` collision.
- Selected approach: direct, atomic project/assembly/namespace rename with an
  explicit one-to-one map.
- Rejected alternative: wrapper/facade or type-forwarding shim. No compatibility
  package was requested, and retaining the old product namespace would leave the
  split incomplete.
- New types/projects: none.
- Dependency direction: unchanged.
- Testability seam: identity guard, graph comparison, full tests, and Docker
  composition smoke.
- Proof the old owner is gone: no active old project/namespace/config identity
  outside the approved historical/compatibility allowlist.

No Builder, Factory, Adapter, Strategy, Facade, or service-locator pattern is
justified for a pure identity migration.
