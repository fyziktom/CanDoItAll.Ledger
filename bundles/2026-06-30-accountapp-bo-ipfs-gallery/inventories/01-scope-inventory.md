# Scope Inventory

## In-Scope BO Families

| Family | VEFramework analogue | Required BO type aliases | Gallery-critical fields |
|---|---|---|---|
| Post | `PostNFT` | `post`, `bo-post` | name, description, text, author, link, tags, data items |
| Image/media | `ImageNFT`, `MusicNFT` | `image`, `media`, `music`, `bo-image` | name, description, image/media item, tags |
| Event | `EventNFT` | `event`, `bo-event` | name, description, start/end/location, tags |
| Ticket | `TicketNFT` | `ticket`, `bo-ticket` | name, event id/name, ticket code/status, venue, date |
| Payment | `PaymentNFT` | `payment`, `bo-payment` | payer, receiver, amount, asset, reference/status |
| Purchase/order | `OrderNFT`, product/purchase flow | `purchase`, `order`, `purchase-order`, `bo-purchase` | seller, buyer, items, total, status |
| Receipt | `ReceiptNFT` | `receipt`, `bo-receipt` | receipt number, payer, seller, amount, payment reference |
| Invoice | `InvoiceNFT` | `invoice`, `bo-invoice` | invoice number, seller, buyer, due date, amount, status |
| Message | `MessageNFT` | `message`, `bo-message` | sender, receiver, subject, body/text |
| Product | `ProductNFT` | `product`, `bo-product` | sku, name, description, price, image/data items |
| Profile/contact | `ProfileNFT` | `profile`, `contact`, `bo-profile` | display name, nickname/company, bio, image/data items |

## Out Of Scope

- `IoTDeviceNFT`, `IoTMessageNFT`, and device/source-code families.
- `XrayNFT`, `XrayImageNFT`, and imaging device parameter forms.
- Coruzant-specific article/profile records.
- Hardwario-specific helpers and domain protocols.

## Existing Local Support

- Attachment model exists through `BusinessObjectDataItem`.
- Creation wizard already stages BO transactions.
- Gallery and detail components already render media payloads, but typed generic families need parser/rendering support.
