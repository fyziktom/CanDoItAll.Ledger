# CanDoItAll.Ledger.Simulator

BO/project simulator runtime moved from `CanDoItAll.Economy.Simulator`.

This landing keeps `CanDoItAll.Economy.Simulator` namespaces temporarily to make
the physical migration safe. The namespace compatibility plan in
`docs/architecture/namespace-compatibility.md` owns the rename and sunset.

This project must not import Economy simple accounts, Smith experiments,
scenario packs, or WebGL observer evidence.
