# Original Request

```text
Use [$candoitall-bundle-workflow](C:\\Users\\dell\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to solve this as new bundle: 
IMPORATNT: You are preparing bundle only now. Do not do implementation yet!

Main goal:
Adding blazor wasm PWA demo application for account. app will use just API and transactions assembly and signatures and keys are on the side of the WASM app. API is just to get transactions info and list for address and broadcast of the signed transactions.

Architect notes:
- This is large task, but we have already good base for it. You must detaily map all components/drivers that are ready and identify what and where we are missing some implementation to make this happen. Use xlsx for detailed checklists and planning
- AccountApp will use WASM and PWA. It must communicate with node only via API. The comminication will be primarilly about getting transactions or specific transaction data for account or broadcast transaction. Signature and assembly of transaction will happen on side of the AccountApp.
- For signatures of the transactions we will have to use browser API because dotnet does not support Cryptography lib in WASM. In VEFramework we had same situation. But we were able to use bouncy castle trough the NBitcoin to use ECDSA. but here we do not want those depenencies. For SHA2556 we need browser api anyway. I cloned VEFramework here: "C:\repositories\VirtualEconomyFramework". You can analyze "C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEDriversLite\Security" where you can see that we have conditional compilation based on if it is WASM or not. For non WASM we still want to use Cryptography and other inbuild .net libs. WASM is kind of necessary hack until Microsoft solve cryptography for WASM too. 
- the cryptography is necessary to encrypt the private key, that must be stored in browser IndexedDb. You can see it in C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor in services. Because we have shared Razor components libraries for SSR and WASM solutions we must allow both ways based on the configuration.
- Things around the encryption are superimportant first step. Wihtout that does not make sense to continue. It must be validated and tested and covered with unit tests to be sure that it works as we expect. 
- Based on that the backup and import of the account can be solved. You do not need to made up UX userstories. You can whole flow copy from the VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor and its example "C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor.Demo.VENFTApp.Client"
It contains all steps around creating account, backup, import. Just the implementation was poor and you can use better approach for implementation of those userstories. You must not skip anything.
- After load of account VENFTApp started loading data from the API to get list of transactions and utxos that it can use for transactions. Lots of parts of the UI we already have in our Razor componentts for this Ledger accounts. But they might be connected via SSR too tight, so you must analyze it and make them flexible so we can use same components in SSR and WASM. 
```
