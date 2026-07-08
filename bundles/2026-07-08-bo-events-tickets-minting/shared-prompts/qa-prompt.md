# QA Prompt

Review the completed bundle against the original request.

- Confirm every raw note in `inputs/02-structured-input.md` is marked `Solved`, `Partially solved`, or `Not solved`.
- Confirm event/ticket date-time fields are not plain unstructured text fields unless a documented BaseLib/MCP blocker exists.
- Confirm event class, ticket class, ticket duration, status, and used-like fields use predefined options.
- Confirm create-mode event/ticket payloads include `mintAuthorAddress` equal to the current workspace account address.
- Confirm existing ticket event picker and multimint tests still pass.
- Review browser screenshots or recorded blocker against `references/ui-validation-questions.md` from the bundle execution skill.
