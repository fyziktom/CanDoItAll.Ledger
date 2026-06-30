# Original Request

User feedback received on 2026-06-30:

> great. it is much better now. 
> we are getting closer. here are few things you must improve:
> - account must have some refreshing loop. not so fast because txs are not comming too often. But now it looked it did not loaded new tx until i refreshed it. analyze it and improve this. It is good to have some caching mechanism for BO and tx details. if account pull from node api list of newest txs (for example just 10 newest for account) and it see they are same, it does not loading anything. if there are some new, it will reload account utxos and new txs. in case of utxsos some of them might be missing (was used meanwhile) and most of them will be same. 
> If you loook in "C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\Neblio" you can see that we had that kind of mechanism there. Implementation was terrible, but logic meaningfull. Part of it was also caching in localstorage (or better should be via some interface and cache provider based on if it is wasm or ssr). in wasm localstorage helped to keep info about same tx (on blockchain it will be same forever). In case of own txs it makes large sense. in case of ssr it is possible to use cache in hdd, memory, etc. thats why adapter/interface and factory will be better approach.
> - notification in UI after send of tx showed also as component with content and did not disappeared automatically:
> "MainPayment Payment accepted at ledger sequence 11.
> Auto refresh Workspace refreshed for Alice demo account."
> it should show up as notificationservice popup only. 
> - in large screen app is not using whole width. it must use always full width. we need it now for large screen desktop only.
> do new bundle for this and use [$candoitall-bundle-workflow](C:\Users\dell\.codex\skills\candoitall-bundle-workflow\SKILL.md) 
