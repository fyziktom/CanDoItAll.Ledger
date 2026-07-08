# QA Prompt

Review the bundle closure against the raw request.

- Verify console and SSR demos both use shared transaction APIs.
- Verify UTXO merge creates selected vin and exactly one plain vout.
- Verify UTXO split creates selected vin and requested plain outputs plus positive change only.
- Verify BO outputs use one carrier token and zero main amount.
- Verify token minting does not mint main currency.
- Verify tests and builds have durable transcripts.
- Verify proof manifests cite existing artifacts with portable `repo://` and `bundle://` references.
- Verify raw notes `N001` through `N006` are closed honestly.
