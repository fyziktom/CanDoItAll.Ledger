# Original Request

> Main goal:
> Rename "Economy" to "Ledger" namespace. I means that for example
> "CanDoItAll.Economy.Node" will be "CanDoItAll.Ledger.Node", etc.
>
> Context:
> - We used to have this as part of CanDoItAll.Economy repo. But then we split
>   them. Here we have things related to Ledger and things around
>   Ledger/Blockchain. We must rename it.
> - It is not easy rename, because you must rename, folders, projects, all
>   connections in slnx, change namespaces in classes, etc. It is lots of work
>   so use candoitall-bundle-workflow to process this run.
> - We have project named CanDoItAll.Economy.Ledger. This will need new name
>   because it cannot be "CanDoItAll.Ledger.Ledger". Find somethingg like
>   LedgerCore or something more suitable, because we have Core project too.
> - You must improve also related documentation, tools, tests, etc.
> - Use apply-candoitall-shared-standards to assure about keeping all standards
>   for our repos. Add also info, that simulator is still in WIP and just
>   experimental for now.
> - You must do proper testing after all changes to assure that it works as
>   before. Test to rebuild and restart and test our dockerized accountapps too
>   to assure it is working.

## Preserved Scope Words

- `must rename`
- `folders, projects, all connections in slnx, change namespaces in classes`
- `documentation, tools, tests`
- `simulator is still in WIP and just experimental`
- `proper testing`
- `rebuild and restart`
- `dockerized accountapps`
