# Original Request

Source: User feedback on 2026-06-30 in `C:\repositories\CanDoItAll.Ledger`.

```text
it is on good way. here are things to tune. it is large so do new bundle for it.
-I added long description in BO Post bo:post:new-ledger-post 2f9109c552bd0679b84e94a93fbbb9eb51e370436b97ad6c0b3ff840d70ddedb on alice address. long description is missing. maybe just in ui, but I cannot see it in raw data too. maybe UI wizard does not take that field.
- now when we have those basic types analyze UX flow of them in VEFramework. now you added their creating just with standard wizard. but if you will analyze VEFramework you will find, that for example Payment, purchase and receipts are created automatically during process of buying different BO. We still want them in menu too, but their main purpose is for automated dispatch of the buying original or clone of BO. the VEFramework solves that whole flow, but implementation is ugly. you must propose better one.
this is very complex, but important part we must implement. so you must deeply design the architecture.
Each NFT/BO in gallery or during minting is possible to setup for sale as original or as copies (with specified max clones). It can be setup in the NFT/BO dialog details too.
Then I can share link to some NFT/BO and someone can open it in form of BuyNFT/BO page as you can see it here: "C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Pages"
the BuyNFT loads the data of that NFT/BO and also if user has account it is on same domain, so it can access private key (or they are asked to login) and then they can buy it. In that case the nft payment is send and when seller opens account (or if account is opened and refreshing) the NFT/BO is dispatched/cloned based on mode. We have our 2L semi-smart contracts that might help to make this logic more elegant and flexible.
In Account dashboard you can see in VEFramework that there are nft payments, nft receipts and always with click to dialog of detail of the NFT/BO.
You must map this whole flow and implement it. Do not do exact same implementation, just keep the UX logic. original VEFramework implementation is functionally good, but in modern C# you can do it more elegant.
```
