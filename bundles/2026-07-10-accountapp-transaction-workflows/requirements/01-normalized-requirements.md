# Normalized Requirements

| ID | Requirement | Observable proof |
|---|---|---|
| R001 | Replace the nine-pill flat navigation with an understandable transaction-family chooser and focused active flow. | Browser screenshot and DOM assertions show family/operation choices and one active form. |
| R002 | Create shared transaction primitives for flow shell, steps, validation, BO selection, amount/balance entry, and review. | Independent component files and direct BUnit tests; old composer shrinks to orchestration. |
| R003 | Create uses guided inputs, inline validation, and review; invalid JSON or required fields block submit. | Negative/positive component tests and browser proof. |
| R004 | Transfer, Update, and Close select from loaded owned BOs, preload source data, and validate receiver/state/confirmation. | Card selection changes draft and review; invalid paths block submit. |
| R005 | Clone is a wizard with `My objects`, `From address`, and `Direct object id` source strategies, result cards, preload, target configuration, and review. | Step-by-step component and browser proof for all three strategies, including no-result/invalid-id states. |
| R006 | BO Merge is composer-visible and supports two distinct compatible source selections, target configuration, validation, and review. | Negative duplicate/incompatible proof plus positive draft proof. |
| R007 | BO Split Payment is composer-visible and supports BO selection, editable recipient outputs, total validation, and review. | Negative empty/invalid total proof plus positive draft proof. |
| R008 | Main Payment shows available main balance, validates positive/bounded amount and receiver, and provides review. | Component/browser invalid and valid states. |
| R009 | Token Transfer shows available tokens/balances, validates token, amount, receiver, and required main amount, and handles zero-token accounts. | Populated BUnit proof and empty AccountApp proof. |
| R010 | UTXO Merge/Split preserve existing eligible-selection behavior and block invalid empty or overspent submissions. | Existing tests remain green; new readiness tests and browser assertions pass. |
| R011 | Every declared transaction mode is either directly composer-supported or explicitly delegated with a visible entry/reason; no silent unsupported enum remains. | Coverage test enumerates all modes and disposition. |
| R012 | Every form uses available workspace data for preload/selection and shows explicit empty/not-found/loading states. | Component tests and browser screenshots. |
| R013 | Submit is enabled only when the active form is valid; validation messages are accessible and specific. | DOM `disabled` assertions and alert/text checks for each mode. |
| R014 | AccountApp WASM is visually inspected for every flow at 1920x1080, 1024x768, and 390x844, including dialog clipping/layering. | Browser analytics rows, screenshots, and computed geometry assertions. |
| R015 | Any defect found during form validation is repaired and revalidated before closure. | Execution report records fail/fix/pass cycles. |
| R016 | Imagegen proposals exist separately for the chooser, each simple form, and each multi-step wizard step. | `bundle://inputs/ui-proposals/manifest.md` maps each proposal to a flow/step and final implementation decision. |

