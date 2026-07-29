# CanDoItAll.Ledger.Cli

Command-line driver for Ledger transaction, node, and development operations.

New commands must expose explicit Ledger behavior, preserve machine-readable
failure results, and avoid bypassing SDK or engine validation.

Validate changes with the solution build, Ledger tests, and at least one local
command smoke.
