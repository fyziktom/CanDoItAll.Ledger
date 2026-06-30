# Normalized Requirements

| Requirement | Raw notes | Statement | Proof |
|---|---|---|---|
| REQ-01 | RN-01 | Rebuild and start `CanDoItAll.IPFS` docker services with persisted storage and prove IPFS API/NodeControl health. | Docker transcript, health probes, add/cat smoke. |
| REQ-02 | RN-01 | AccountApp WASM must have a configured IPFS upload service and same-origin docker proxy path for file uploads. | AccountApp build, docker config diff, browser upload proof. |
| REQ-03 | RN-02, RN-05 | The supported generic BO families must be enumerable and creatable: post, image/media, event, ticket, payment, purchase/order, receipt, invoice, message, product, profile/contact. | Unit tests plus Playwright creation/gallery matrix. |
| REQ-04 | RN-03, RN-04 | BO parsing must use a reusable typed schema/builder/caster architecture, not ad hoc gallery-only JSON branches. | Source assertions and parser tests with positive and malformed inputs. |
| REQ-05 | RN-02 | Gallery cards and detail dialogs must show correct type-specific data, tags, description/text, and attachment previews for every in-scope generic BO family. | Component tests and Playwright DOM/screenshot proof. |
| REQ-06 | RN-03, RN-05 | VEFramework UX lessons must inform flow structure while excluding IoT, Xray, Coruzant, and other domain-specific NFTs. | Inventory and traceability review. |
| REQ-07 | RN-06 | Real Alice/Bob WASM validation must create BOs, upload attachments where applicable, send/transfer at least one BO between accounts, refresh, and verify both accounts see the expected state. | Playwright transcripts and screenshots against docker apps. |

## Literal Scope Handling

The user requested "all types of the BOs we have to create them." This bundle interprets "all" as all generic BO creation families in the target inventory above. Domain-specific VEFramework-only families are explicit scope exceptions and must not be silently added.
