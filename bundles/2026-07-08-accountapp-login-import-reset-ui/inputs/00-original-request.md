# Original Request

```text
use [$candoitall-bundle-workflow](C:\\Users\\dell\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to solve this:

minor repair: look at screenshot. remove that top bar. it is useless. 

main task:
improve our login and create account and import account screens. 
look at "C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Components\Account"
especially ImportAccount, UnlockAccount and CreateAccount.
On "C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Pages\MyAccount.razor"
you can see that if there is no stored account it will offer create account or import account.
analyze also system to "remove" account. It is difficult procedure because we do not want to user do it accidentally, but there must be some way how to do reset. 
It is good to do import as steps. We have nice component for Steps in CanDoItAll.Components.BaseLib. Use that one. You can use CanDoItAll components mcp to guide around how to use those components. Prefer always use of our components.
we need just large screen now. do not waste time on small and medium screens.
You must do real testing with playwright mcp.
when you finish you must rebuild our dockered apps. 
```

Source screenshot copied to `bundle://inputs/screenshot-topbar-remove.png`.
