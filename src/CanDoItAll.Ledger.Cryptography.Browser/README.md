# CanDoItAll.Ledger.Cryptography.Browser

Web Crypto and IndexedDB implementations for browser-based CanDoItAll Ledger
accounts.

Use this Razor Class Library in Blazor WebAssembly applications that create,
import, unlock, and use locally encrypted account vaults. It implements the
platform-neutral cryptography and vault contracts from
`CanDoItAll.Ledger.Cryptography`.

## Requirements

- .NET 10
- Blazor WebAssembly
- A secure browser context: HTTPS in deployed environments or trusted localhost

## Install

```powershell
dotnet add package CanDoItAll.Ledger.Cryptography.Browser
```

## Register

```csharp
using CanDoItAll.Ledger.Cryptography.Browser;

builder.Services.AddCanDoItAllBrowserCrypto();
```

The registration supplies:

- `BrowserLedgerCryptoDriver` and `ILedgerCryptoDriver`;
- `BrowserIndexedDbAccountVaultStore` and `IAccountVaultStore`;
- `AccountVaultService`.

The driver imports its JavaScript module from:

```text
_content/CanDoItAll.Ledger.Cryptography.Browser/candoitall-ledger-crypto.js
```

Do not add a manual script element for this module.

## Browser Storage And Security

- IndexedDB storage is scoped to the browser origin. Changing scheme, host, or
  port creates a separate storage boundary.
- Users and browsers can clear IndexedDB. Provide an encrypted export and
  tested restore flow; there is no automatic password or key recovery.
- Private-key material is encrypted at rest. Vault ID, display name, address,
  algorithm, public key, KDF metadata, and timestamps remain plaintext.
  Unlocked keys are available to the running page for signing, so treat
  content-security policy, dependency safety, and XSS prevention as
  key-protection controls.
- Browser storage is local custody, not a server-side HSM or managed key vault.
- Never embed node API secrets or privileged signing keys in a published WASM
  application.

See the
[AccountApp reference host](https://github.com/fyziktom/CanDoItAll.Ledger/tree/main/examples/CanDoItAll.Ledger.AccountApp),
[base cryptography package](https://www.nuget.org/packages/CanDoItAll.Ledger.Cryptography),
and [project documentation](https://github.com/fyziktom/CanDoItAll.Ledger/tree/main/docs).

This package is part of the [CanDoItAll ecosystem](https://aicandoitall.com)
and uses the repository's MIT-derived license with website-link requirement.
