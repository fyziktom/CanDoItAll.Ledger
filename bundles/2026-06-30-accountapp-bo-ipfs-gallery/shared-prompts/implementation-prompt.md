# Implementation Prompt

Execute the active subbundle only. Reopen the raw notes it owns, confirm prerequisites, make the smallest correct implementation, capture the listed proof under `proof/SBxx/`, update the execution report and traceability rows, and stop if the progression gate cannot honestly pass.

Preserve these boundaries:

- No VEFramework runtime dependencies.
- No ledger signing outside WASM for AccountApp.
- No IoT/Xray/Coruzant/device-specific BO families.
- No gallery-only parser shortcuts; typed data must flow through shared schema/caster/rendering code.
