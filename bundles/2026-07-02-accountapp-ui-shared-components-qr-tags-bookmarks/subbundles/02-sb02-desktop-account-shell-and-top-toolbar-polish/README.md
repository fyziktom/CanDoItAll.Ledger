# SB02 Desktop Account Shell And Top Toolbar Polish

## Status

- `Completed`

## Objective

Replace the post-login card stack with a compact large-screen AccountApp command frame that uses notifications, icons, tooltips, and a top profile/account identity region.

## Success Criteria

- Successful unlock/create/import shows via `NotificationService`.
- Persistent success cards no longer push the economy workspace downward.
- Account, address, endpoint, signing state, profile/avatar, and primary actions are available in one compact top frame bar.
- Top-level actions are icon-first/minimal and have tooltip labels.
- Screenshot proof shows materially reduced vertical pre-workspace space.

## Covered Inputs

- Screenshot complaint about cards pushing the hero/workspace down.
- NotificationService requirement for login confirmation.
- Minimal icon button and tooltip requirement.
- Profile/account card requirement.
- Large-screen-only focus.

## Prerequisites

- SB01 for QR action components if QR buttons are added in this phase.
- Existing AccountApp unlock/create/import flows working locally.

## Exact Source References

- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor`
- `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppSession.cs`
- `repo://examples/CanDoItAll.Economy.AccountApp/Layout/MainLayout.razor`
- `repo://examples/CanDoItAll.Economy.AccountApp/wwwroot/css/app.css`
- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspaceAdapterHost.razor`
- `bundle://inputs/artifacts/accountapp-after-login-current.png`
- `bundle://inputs/artifacts/venft-profile-home-reference.png`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.BaseLib\Components\Navigation\Toolbar.razor`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.BaseLib\Components\Navigation\PageHeaderActionButton.razor`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.BaseLib\Components\Feedback\TooltipTarget.razor`

## Deliverables

- Refactored AccountApp unlocked shell.
- Notification-backed success flow for unlock/create/import/load/refresh where appropriate.
- Top identity/profile/account region.
- Compact action toolbar with tooltips.
- Reduced or removed local shell CSS where BaseLib components can replace it.
- Before/after screenshot comparison notes.

## Dependency Impact

- SB05 uses the new top toolbar placement for settings backup entry.
- SB07 depends on this phase to satisfy the primary screenshot complaint.
- QR buttons from SB01 can be added to address/hash identity actions here or completed in SB06 depending on sequencing.

## Validation Depth

- `Critical UI foundation`
- Requires browser proof, visual review, and build proof.

## Implementation Steps

1. Inject and use `NotificationService` for successful unlock/create/import/session operations.
2. Remove or hide persistent success-card markup in the unlocked state.
3. Consolidate account, endpoint, signing, address, and primary actions into a sticky top workspace bar.
4. Move profile edit/view entry into the account identity region with avatar/icon and account name.
5. Convert large text buttons to icon-forward action buttons with tooltip labels.
6. Preserve keyboard/accessibility labels for icon actions.
7. Run AccountApp and capture a large-screen screenshot after unlock.
8. Compare against `accountapp-after-login-current.png` and document scroll/space improvements.

## Scope Exceptions

- Small and medium responsive polish is out of scope.
- Deep address manager, backup, tag, and verification dialogs are later subbundles.

## Do Not Do

- Do not replace the entire economy workspace navigation shell.
- Do not create a marketing/hero landing page.
- Do not solve management flows with new accordion panels.
- Do not introduce broad custom CSS when BaseLib layout and action components fit.

## Acceptance Checklist

- Unlock success notification is visible and no success card remains.
- Workspace begins closer to the top on large screen than in the supplied screenshot.
- Profile/account identity is top-visible with edit action.
- Every icon/minimal action has a tooltip.
- No top bar element overlaps or clips at large-screen viewport.
- Existing refresh/send/settings/security routes still open.

## Proof Required

- `dotnet build repo://CanDoItAll.Ledger.slnx`
- Large-screen screenshot after unlock.
- Tooltip hover screenshot or DOM/accessibility proof.
- Execution report update with visual findings.
- Workbook checklist status update for SB02.

## Browser Validation Logging

- Route: AccountApp home after browser vault unlock.
- Viewport: 1920x1080 or maximized large-screen browser.
- Actions: unlock or create/import account, observe notification, hover toolbar actions, open profile/settings.
- Screenshots: `bundle://evidence/SB02/accountapp-home-after-unlock-large.png`, `bundle://evidence/SB02/accountapp-toolbar-tooltips-large.png`.
- Review questions: Did the workspace move up; are actions compact; are tooltips present; is any text clipped; are there new scroll or overlay issues?

## Progression Gate

- SB05 and SB07 cannot proceed until the shell screenshot confirms the post-login card stack has been removed or compressed.

## Suggested Agent Prompt

```text
Implement SB02 only. Use BaseLib components where practical, replace success cards with notifications, capture the large-screen screenshot and tooltip proof, update the execution report, and stop if the screenshot still shows the old vertical card stack.
```

