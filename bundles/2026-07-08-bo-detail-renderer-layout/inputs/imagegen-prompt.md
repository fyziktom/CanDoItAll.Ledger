# ImageGen Prompt

Mode: built-in `image_gen` tool.

Saved output: `bundle://evidence/imagegen/bo-detail-layout-proposal.png`

Reference screenshots copied into the bundle:

- `bundle://evidence/references/accountapp-gallery.png`
- `bundle://evidence/references/accountapp-bo-details-fixed.png`

Prompt supplied:

```text
Use case: ui-mockup
Asset type: desktop product UI proposal board for CanDoItAll Ledger BO detail dialog first tab
Primary request: Create a polished large-screen-only UI proposal board showing how the first Overview tab of a Business Object detail dialog should look for BOs with and without DataItems. This is a practical Blazor app mockup, not marketing art.

Existing app visual references to preserve:
- Gallery card: white rectangular card, small 6-8px radius, thin border, purple accent strip at top and a purple bottom info bar; top thumbnail area is either a square icon tile or a real thumbnail image; card body has a strong BO title, short description, small status badges, object id/date meta row, and tag chips.
- Current detail dialog: large white modal on gray overlay, top header with title and close button, subtitle below, tab pills near top: Overview, Actions, Ledger details. Overview currently has a large left DataItems preview hero and a right column with object type, title, description, facts, sale panel, and a Data items rail. For BOs with no DataItems, remove the empty left gallery hero and use that space for better information layout.

Layout proposal requirements:
- Make a single 16:9 desktop mockup board with four labeled mini-dialog variants arranged in a 2x2 grid.
- Variant 1: Event BO without DataItems. Hide DataItems gallery. Use a strong event overview: icon tile, name, event date, startsAt, endsAt, location, organizer, status, eventClass, authorLink, videoLink, musicInLink, mintAuthorAddress. Include readable fact chips and one wide description/text area.
- Variant 2: Ticket BO without DataItems but related to an Event BO. Hide DataItems gallery. Use ticket overview: ticketNumber, holder, ticketClass, ticketDuration, seat, used, usedAt, status, eventId, eventAddress, eventDate, location. Include a related Event card using the same gallery card style with thumbnail/icon, title, badges, object id/date, and tags. The related Event card should fill the available empty space.
- Variant 3: Payment and Purchase BOs without DataItems but related to purchased BO. Hide DataItems gallery. Use commerce overview: amount, currency, payer/buyer, payee/vendor/seller, paymentObjectId, purchaseObjectId, targetObjectId, targetVersion, targetOutPoint, status, saleMode, paymentTxId, dispatchTxId, deliveredObjectId, receiptObjectId. Include a related Sold BO gallery card with thumbnail.
- Variant 4: Receipt BO without DataItems, plus Media BO with DataItems. Receipt: receiptNumber, amount, currency, paidBy, receivedBy, issuedAt, targetObjectId, paymentObjectId, deliveredObjectId, dispatchTxId, status. Show related Delivered BO gallery card. Media example should still keep the large DataItems preview hero and a compact data item strip, proving the layout only hides the hero when DataItems are empty.

Style and constraints:
- Dense operational app UI, restrained, quiet, professional.
- Use the existing app feeling: white surfaces, navy/dark text, muted borders, purple accent only where the current cards use it, small material-style icons, compact badges.
- Do not use mobile layouts, decorative gradients, hero marketing treatment, nested cards, huge typography, or custom illustration.
- Keep all text within containers; use realistic spacing; no overlapping elements.
- No code, no annotations about implementation, no marketing copy, no watermark.
```

