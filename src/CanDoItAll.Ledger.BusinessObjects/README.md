# CanDoItAll.Ledger.BusinessObjects

Account and business-object services for applications built on CanDoItAll
Ledger.

Use this package when an application needs higher-level account workflows,
encrypted account backups, transaction construction and signing, typed
business objects, ownership and commerce transitions, permission rules, or
secure messages. A submitted transaction becomes authoritative only after a
Ledger node or in-process engine accepts it.

## Requirements

- .NET 10
- An `ILedgerGateway` and `ILedgerReadClient` for live account operations

## Install

```powershell
dotnet add package CanDoItAll.Ledger.BusinessObjects
```

## Create A Business-Object Payload

```csharp
using CanDoItAll.Ledger.BusinessObjects;

BusinessObjectFamilyDefinition definition =
    BusinessObjectFamilyCatalog.FindOrDefault("invoice");

BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.CreatePayload(
    definition,
    name: "INV-2026-001",
    description: "Agent infrastructure expense",
    text: "Approved hosting and model usage",
    tags: "#expense #agent",
    dataItems: []);

string stateJson = BusinessObjectPayloadBuilder.Serialize(payload);
```

## Register Account Services

```csharp
using CanDoItAll.Ledger.BusinessObjects;

builder.Services.AddBusinessObjectAccountLayer();
```

This registers account setup, vault, backup, signing, casting, and transaction
building services. It deliberately does not choose a remote or in-process
ledger. For a remote node, use
`CanDoItAll.Ledger.Sdk` and `AddLedgerSdkWithNodeDriver(...)`; that composition
provides the gateway and read client.

`AddBusinessObjectLedgerLayer()` selects `InProcessLedgerGateway`. The host must
also register the complete engine, store, cryptography, clock, and policy
dependencies required by that gateway.

Server applications that need content-addressed attachments can call
`AddBusinessObjectIpfsStorage(apiBaseUri, gatewayBaseUri)`. Do not call this
server-side registration from WebAssembly applications. Register a
browser-safe HTTP upload service or driver instead.

## Security And Data Notes

- Protect account private keys and encrypted backups as sensitive material.
- Always password-protect portable account backups. An export without a
  password can contain unencrypted private-key material.
- The default in-memory vault store is disposable and is not a custody or
  recovery system.
- Transaction builders prepare signed intent; node validation and confirmation
  determine acceptance.
- Public business-object identity is derived from its confirmed output
  outpoint. Do not substitute an internal lineage key as the public ID.

See the
[SDK](https://www.nuget.org/packages/CanDoItAll.Ledger.Sdk),
[project documentation](https://github.com/fyziktom/CanDoItAll.Ledger/tree/main/docs),
and [AccountApp example](https://github.com/fyziktom/CanDoItAll.Ledger/tree/main/examples/CanDoItAll.Ledger.AccountApp).

This package is part of the [CanDoItAll ecosystem](https://aicandoitall.com)
and uses the repository's MIT License.
