# Implementation Prompt

Implement this bundle one subbundle at a time. Read the root README, phase plan, feedback matrix, and active subbundle README before code edits. Preserve the API-only AccountApp split: the node provides read/broadcast APIs, while transaction assembly and signing remain in WASM. Do not couple browser localStorage directly to shared SSR paths; use an interface and provider registration.

After each subbundle, update `reviews/01-execution-report.md`, record transcript paths under `proof/SBxx/transcripts`, and update raw-note closure status only when proof exists.
