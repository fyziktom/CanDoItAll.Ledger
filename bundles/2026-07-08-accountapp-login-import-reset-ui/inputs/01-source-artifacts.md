# Source Artifacts

| Artifact | Path | Notes |
| --- | --- | --- |
| Screenshot with red strike-through over redundant unlocked top bar | `bundle://inputs/screenshot-topbar-remove.png` | Shows `AccountApp WASM PWA` identity/facts/action top frame above the real `VENFT` workspace shell. User says remove it. |
| Legacy CreateAccount reference | `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Components\Account\CreateAccount.razor` | Modal-first create flow with password guidance and explicit account-created callback. |
| Legacy UnlockAccount reference | `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Components\Account\UnlockAccount.razor` | Password entry with progress status and enter-key unlock behavior. |
| Legacy ImportAccount reference | `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Components\Account\ImportAccount.razor` | Three-step import flow: import material, password, finish. |
| Legacy MyAccount reference | `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Pages\MyAccount.razor` | If no account is stored, offers create or import; if stored, tells user to login. |
| Current AccountApp home | `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` | Locked and unlocked account entry/workspace surface. |
| Current AccountApp session | `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppSession.cs` | Owns create, unlock, backup import, pasted key import, and stored-vault detection. |
| Current AccountApp CSS | `repo://examples/CanDoItAll.Economy.AccountApp/wwwroot/css/app.css` | Owns AccountApp shell and account-entry styling. |
