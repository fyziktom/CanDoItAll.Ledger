# CanDoItAll.Ledger.Components

Reusable Razor components and UI services for Ledger accounts, transactions,
node administration, business-object ownership, commerce, content, and SDK
workflows.

The project consumes shared visual primitives from published
`CanDoItAll.Components.*` packages. Finance-specific rendering and interaction
belong here; general-purpose design-system primitives belong in the Components
packages.

The component library may depend on Ledger and BusinessObject contracts. It
must not become a transaction execution or persistence layer.
