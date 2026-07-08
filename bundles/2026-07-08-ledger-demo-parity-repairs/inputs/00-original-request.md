# Original Request

User request from the current Codex thread:

> use [$candoitall-bundle-workflow](C:\\Users\\dell\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to create bundle for reafactoring and repairs in our other ledger related apps in our repo. we worked hard on the wasm accountapp and it is working well now.
> we improved some types of transactions, assure that they creates proper vouts and also how BO works (1 token without main currency amount). We improved split and merge transaction and things like that. in wasm account app it works fine. we can take it as example how is it ok.
>
> analyze our console app demo and blazor ssr demo to assure that they will work the same and will be updated.
> when bundle is ready implement it and test it.

## Raw Notes

| Raw note | Exact wording | Normalized requirement ids | Owning subbundle |
| --- | --- | --- | --- |
| `N001` | `wasm accountapp ... is working well now ... take it as example how is it ok` | `R001`, `R002`, `R003`, `R004` | `SB01`, `SB02` |
| `N002` | `assure that they creates proper vouts` | `R002`, `R003`, `R004`, `R006` | `SB01`, `SB02` |
| `N003` | `how BO works (1 token without main currency amount)` | `R003`, `R005`, `R006` | `SB01`, `SB02` |
| `N004` | `We improved split and merge transaction` | `R002`, `R004`, `R007` | `SB01`, `SB02` |
| `N005` | `analyze our console app demo and blazor ssr demo ... updated` | `R008`, `R009`, `R010` | `SB01`, `SB02` |
| `N006` | `when bundle is ready implement it and test it` | `R011`, `R012` | `SB01`, `SB02` |
