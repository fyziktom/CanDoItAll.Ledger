# Assumptions And Risks

## Assumptions

- The existing ledger node API remains the only ledger communication channel for AccountApp.
- The AccountApp nginx container can proxy IPFS API requests to the IPFS docker service, giving WASM a same-origin upload endpoint.
- The IPFS node API can serve uploaded images through `api/v0/cat` or gateway-compatible URLs available to the browser.
- The existing BO transaction builder can create typed BOs once state JSON and object type are constructed correctly.
- Alice and Bob import files from the previous AccountApp setup remain available under `docker/demo-keys`.

## Critical Path Risks

- If the IPFS docker stack does not become healthy, attachment upload and image rendering proof cannot close.
- If the WASM build excludes all IPFS driver code, a browser-specific upload driver must live in AccountApp or a WASM-compatible shared service.
- If typed BO payloads are only parsed by UI branches and not the shared caster layer, gallery proof may pass narrowly while details/actions remain generic.
- If all generic BO families are tested only by fixture/state strings instead of real signed browser transactions, Alice/Bob validation will be weak.
- If the AccountApp database already consumed demo spendable outputs, tests may need to create fresh funding/token sources before browser BO creation.

## Validation Risks

- Browser proof must verify both DOM data and screenshots; a non-empty card count is not enough.
- File upload proof must use a real browser file input and then verify IPFS CID-backed data appears in the BO detail/gallery.
- Same-origin nginx proxy configuration must be tested from inside the running AccountApp, not only through host curl.
- Typed parser tests need malformed/legacy cases so a shallow `BusinessObjectMediaPayload` fallback cannot pass.

## Reopen Triggers

- Reopen SB01 if later browser upload cannot reach IPFS or image URLs do not render in AccountApp.
- Reopen SB02 if any in-scope BO family falls back to `GenericBusinessObjectView` without typed fields in gallery/detail.
- Reopen SB03 if Playwright shows wizard inputs do not persist into staged/signed transactions.
- Reopen SB04 if Alice-created BOs do not appear for Bob after send/transfer, or if attachment previews disappear after refresh.
