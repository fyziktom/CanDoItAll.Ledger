# Raw Request

```
use [$candoitall-bundle-workflow](C:\\Users\\dell\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) and [$csharp-architecture-governor](C:\\Users\\dell\\.codex\\skills\\csharp-architecture-governor\\SKILL.md) and other Csharp related skills to preapre bundle with improvements of our UI components for ledger related apps. 
the main goal is create good and flexible system of display of BOs data. 
Some parts they might have common, but usually they have some specific function too. 
thats why we have Renderers in our Ledger Components. 
They are missing another types of BOs like events, tickets, payments, receipts, etc. 
We should improve the automatic layouting of the first tab in BO dialog detail. If there are no DataItems in BO it makes no sense to have DataItems gallery as hero and we should hide it and show some visually nice layout of other informations that are in BO. In case of that BO is related to some another BO (like in case of ticket has some event or purchase/receipt has some BO that was bought) that BO with thumbnail would be nice to show there too to fill up the available space (use same card as we have in BOs gallery page). 
Use [$imagegen](C:\\Users\\dell\\.codex\\skills\\imagegen\\SKILL.md) to create proposal of nice looking layout that contains all necessary info and in case of related BO also that, for each type of BO where we have customized renderer. you must provide to ImageGen all information about what fields does it need and also example of how our actual card with thumbnail looks like and how our BO detail dialog looks like with image gallery and info that in some cases we need it without image gallery.
when bundle is ready implement it and test with screenshots and analysis against proposal if it is close enough. Do not create some customized css. always prefer our compoonents from BaseLib and tailwind styling. we need large screen only. do not do small and medium screens. 
when it is all done rebuild our dockered apps and test it is working there. 
```

