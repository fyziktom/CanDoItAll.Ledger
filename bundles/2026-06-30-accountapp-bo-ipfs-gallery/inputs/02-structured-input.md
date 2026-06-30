# Structured Input

## Raw Notes

| Note | Exact scope |
|---|---|
| RN-01 | Rebuild `C:/repositories/CanDoItAll.IPFS` and start it in docker so images and BO attachments can be loaded. |
| RN-02 | Test all BO types we have to create and assure they have correct data in gallery, including posts and tickets. |
| RN-03 | Analyze VEFramework NFT drivers and Blazor NFT/Minting components to understand practical generic NFT/BO flows. |
| RN-04 | Architect a better flexible/robust parser and builder model than the VEFramework implementation. |
| RN-05 | Implement generic families only: posts, tickets, events, payments, purchases/orders, receipts, messages, and similar generic records. Exclude IoT, Xray, and other domain-specific NFTs. |
| RN-06 | Execute and test all, with real validation between Alice and Bob in WASM AccountApp. |

## Scope Inventory

In-scope generic families:

- post
- image/media
- event
- ticket
- payment
- purchase/order
- receipt
- invoice
- message
- product
- profile/contact-style record

Out-of-scope domain-specific VEFramework families:

- IoT device/message records
- Xray imaging records and device parameters
- Coruzant-specific article/profile records
- hardware/software/mechanical source code NFT families
