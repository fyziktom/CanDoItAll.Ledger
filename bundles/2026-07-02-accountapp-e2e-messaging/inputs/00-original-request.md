# Original Request

## 2026-07-02 User Request

> remove from tags Neblio. this has nothing to do with neblio.
>
> then improve the messaging in the app. the messaging works if it is possible to find on api some transaction of receiver that will contains receiver public key (assure that signed tx contains signer pubkey). then message BO content must be encrypted with shared secret generated with ECDH. then receiver when it receives has public key of sender (directly in tx) and own private key to calc hash and encrypt the data. Same loaded content to ipfs must be encrypted same way.
> Even in background those are BOs we must have UI simplified so people understand it as messaging. it must have layouts and UX more like email/chat style. It cannot decrypt all messages. it just show the nickname of address or receiver/sender based on bookmarks if present. if user open message then it is decrypted. attachments are decrypted also only on request.
> use [$candoitall-bundle-workflow](C:\Users\dell\.codex\skills\candoitall-bundle-workflow\SKILL.md) to solve this as new bundle. It is complex and you must use [$imagegen](C:\Users\dell\.codex\skills\imagegen\SKILL.md) to create proposals of messaging UI. deeply analyze our implementation of EDCH and solve it for wasm via webcrypto browser api.
> you must not just somehow "hack" it. it must be fully working and secure p2p e2e encrypted communication. Same as you can see in VEFramework "C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\NFT\MessageNFT.cs"
>
> you must validate that it is working in our docker wasm apps to send messages between bob and alice and back. with and without attachments. Assure that also attachments are correctly encrypted same as message data.

## Interpretation Notes

- `EDCH` is treated as `ECDH`.
- The VEFramework `MessageNFT.cs` file is a behavior reference, not a dependency to import.
- Generated UI proposal images are planning artifacts only and cannot count as shipped proof.
