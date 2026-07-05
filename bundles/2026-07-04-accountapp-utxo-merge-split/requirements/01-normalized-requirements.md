# Normalized Requirements

| ID | Requirement | Observable success |
| --- | --- | --- |
| R001 | The home send dialog must expose Send, Merge, and Split forms as reusable Razor components. | Source structure shows independent form components consumed by the dialog/shell, and component tests can render forms directly. |
| R002 | Merge must let the user select multiple eligible UTXOs from available account outputs. | UI shows eligible output rows with selection controls and running selected total. |
| R003 | Merge must exclude BO unique outputs and all token/permission/rule outputs. | Domain tests and UI data source only include outputs where `BusinessObject is null` and `Token is null`; excluded outputs appear nowhere as selectable Merge/Split inputs. |
| R004 | Merge must submit a transaction whose vin is all selected UTXOs and whose vout is exactly one output. | Transaction detail proof for Merge shows selected input count and `Out 1`; output address is self by default or the selected external address. |
| R005 | Split must let the user select one or more eligible UTXOs as inputs. | UI supports multi-select input rows and total calculation. |
| R006 | Split must support equal-amount output creation. | User can enter output count and equal amount, and submitted tx has that many outputs with that amount. |
| R007 | Split must support specific output amounts. | User can edit individual output rows, and submitted tx preserves those amounts. |
| R008 | Split must reject output totals that exceed selected input totals. | Validation/test proof rejects overspend before or during assembly. |
| R009 | Plain Send must continue to work while using the shared component structure. | Existing/new tests and browser proof submit a normal payment successfully. |
| R010 | Transaction details must remain usable with many inputs or outputs on a large desktop screen. | Screenshots and DOM assertions show no overlay, clipping, or lateral overflow in details dialogs with many-row Merge/Split transactions. |
| R011 | Docker stack must be rebuilt and restarted for UI validation. | Transcript records docker compose down/build/up or equivalent compose apply proof before Playwright. |
| R012 | Final closure must include proof in real submitted transactions. | Execution report links transaction IDs, details screenshots, and raw vin/vout assertions. |
