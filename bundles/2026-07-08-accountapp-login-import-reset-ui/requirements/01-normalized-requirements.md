# Normalized Requirements

| Requirement | Source | Acceptance |
| --- | --- | --- |
| `R001` Remove redundant unlocked top bar | `N001` | `Home.razor` no longer renders `account-app__workspace-frame`; unlocked large-screen browser screenshot starts with the shared VEF workspace shell, not the AccountApp header from the screenshot. |
| `R002` Improve locked account entry for large desktop | `N002`, `N003`, `N004`, `N007` | Locked `/` presents create/unlock/import/reset in a stable large-screen layout with readable headings, field labels, status text, and no overlapping content at `1600x1000` or equivalent large desktop. |
| `R003` Import uses shared step component | `N003`, `N006` | Import account UI uses BaseLib `Steps` and `StepsItem`, has separate material/password/finish stages, and enables finish only when selected import mode and passwords are valid enough to attempt. |
| `R004` Preserve AccountApp-native import modes | `N003`, `N006` | Existing encrypted backup JSON import and pasted key pair import remain available and call the existing session import methods. No VEF/Neblio import logic is introduced. |
| `R005` Add guarded local reset/remove account | `N005` | Locked UI exposes a reset path that deletes the local browser vault through `IAccountVaultStore.DeleteAsync`, clears unlocked/session state, and requires both acknowledgement and typing a confirmation phrase before the destructive button enables. |
| `R006` Prefer shared components | `N006` | Components MCP evidence is recorded; layout uses BaseLib `Steps` plus appropriate `Grid`, `Stack`, and/or `FormSection` rather than building the whole flow from raw wrappers. |
| `R007` Real browser proof | `N008` | Playwright proof records route, viewport, actions, DOM/style assertions, and screenshots for locked setup, import steps, reset guard, and unlocked no-topbar state. |
| `R008` Rebuild dockered apps | `N009` | Docker compose rebuild command succeeds and transcript is recorded before final closure. |
