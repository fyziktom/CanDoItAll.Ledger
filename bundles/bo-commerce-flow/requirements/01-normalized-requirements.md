# Normalized Requirements

| Requirement ID | Requirement | Source notes | Owning subbundle | Proof |
| --- | --- | --- | --- | --- |
| `REQ-001` | BO Post long description must be captured from the wizard, serialized as structured payload text, parsed into typed views, displayed in details as long body, and visible in raw state. | `RN-01` | `SB01` | Unit round-trip, browser create/details/raw-state screenshot. |
| `REQ-002` | Payment, purchase, and receipt BO types must remain manually creatable from the menu while also having commerce-specific typed payload helpers for automated buy/dispatch flow. | `RN-02`, `RN-07` | `SB02` | Parser/builder tests and menu browser smoke. |
| `REQ-003` | BO sale terms must support not-for-sale, original sale, and copy/clone sale with price amount, asset, seller address, optional max clone count, listed timestamp, and status. | `RN-03` | `SB02`, `SB03` | Contract tests and Set Sale dialog proof. |
| `REQ-004` | Gallery/details/minting flow must allow setting, updating, clearing, and inspecting sale terms for owned BOs. | `RN-03` | `SB03` | Playwright open-state dialog screenshots and node state assertion. |
| `REQ-005` | A shareable Buy BO route must load listed BO data from the node API by object id, show sale details, require/unlock browser account when needed, and submit a signed payment BO transaction from buyer to seller. | `RN-04` | `SB03` | Browser Bob opens Alice link and payment tx accepted. |
| `REQ-006` | Seller account refresh must detect pending payment BOs, validate sale terms, enforce original/copy mode and max clone limit, dispatch transfer or clone, and mark payment outcome idempotently. | `RN-05` | `SB04` | Unit tests for duplicate refresh/max clone/original sold plus Alice/Bob real flow. |
| `REQ-007` | Commerce dispatch must produce receipt/purchase/payment records that link buyer, seller, original BO, payment tx, dispatch tx, sale mode, price, and related object id. | `RN-02`, `RN-06` | `SB02`, `SB04` | Node raw-state assertion and dashboard click-through proof. |
| `REQ-008` | Account dashboard must list BO payments and receipts with clear status and click-through to record details and related BO details. | `RN-06` | `SB03`, `SB05` | Playwright dashboard/dialog screenshots. |
| `REQ-009` | The design must keep VEFramework UX semantics while using modern C# contracts, adapters, services, and tests instead of copying implementation. | `RN-07` | `SB02`-`SB05` | Source audit and anti-stub audit. |

## Scope Exceptions

- Domain-specific VEFramework NFT kinds such as IoT, Xray, Dogecoin, and BTC payment paths are outside this bundle.
- Main-currency purchase flow is required. Non-main-currency commerce is optional unless existing transaction builders make it low-risk.
