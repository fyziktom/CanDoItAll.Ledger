import fs from "node:fs/promises";
import path from "node:path";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const bundleRoot = path.resolve("..");
const outputDir = path.join(bundleRoot, "plan");
const previewDir = path.join(bundleRoot, "proof", "workbook-previews");
await fs.mkdir(outputDir, { recursive: true });
await fs.mkdir(previewDir, { recursive: true });

const workbook = Workbook.create();

const theme = {
  dark: "#102A43",
  blue: "#1D4ED8",
  teal: "#0F766E",
  amber: "#B45309",
  red: "#B91C1C",
  green: "#15803D",
  gray: "#E5E7EB",
  light: "#F8FAFC",
  mid: "#CBD5E1",
  text: "#111827",
  white: "#FFFFFF",
};

function colName(index) {
  let n = index + 1;
  let out = "";
  while (n > 0) {
    const rem = (n - 1) % 26;
    out = String.fromCharCode(65 + rem) + out;
    n = Math.floor((n - 1) / 26);
  }
  return out;
}

function rangeAddress(startRow, startCol, rowCount, colCount) {
  const start = `${colName(startCol)}${startRow + 1}`;
  const end = `${colName(startCol + colCount - 1)}${startRow + rowCount}`;
  return `${start}:${end}`;
}

function styleSheet(sheet, widths) {
  sheet.showGridLines = false;
  widths.forEach((width, index) => {
    sheet.getRangeByIndexes(0, index, 1, 1).format.columnWidth = width;
  });
}

function title(sheet, text, subtitle, lastCol = 8) {
  const endCol = colName(lastCol);
  const titleRange = sheet.getRange(`A1:${endCol}1`);
  titleRange.merge();
  titleRange.values = [[text]];
  titleRange.format = {
    fill: theme.dark,
    font: { bold: true, color: theme.white, size: 16 },
    alignment: { horizontal: "left", vertical: "middle" },
  };
  titleRange.format.rowHeight = 30;

  const subRange = sheet.getRange(`A2:${endCol}2`);
  subRange.merge();
  subRange.values = [[subtitle]];
  subRange.format = {
    fill: "#DBEAFE",
    font: { color: theme.text, size: 10 },
    alignment: { horizontal: "left", vertical: "middle" },
    wrapText: true,
  };
  subRange.format.rowHeight = 34;
}

function applyHeader(range) {
  range.format = {
    fill: theme.blue,
    font: { bold: true, color: theme.white },
    alignment: { horizontal: "center", vertical: "middle" },
    wrapText: true,
    borders: { preset: "outside", style: "thin", color: "#94A3B8" },
  };
  range.format.rowHeight = 26;
}

function addTable(sheet, startRow, startCol, headers, rows, options = {}) {
  const all = [headers, ...rows];
  const address = rangeAddress(startRow, startCol, all.length, headers.length);
  const range = sheet.getRange(address);
  range.values = all;
  applyHeader(sheet.getRange(rangeAddress(startRow, startCol, 1, headers.length)));
  const dataRange = sheet.getRange(rangeAddress(startRow + 1, startCol, rows.length, headers.length));
  dataRange.format = {
    fill: theme.white,
    font: { color: theme.text, size: 10 },
    alignment: { vertical: "top" },
    wrapText: true,
    borders: { preset: "inside", style: "thin", color: "#E2E8F0" },
  };
  dataRange.format.rowHeight = options.rowHeight ?? 44;
  sheet.freezePanes.freezeRows(Math.min(3, startRow + 1));
  return { address, rowCount: all.length, colCount: headers.length };
}

function statusFill(status) {
  if (status === "Ready" || status === "Strong base" || status === "Closed" || status === "Passed" || status === "Mitigated") return "#DCFCE7";
  if (status === "Partial" || status === "Planned") return "#FEF3C7";
  if (status === "Missing" || status === "Blocked") return "#FEE2E2";
  return "#E0F2FE";
}

function colorStatusCells(sheet, startRow, startCol, rows, statusIndex) {
  rows.forEach((row, i) => {
    const fill = statusFill(row[statusIndex]);
    sheet.getRangeByIndexes(startRow + 1 + i, startCol + statusIndex, 1, 1).format = {
      fill,
      font: { bold: true, color: theme.text },
      alignment: { horizontal: "center", vertical: "middle" },
      wrapText: true,
    };
  });
}

const componentRows = [
  ["Core transaction records", "src/CanDoItAll.Economy.Core/Transactions.cs", "Ready", "Immutable LedgerTransaction/input/output/data records are shared by node, SDK, and future WASM path.", "Keep canonical model unchanged.", "SB04"],
  ["Signature abstractions", "src/CanDoItAll.Economy.Cryptography/CryptoDrivers.cs", "Strong base", "Host-neutral async crypto driver contract exists for .NET and browser implementations.", "Downstream account/session adapters still need to consume it.", "SB01"],
  ["Address/hash value objects", "src/CanDoItAll.Economy.Cryptography/EconomyAddressCodec.cs", "Strong base", "Async address/hash codec routes SHA256 through the crypto driver for browser compatibility.", "Keep core sync API unchanged for server path.", "SB01"],
  ["ECDSA P-256 provider", "src/CanDoItAll.Economy.Cryptography/EcdsaP256SignatureProvider.cs", "Strong base", "Verifier accepts WebCrypto raw 64-byte P1363 signatures and .NET driver signs that format.", "Transaction signing integration remains in SB04.", "SB01"],
  ["Account backup service", "src/CanDoItAll.Economy.BusinessObjects/AccountBackup.cs", "Strong base", "AES-GCM/PBKDF2 backup format with private/public key integrity checks exists.", "Integrated with AccountSetupService; wider AccountApp route wiring remains.", "SB02"],
  ["Account key source/signer", "src/CanDoItAll.Economy.BusinessObjects/AccountKeys.cs", "Strong base", "IAccountTransactionSigner, AccountVaultKeySource, and CryptoDriverAccountInputSigner support browser-side signing through the crypto driver.", "SB06 must wire unlocked vault keys into the AccountApp route.", "SB04/SB06"],
  ["Transaction builder", "src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs", "Ready", "Broad transaction assembly coverage now exposes unsigned draft methods while preserving signed SSR/server wrappers.", "SB06 must connect draft/sign/broadcast actions in AccountApp.", "SB04/SB06"],
  ["Node ledger client", "src/CanDoItAll.Economy.Sdk/EconomyNodeLedgerClient.cs", "Strong base", "HTTP client has read/list/detail/broadcast calls.", "Browser auth/config profile needed.", "SB03"],
  ["Node API endpoints", "src/CanDoItAll.Economy.Node/EconomyNodeApiEndpoints.cs", "Strong base", "Required read and signed submit endpoints are present.", "CORS/PWA/auth behavior must be explicit.", "SB03"],
  ["Account workspace UI", "src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor", "Ready", "Model/callback driven account workspace renders through SSR and WASM adapters and in the AccountApp browser proof.", "Closed by SB05 adapter host and SB06 AccountApp proof.", "SB05/SB06"],
  ["Backup panel UI", "src/CanDoItAll.Economy.Components/Components/Account/AccountBackupPanel.razor", "Strong base", "Host-neutral create/unlock/backup/import/pasted-key callbacks exist and are component-tested.", "SB05/SB06 must wire host adapters and full route state.", "SB02/SB05"],
  ["SSR node session", "src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs", "Ready", "Maps real node workspace behavior and remains isolated from WASM registration.", "SSR demo build passed after AccountApp changes.", "SB05/SB07"],
  ["Component demo", "examples/CanDoItAll.Economy.Components.Demo", "Ready", "Server demo for current components.", "No WASM/PWA account app exists.", "SB06"],
  ["Browser proof suite", "proof/SB06/accountapp-browser-proof.js", "Ready", "Clean-profile AccountApp proof covers create, backup, import, unlock, pasted key import, API detail, signed broadcast, storage audit, manifest, service worker, desktop/mobile screenshots.", "Closed by SB06/SB07 proof artifacts.", "SB06/SB07"],
];

const cryptoRows = [
  ["SHA256/hash/address", "EconomyAddressCodec + DotNet/Browser drivers", "Ready", "Browser WebCrypto digest wrapper plus .NET provider.", "Address vector matches .NET and node.", "SB01", "Critical"],
  ["Random bytes", "DotNetEconomyCryptoDriver and WebCrypto module", "Ready", "window.crypto.getRandomValues wrapper.", "IV/salt/nonces generated through secure browser random.", "SB01", "Critical"],
  ["AES-GCM encrypt/decrypt", "DotNetEconomyCryptoDriver and WebCrypto module", "Ready", "WebCrypto AES-GCM with compatible ciphertext+tag shape.", "Encrypt/decrypt round trip and tamper reject.", "SB01", "Critical"],
  ["Password KDF", "DotNetEconomyCryptoDriver and WebCrypto module", "Ready", "PBKDF2-SHA256 metadata usable in browser and .NET.", "Wrong password fails; vectors include salt/iterations.", "SB01", "Critical"],
  ["ECDSA signing", "DotNet driver + Browser WebCrypto module", "Ready", "WebCrypto raw P1363 signatures accepted by .NET provider.", "Browser signature verifies through .NET/node provider.", "SB01", "Critical"],
  ["Private key persistence", "AccountVaultService + BrowserIndexedDbAccountVaultStore", "Ready", "Encrypted IndexedDB vault.", "IndexedDB contains no raw private key.", "SB01", "Critical"],
  ["Backup compatibility", "AccountBackupService + AccountSetupService + AccountAppSession", "Ready", "Encrypted backup export/import, wrong-password rejection, pasted-key import, same-address recovery, and AccountApp route import/re-encrypt/unlock proof passed.", "Closed by SB02 and SB06 browser proof.", "SB02/SB06", "High"],
  ["Forbidden dependencies", "Browser package audit captured", "Ready", "Dependency audit in AccountApp path.", "No NBitcoin/BouncyCastle in browser project graph.", "SB01/SB06", "High"],
];

const apiRows = [
  ["Spendable outputs", "GET", "/api/v1/ledger/{ledgerId}/accounts/{address}/spendable-outputs", "Strong base", "EconomyNodeLedgerClient.GetSpendableOutputsAsync", "Contract test and browser network proof.", "SB03"],
  ["Account transactions", "GET", "/api/v1/ledger/{ledgerId}/accounts/{address}/transactions", "Strong base", "EconomyNodeLedgerClient.GetTransactionsAsync", "includeData behavior and pagination/limits check.", "SB03"],
  ["Ledger transaction detail", "GET", "/api/v1/ledger/{ledgerId}/transactions/{txId}", "Strong base", "EconomyNodeLedgerClient.GetTransactionAsync(context, txId)", "includeData proof.", "SB03"],
  ["Top-level transaction detail", "GET", "/api/v1/transactions/{txId}", "Strong base", "EconomyNodeLedgerClient.GetTransactionAsync(txId)", "Need decide route used by AccountApp.", "SB03"],
  ["Signed broadcast", "POST", "/api/v1/ledger/transactions", "Ready", "EconomyNodeLedgerClient.SubmitTransactionAsync plus SB04 browser-signed POST proof.", "SB06 must repeat in the real AccountApp route.", "SB03/SB04/SB06"],
  ["Auth/CORS", "Config", "Node pipeline/API filters", "Ready", "Browser node driver sends no default bearer/api-key headers and AccountApp proof uses API-only CORS-safe fake node endpoints.", "Future production auth remains deployment-specific, but demo PWA has no compiled static secret.", "SB03/SB06"],
  ["WASM DI profile", "Config", "Sdk ServiceCollectionExtensions", "Ready", "Browser-safe node driver registration exists without static API key/bearer-token fields and uses the crypto-driver account signer.", "SB06 must wire actual AccountApp configuration.", "SB03/SB04/SB06"],
];

const uiRows = [
  ["Account workspace", "EconomyAccountWorkspace.razor", "Reuse with adapter", "Account dashboard, balances, recent txs, callbacks.", "SSR session dependency must be isolated.", "SB05"],
  ["Transaction composer", "Components/Transactions", "Reuse with adapter", "Composer/dialog shell already callback oriented.", "Needs browser transaction action service.", "SB05/SB06"],
  ["Transaction history", "TransactionHistoryBrowser.razor", "Reuse with adapter", "History/list display exists.", "Needs API data adapter.", "SB05"],
  ["UTXO explorer", "UtxoExplorer.razor", "Reuse with adapter", "Spendable output display exists.", "Needs API data adapter.", "SB05"],
  ["Backup panel", "AccountBackupPanel.razor", "Reuse with adapter", "Host-neutral action callbacks exist for create, unlock, backup, backup import, pasted key import, and subaccount creation.", "Needs AccountApp adapter state and browser route wiring.", "SB02/SB05"],
  ["VENFT shell reference", "VEFramework VEBlazor components", "Flow reference only", "Create/import/unlock/backup story steps.", "Do not copy broad AppData/static JS pattern.", "SB02"],
  ["WASM PWA app shell", "examples/CanDoItAll.Economy.AccountApp", "Ready", "PWA manifest/service worker, AccountApp setup/import/unlock/workspace/detail/broadcast screens passed desktop and mobile proof.", "Closed by proof/SB06/accountapp-browser-proof-output.json.", "SB06"],
];

const flowRows = [
  ["Create account", "Password -> create key -> encrypt private key -> save address/encrypted key -> account-created event", "SB01 vault, SB02 setup service, SB05/06 UI", "Do not use weak IV or password hash as direct key.", "SB02"],
  ["Unlock account", "Password -> load encrypted key -> decrypt -> load account -> load data", "SB01 vault, SB02 session, SB03 API load", "Do not mix cache/bookmarks/API settings in one broad service.", "SB02/SB06"],
  ["Import backup", "Upload backup or paste key -> old password -> new password/confirm -> import -> encrypt -> save", "SB02 import service and UI", "Do not skip pasted-key path or password confirm.", "SB02"],
  ["Backup export", "Build backup DTO -> browser download", "SB02 backup export", "Do not include unencrypted private key except explicit user export format if accepted by requirement.", "SB02"],
  ["Load transactions", "After account load, call API for transactions and details", "SB03 API client, SB05 UI adapter", "Do not use in-process node services.", "SB03/SB05/SB06"],
  ["Send transaction", "Load UTXOs -> assemble unsigned draft -> sign through browser crypto driver -> broadcast signed transaction", "SB04 transaction service, SB03 API", "Do not server-sign or send unsigned drafts.", "SB04/SB06"],
];

const subbundleRows = [
  ["SB01", "Browser Crypto Vault", "Critical", "Closed", "None", "WebCrypto, .NET crypto compatibility, IndexedDB vault, no forbidden dependencies.", "Vault manifest and browser proof."],
  ["SB02", "Account Backup Import Flow", "Critical", "Closed", "SB01", "Create, unlock, backup, import, pasted-key flows.", "Passed in proof/SB02/manifest.md."],
  ["SB03", "API Only Node Client Contracts", "Critical", "Closed", "None", "Read/list/detail/broadcast API and browser-safe auth/CORS.", "Passed in proof/SB03/manifest.md."],
  ["SB04", "Transaction Assembly Signing Split", "Critical", "Closed", "SB01, SB03", "Browser assembly/signing and signed broadcast.", "Passed in proof/SB04/manifest.md."],
  ["SB05", "Shared UI SSR/WASM Flex", "High", "Closed", "SB02, SB03, SB04", "Host-neutral shared components and adapters.", "Passed in proof/SB05/manifest.md."],
  ["SB06", "WASM PWA Demo App", "High", "Closed", "SB04, SB05", "New AccountApp project and PWA shell.", "Passed in proof/SB06/manifest.md."],
  ["SB07", "Integration Browser Proof", "Critical", "Closed", "SB06", "End-to-end browser proof and final closure.", "Passed in proof/SB07/manifest.md."],
];

const validationRows = [
  ["Crypto vectors", "Unit + browser", "SB01", "Passed", "Hash/address/signature/encryption compatibility.", "proof/SB01/manifest.md"],
  ["Vault persistence", "Browser", "SB01", "Passed", "Encrypted IndexedDB record only.", "proof/SB01/browser-harness-result.json"],
  ["Wrong password/tamper", "Unit + browser", "SB01/SB02", "Passed", "Reject wrong password and tampered data for SB01 vault.", "proof/SB01/manifest.md"],
  ["Backup import/export", "Unit + browser", "SB02", "Passed", "Export/import recovers same address, rejects wrong passwords/forged keys, and imports pasted keys.", "proof/SB02/manifest.md"],
  ["API contracts", "Integration", "SB03", "Passed", "Read/list/detail/broadcast endpoint request shapes and no-private-key broadcast body passed.", "proof/SB03/manifest.md"],
  ["Browser signed broadcast", "Integration + browser", "SB04", "Passed", "Unsigned draft assembly, crypto-driver signing, ledger acceptance, and browser POST proof passed.", "proof/SB04/manifest.md"],
  ["Shared UI SSR/WASM", "Component + browser", "SB05", "Passed", "Adapter-driven component tests, WASM adapter sign/broadcast test, SSR route screenshot, and SSR/WASM sample route screenshots passed.", "proof/SB05/manifest.md"],
  ["PWA launch/install", "Browser", "SB06", "Passed", "Manifest/service worker/app launch plus AccountApp desktop/mobile proof passed.", "proof/SB06/manifest.md"],
  ["No-key-leak audit", "Browser + network", "SB07", "Passed", "Broadcast body has browser signature/public key material and no private key fields; IndexedDB contains encrypted private key only; local/session storage empty of secrets.", "proof/SB07/manifest.md"],
  ["Bundle closure", "Validator", "SB07", "Passed", "Completed-stage bundle validator checklist passed with scoped builds and 188/188 ledger tests.", "proof/SB07/validator-result.md"],
];

const riskRows = [
  ["WebCrypto ECDSA format mismatch", "Critical", "Mitigated", "SB01 proves raw P1363 WebCrypto signatures verify through the .NET provider.", "SB01"],
  ["Browser key export conflicts with backup", "Critical", "Mitigated", "SB01 uses extractable P-256 key material for encrypted vault/backup compatibility; SB02 owns user backup UX.", "SB01/SB02"],
  ["Core SHA256 not WASM-safe", "Critical", "Mitigated", "SB01 added async driver-based address/hash derivation for browser paths.", "SB01"],
  ["Builder signs during assembly", "High", "Mitigated", "SB04 added unsigned draft builders, a transaction signer service, and browser crypto-driver signing proof.", "SB04"],
  ["SSR service coupling in UI", "High", "Mitigated", "Host-neutral adapters, WASM DI isolation tests, AccountApp proof, and SSR demo build passed.", "SB05/SB07"],
  ["Browser auth requires unsafe secrets", "Critical", "Mitigated", "Browser SDK registration sends no default static bearer/api-key headers; demo AccountApp uses configurable API base and local fake API proof.", "SB03/SB06"],
  ["PWA stale config", "Medium", "Mitigated", "Clean-profile PWA proof validated manifest/service worker and appsettings-driven adapter/node configuration.", "SB06/SB07"],
  ["Old VEFramework crypto copied too literally", "High", "Mitigated", "SB02 uses separated setup/vault/backup services and component callbacks instead of copying broad VEBlazor AppData.", "SB02"],
];

const summary = workbook.worksheets.add("Readiness Summary");
styleSheet(summary, [24, 18, 18, 18, 24, 26, 30, 22]);
title(summary, "Account WASM PWA Demo - Bundle Planning", "Prepared and executed on 2026-06-29. SB01 through SB07 are implemented and proven with browser crypto, account recovery, API-only contracts, local signing, shared UI adapters, AccountApp PWA flow, no-key-leak audit, scoped builds, and 188/188 ledger tests.", 7);
summary.getRange("A4:D4").values = [["Area", "Ready/Strong", "Partial/Planned", "Missing/Blocked"]];
applyHeader(summary.getRange("A4:D4"));
summary.getRange("A5:A10").values = [["Components"], ["Crypto"], ["API"], ["UI"], ["Subbundles"], ["Validation"]];
summary.getRange("B5:D10").values = [
  [14, 0, 0],
  [8, 0, 0],
  [7, 0, 0],
  [6, 0, 0],
  [7, 0, 0],
  [10, 0, 0],
];
summary.getRange("A5:D10").format = {
  fill: theme.white,
  borders: { preset: "inside", style: "thin", color: "#E2E8F0" },
  alignment: { vertical: "middle" },
};
summary.getRange("B5:D10").format = {
  fill: "#F1F5F9",
  font: { bold: true, color: theme.text },
  alignment: { horizontal: "center", vertical: "middle" },
};
const ruleRange = summary.getRange("A12:H13");
ruleRange.merge();
ruleRange.values = [["Final closure rule: AccountApp PWA proof, storage/network no-key-leak audit, SSR demo build, scoped AccountApp build, and 188/188 ledger tests passed. Future production auth/CORS policy remains deployment-specific, not a demo-bundle blocker."]];
ruleRange.format = {
  fill: "#FEF3C7",
  font: { bold: true, color: "#7C2D12" },
  wrapText: true,
  alignment: { vertical: "middle" },
  borders: { preset: "outside", style: "thin", color: "#F59E0B" },
};
ruleRange.format.rowHeight = 36;

const compSheet = workbook.worksheets.add("Component Inventory");
styleSheet(compSheet, [25, 38, 16, 48, 44, 16]);
title(compSheet, "Component And Driver Inventory", "Current ready/missing map for repository components and drivers.", 5);
addTable(compSheet, 3, 0, ["Surface", "Source", "Status", "Ready Evidence", "Missing / Action Needed", "Owner"], componentRows, { statusCol: 2, rowHeight: 58 });
colorStatusCells(compSheet, 3, 0, componentRows, 2);

const cryptoSheet = workbook.worksheets.add("Crypto Gap Matrix");
styleSheet(cryptoSheet, [26, 42, 16, 44, 42, 16, 16]);
title(cryptoSheet, "Crypto Gap Matrix", "Foundational gaps that must close before account import, vault, and signing work continues.", 6);
addTable(cryptoSheet, 3, 0, ["Capability", "Current implementation", "Status", "Target implementation", "Validation required", "Owner", "Priority"], cryptoRows, { statusCol: 2, priorityCol: 6, rowHeight: 58 });
colorStatusCells(cryptoSheet, 3, 0, cryptoRows, 2);

const apiSheet = workbook.worksheets.add("API Contract Map");
styleSheet(apiSheet, [24, 12, 48, 16, 46, 46, 16]);
title(apiSheet, "API Contract Map", "API-only AccountApp communication requirements and current endpoint readiness.", 6);
addTable(apiSheet, 3, 0, ["Need", "Method", "Endpoint / Surface", "Status", "Current Support", "Gap / Proof Needed", "Owner"], apiRows, { statusCol: 3, rowHeight: 58 });
colorStatusCells(apiSheet, 3, 0, apiRows, 3);

const uiSheet = workbook.worksheets.add("UI Reuse Matrix");
styleSheet(uiSheet, [24, 42, 22, 44, 44, 18]);
title(uiSheet, "UI Reuse Matrix", "Existing UI assets and SSR/WASM flexibility plan.", 5);
addTable(uiSheet, 3, 0, ["UI Surface", "Source", "Reuse Decision", "Ready Evidence", "Work Needed", "Owner"], uiRows, { rowHeight: 60 });

const flowSheet = workbook.worksheets.add("VEFramework Flow Map");
styleSheet(flowSheet, [24, 58, 42, 44, 18]);
title(flowSheet, "VEFramework Flow Map", "User-story steps copied from VEBlazor at flow level, with implementation cautions.", 4);
addTable(flowSheet, 3, 0, ["Flow", "VEBlazor steps to preserve", "Ledger target", "Do not copy / caution", "Owner"], flowRows, { rowHeight: 66 });

const subSheet = workbook.worksheets.add("Subbundle Plan");
styleSheet(subSheet, [12, 30, 16, 16, 30, 50, 42]);
title(subSheet, "Subbundle Plan", "Execution order, dependency gates, and closure proof.", 6);
addTable(subSheet, 3, 0, ["ID", "Subbundle", "Priority", "Status", "Prerequisites", "Scope", "Closure Gate"], subbundleRows, { statusCol: 3, priorityCol: 2, rowHeight: 56 });
colorStatusCells(subSheet, 3, 0, subbundleRows, 3);

const valSheet = workbook.worksheets.add("Validation Checklist");
styleSheet(valSheet, [28, 24, 18, 16, 52, 42]);
title(valSheet, "Validation Checklist", "Required proof artifacts and closure checks by phase.", 5);
addTable(valSheet, 3, 0, ["Check", "Type", "Owner", "Status", "Acceptance Evidence", "Proof Path"], validationRows, { statusCol: 3, rowHeight: 58 });
colorStatusCells(valSheet, 3, 0, validationRows, 3);

const riskSheet = workbook.worksheets.add("Risk Register");
styleSheet(riskSheet, [40, 16, 16, 58, 18]);
title(riskSheet, "Risk Register", "Open risks and reopen triggers for the initiative.", 4);
addTable(riskSheet, 3, 0, ["Risk", "Priority", "Status", "Mitigation / Gate", "Owner"], riskRows, { priorityCol: 1, rowHeight: 58 });

for (const sheet of workbook.worksheets.items) {
  const used = sheet.getUsedRange();
  used.format.autofitRows();
}

const inspect = await workbook.inspect({
  kind: "table",
  range: "Readiness Summary!A4:D10",
  include: "values,formulas",
  tableMaxRows: 12,
  tableMaxCols: 6,
  maxChars: 5000,
});
console.log(inspect.ndjson);

const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
  maxChars: 4000,
});
console.log(errors.ndjson);

for (const sheet of workbook.worksheets.items) {
  const preview = await workbook.render({
    sheetName: sheet.name,
    autoCrop: "all",
    scale: 1,
    format: "png",
  });
  await fs.writeFile(
    path.join(previewDir, `${sheet.name.replace(/[^A-Za-z0-9]+/g, "-").toLowerCase()}.png`),
    new Uint8Array(await preview.arrayBuffer()),
  );
}

const xlsx = await SpreadsheetFile.exportXlsx(workbook);
await xlsx.save(path.join(outputDir, "account-wasm-pwa-planning.xlsx"));

console.log(`Workbook saved to ${path.join(outputDir, "account-wasm-pwa-planning.xlsx")}`);
console.log(`Previews saved to ${previewDir}`);
process.exit(0);
