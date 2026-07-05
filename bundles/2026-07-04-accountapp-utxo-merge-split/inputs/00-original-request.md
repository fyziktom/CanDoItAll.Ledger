# Original Request

Use [$candoitall-bundle-workflow](C:\\Users\\dell\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to solve this:

In home page we have send dialog. It contains tab with "Merge" and "Split" transactions. They are not correct. Those types of payments helps to merge or split utxos. For example when I have 50 utxos each with some small amount like 0.4, 3, ...etc I can merge them (there must be some nice selector of available utxos, except BOs uniquie ones) and then based on selected ones it will calc output one utxo amount. when it send it, it takes all selected utxos as vin and vout is just one to yourself or selected external address.
Split is oposite of this. You can selecte some inputs (even multiple of them) and then user create how much outputs wants and if with same amounts (for example each output will have 0.5amount) or each with specific amount.

Split is usually helpful for parallel operations, where user will first prepare for example 100 available source utxos with split tx and then it can do 100 txs in parallel because of being sure of the available utxos for vins of those parallel txs.

All those forms for split, merge, send tx that we have in dialog in tabs must be as reusable components. Assure that they are properly isolated, they have good bases and helpers.

do proper testing via UI of all types of those transactions and assure that it works as we expect.
rebuild and restart our dockers for those tests.
if it is not working correctly you must repair it and test it again until it is working and you have proof in transactions about it.
Assure with screenshots, that transaction dialog details looks correct even with multiple inputs or outpus. in merge/split txs there can be plenty of them. It might disturb/overflow/overlay UI. If it is not correct you must repair it. We need large screen desktop app only. do not waste time on small and medium screens.
