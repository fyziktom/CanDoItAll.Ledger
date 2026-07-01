# Structured Input

## Raw Notes

| Note ID | Exact note | Initial interpretation | Must preserve |
| --- | --- | --- | --- |
| `RN-01` | `I added long description in BO Post bo:post:new-ledger-post 2f9109c552bd0679b84e94a93fbbb9eb51e370436b97ad6c0b3ff840d70ddedb on alice address. long description is missing. maybe just in ui, but I cannot see it in raw data too. maybe UI wizard does not take that field.` | Long BO post body must be captured by wizard, serialized in BO state, parsed into typed views, visible in details, and observable in raw state. | Concrete Alice object id/hash, possible UI and raw-data failure modes. |
| `RN-02` | `Payment, purchase and receipts are created automatically during process of buying different BO. We still want them in menu too, but their main purpose is for automated dispatch of the buying original or clone of BO.` | Manual creation remains, but commerce records must also be produced by the buy/dispatch lifecycle. | Automated creation is primary; menu creation remains. |
| `RN-03` | `Each NFT/BO in gallery or during minting is possible to setup for sale as original or as copies (with specified max clones). It can be setup in the NFT/BO dialog details too.` | Sale terms need first-class state: not for sale, original sale, copy sale, price, asset, max copies. | Gallery, minting, and details entry points. |
| `RN-04` | `Then I can share link to some NFT/BO and someone can open it in form of BuyNFT/BO page... BuyNFT loads the data... if user has account it is on same domain, so it can access private key (or they are asked to login) and then they can buy it.` | Add a buy route/page that loads a listed BO by id from the node API and uses the browser vault to submit a signed payment BO transaction. | Share-link, local account, unlock fallback, API-only node communication. |
| `RN-05` | `when seller opens account (or if account is opened and refreshing) the NFT/BO is dispatched/cloned based on mode. We have our 2L semi-smart contracts that might help...` | Seller refresh should detect pending BO payments and idempotently dispatch original or clone based on sale terms. | Open account and auto-refresh trigger; optional 2L contracts if they improve design. |
| `RN-06` | `In Account dashboard you can see in VEFramework that there are nft payments, nft receipts and always with click to dialog of detail of the NFT/BO.` | Dashboard must expose payment and receipt records with click-through to details and related BO details. | Dashboard visibility and detail dialog. |
| `RN-07` | `Do not do exact same implementation, just keep the UX logic. original VEFramework implementation is functionally good, but in modern C# you can do it more elegant.` | Reuse UX semantics, not old implementation style. | Modern contracts, typed services, idempotency, tests. |

## Constraints

- AccountApp remains WASM/PWA and communicates with the node only through API calls and signed transaction broadcast.
- Browser account signing and private keys stay on the WASM side.
- Shared components must work for SSR/node and WASM adapters.
- VEFramework is a behavior reference, not a dependency or code source.
- Existing manual BO creation for payment, purchase, receipt must remain available.
- Dispatch must be repeat-safe; duplicate refresh or page reload must not duplicate sold originals or clones.

## Desired Evidence

- Targeted `dotnet test` coverage for payload parsing, commerce contracts, buy request creation, dispatch idempotency, clone limits, and long text round-trip.
- `dotnet build` or solution-level build of affected projects.
- Playwright validation against running node and WASM AccountApps with Alice/Bob: list, share link, buy, seller refresh dispatch, buyer gallery, payment/receipt dashboard, dialogs, long raw state.
- Screenshots saved under `bundle://evidence/` or `bundle://proof/SBxx/browser/`.
