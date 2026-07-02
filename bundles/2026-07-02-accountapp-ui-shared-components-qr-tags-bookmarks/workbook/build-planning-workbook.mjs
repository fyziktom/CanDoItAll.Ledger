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
  navy: "#143642",
  blue: "#2563EB",
  teal: "#0F766E",
  amber: "#B45309",
  red: "#B91C1C",
  green: "#15803D",
  slate: "#334155",
  gray: "#E5E7EB",
  light: "#F8FAFC",
  white: "#FFFFFF",
  text: "#111827",
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

function title(sheet, text, subtitle, lastColIndex) {
  const titleRange = sheet.getRange(`A1:${colName(lastColIndex)}1`);
  titleRange.merge();
  titleRange.values = [[text]];
  titleRange.format = {
    fill: theme.navy,
    font: { bold: true, color: theme.white, size: 16 },
    alignment: { horizontal: "left", vertical: "middle" },
  };
  titleRange.format.rowHeight = 30;

  const subtitleRange = sheet.getRange(`A2:${colName(lastColIndex)}2`);
  subtitleRange.merge();
  subtitleRange.values = [[subtitle]];
  subtitleRange.format = {
    fill: "#E0F2FE",
    font: { color: theme.text, size: 10 },
    alignment: { horizontal: "left", vertical: "middle" },
    wrapText: true,
  };
  subtitleRange.format.rowHeight = 36;
}

function applyHeader(range) {
  range.format = {
    fill: theme.blue,
    font: { bold: true, color: theme.white, size: 10 },
    alignment: { horizontal: "center", vertical: "middle" },
    wrapText: true,
    borders: { preset: "outside", style: "thin", color: "#94A3B8" },
  };
  range.format.rowHeight = 28;
}

function addTable(sheet, startRow, startCol, headers, rows, options = {}) {
  const values = [headers, ...rows];
  const address = rangeAddress(startRow, startCol, values.length, headers.length);
  const range = sheet.getRange(address);
  range.values = values;
  applyHeader(sheet.getRange(rangeAddress(startRow, startCol, 1, headers.length)));
  const dataRange = sheet.getRange(rangeAddress(startRow + 1, startCol, rows.length, headers.length));
  dataRange.format = {
    fill: theme.white,
    font: { color: theme.text, size: 10 },
    alignment: { vertical: "top" },
    wrapText: true,
    borders: { preset: "inside", style: "thin", color: "#E2E8F0" },
  };
  dataRange.format.rowHeight = options.rowHeight ?? 48;
  sheet.freezePanes.freezeRows(Math.min(startRow + 1, 4));
  return { address, rowCount: values.length, colCount: headers.length };
}

function statusFill(status) {
  if (["Ready", "Prepared", "Passed", "Use", "Strong", "Completed", "Implemented", "Validated", "Mitigated"].includes(status)) return "#DCFCE7";
  if (["Planned", "Pending", "Partial"].includes(status)) return "#FEF3C7";
  if (["Blocked", "Risk", "Missing"].includes(status)) return "#FEE2E2";
  return "#E0F2FE";
}

function colorStatusCells(sheet, startRow, startCol, rows, statusIndex) {
  rows.forEach((row, i) => {
    sheet.getRangeByIndexes(startRow + 1 + i, startCol + statusIndex, 1, 1).format = {
      fill: statusFill(row[statusIndex]),
      font: { bold: true, color: theme.text, size: 10 },
      alignment: { horizontal: "center", vertical: "middle" },
      wrapText: true,
      borders: { preset: "inside", style: "thin", color: "#CBD5E1" },
    };
  });
}

const readinessRows = [
  ["Bundle state", "Completed", "Architecture, subbundles, traceability, implementation proof, and workbook are synchronized.", "reviews/01-execution-report.md"],
  ["QR generator", "Passed", "CanDoItAll.Components.QRCode builds, tests, and renders SVG in Blazor WASM PWA.", "proof/SB01/transcripts/components-qrcode-tests.txt"],
  ["Production implementation", "Completed", "Seven gated subbundles implemented across AccountApp, Economy components, node host, tests, and shared QR components.", "subbundles/"],
  ["Large-screen visual closure", "Validated", "AccountApp large-screen screenshots captured and reviewed after SB02-SB06 implementation.", "proof/SB07/screenshots/accountapp-final-home-large.png"],
  ["Mobile/medium responsive tuning", "Deferred", "Explicitly out of scope for this bundle.", "inputs/02-structured-input.md"],
];

const requirementRows = [
  ["R01", "Replace post-login card stack with notification-backed compact sticky top bar.", "SB02", "proof/SB02/screenshots/accountapp-home-after-unlock-large.png", "Implemented"],
  ["R02", "Use icon/minimal action buttons with tooltip labels.", "SB02", "proof/SB02/screenshots/accountapp-toolbar-tooltips-large.png", "Implemented"],
  ["R03", "Prefer prepared CanDoItAll components for forms/dialogs.", "SB01-SB05", "Source diff review in reviews/01-execution-report.md", "Implemented"],
  ["R04", "Move profile identity to top account/avatar region.", "SB02", "proof/SB07/screenshots/accountapp-final-home-large.png", "Implemented"],
  ["R05", "Add shared QR generation components.", "SB01", "proof/SB01/transcripts/components-qrcode-build.txt", "Passed"],
  ["R06", "Add shared QR scanner components.", "SB01/SB06", "proof/SB06/screenshots/scan-manual-entry-large.png", "Implemented"],
  ["R07", "Use QR for address, tx hash, and ownership hash.", "SB06", "proof/SB06/screenshots/address-qr-large.png; proof/SB06/screenshots/tx-hash-qr-large.png; proof/SB06/screenshots/ownership-qr-large.png", "Implemented"],
  ["R08", "Build verification as reusable driver/builder.", "SB06", "proof/SB06/screenshots/ownership-verification-large.png", "Implemented"],
  ["R09", "Implement tag occurrence service.", "SB03", "proof/SB03/transcripts/account-workspace-local-services-tests.txt", "Passed"],
  ["R10", "Use TagEditor and colored badges with persisted readable colors.", "SB03", "proof/SB03/screenshots/tag-editor-suggestions-large.png; proof/SB03/screenshots/tag-badges-after-reload-large.png", "Implemented"],
  ["R11", "Add generic address manager dialog.", "SB04", "proof/SB04/screenshots/address-manager-large.png", "Implemented"],
  ["R12", "Reuse address manager in multiple flows.", "SB04", "proof/SB04/screenshots/address-select-send-large.png", "Implemented"],
  ["R13", "Add settings backup/import/export dialog.", "SB05", "proof/SB05/screenshots/settings-backup-export-large.png; proof/SB05/screenshots/settings-backup-import-preview-large.png", "Implemented"],
  ["R14", "Use dialogs instead of accordion-heavy management.", "SB02/SB04/SB05/SB06", "Dialog screenshots in proof/SB04, proof/SB05, proof/SB06, and proof/SB07", "Validated"],
  ["R15", "Capture and analyze large-screen visual proof.", "SB07", "proof/SB07/screenshots/accountapp-final-home-large.png; reviews/01-execution-report.md", "Validated"],
  ["R16", "Preserve existing ledger behavior.", "SB07", "proof/SB07/transcripts/ledger-solution-build.txt", "Passed"],
];

const subbundleRows = [
  ["SB01", "Shared component boundary and QR foundation", "Critical", "Completed", "QR spike proof", "New CanDoItAll.Components.QRCode project, QR view/button/dialog/scanner components, tests.", "Build/test transcripts and QR dialog screenshot are present."],
  ["SB02", "Desktop shell and top toolbar polish", "Critical", "Completed", "SB01 for QR actions", "Removed post-login card stack, added notification-backed compact identity/action bar.", "After-unlock and tooltip screenshots prove layout."],
  ["SB03", "Tag occurrence and shared tag rendering", "High", "Completed", "Browser storage pattern", "Unique/general counters, persisted colors, TagEditor suggestions, TagBadge rendering.", "Service tests and tag screenshots pass."],
  ["SB04", "Generic address manager dialog", "High", "Completed", "SB03 tag model", "Own/external address directory, favorites, tags, profile cache, manage/select modes.", "Manage/select screenshots and persistence service tests pass."],
  ["SB05", "Browser settings backup import export", "High", "Completed", "SB02-SB04 stores", "Versioned local settings export/import dialog with preview and section selection.", "Export/import screenshots and service tests pass."],
  ["SB06", "Verification driver QR scan and ticket ownership", "Critical", "Completed", "SB01 and SB04", "QR replacements, scanner/manual entry, reusable BO verification service, ticket Used flow.", "QR, verification, signed ownership, and used-ticket screenshots pass."],
  ["SB07", "Large-screen browser validation and closure", "Critical", "Completed", "SB02-SB06 complete", "Final live node/account app build, screenshot review, workbook/report closure.", "All evidence rows concrete; build/tests pass; no critical visual defect remains."],
];

const componentRows = [
  ["Notification", "BaseLib", "Use", "Unlock/create/import success messages; refresh success.", "Avoid persistent success cards."],
  ["TooltipTarget / TooltipService", "BaseLib", "Use", "Icon/minimal toolbar actions and dialog actions.", "Every icon action needs a label."],
  ["Toolbar / PageHeaderActionButton", "BaseLib", "Use", "Top command strip and action groups.", "Keep large-screen density; avoid local button sprawl."],
  ["DialogScaffold / PickerDialogShell", "BaseLib", "Use", "QR, settings backup, address manager, verification.", "Management surfaces live in dialogs."],
  ["TagEditor", "BaseLib", "Use", "BO tags and address user tags.", "Backed by suggestions from occurrence store."],
  ["Badge / StatusBadge", "BaseLib", "Use", "Tag display, occurrence badges, verification status.", "Persist color and foreground pairing."],
  ["CanDoItAll.Components.QRCode", "New shared project", "Create", "QR button/dialog/render/scan components.", "Generic UI only; ledger payload semantics stay in Economy components."],
];

const qrRows = [
  ["Microsoft guidance", "Recommends Net.Codecrete.QrCodeGenerator for Blazor QR rendering.", "Use", "https://learn.microsoft.com/en-us/aspnet/core/blazor/security/qrcodes-for-authenticator-apps?view=aspnetcore-10.0"],
  ["Net.Codecrete.QrCodeGenerator", "Pure C# generator; built and rendered in WASM PWA spike.", "Use", "https://github.com/manuelbl/QrCodeGenerator"],
  ["AlexNek/Blazor.QrCode", "Possible generation fallback.", "Fallback", "https://github.com/AlexNek/Blazor.QrCode"],
  ["ZXingBlazor", "Blazor QR/barcode reader.", "Use", "https://github.com/densen2014/ZXingBlazor"],
  ["Local spike", "Build passed and screenshot captured.", "Passed", "evidence/qr-wasm-test/qrcode-wasm-cli-render.png"],
];

const tagRows = [
  ["Normalize tag", "Trim, case-normalize, reject empty tags.", "Prevents duplicate suggestion keys.", "Unit test"],
  ["General occurrence", "Increment on every BO details open for each tag.", "Captures common viewing behavior.", "Unit test"],
  ["Unique occurrence", "Increment once per account/BO/tag tuple.", "Prevents one BO from dominating suggestions.", "Unit test"],
  ["Suggestion mode", "UniqueBusinessObjects, GeneralOpenCount, ManualPinned.", "User-selectable source for often tags.", "Settings UI + test"],
  ["Visual metadata", "Store color token and foreground token per tag.", "Stable, readable badges after reload.", "Unit test + screenshot"],
  ["BaseLib UI", "Use TagEditor and badges.", "Consistent forms/dialogs.", "Browser screenshot"],
];

const addressRows = [
  ["Entry model", "Address, display name, kind, profile cache, favorite, tags, source, last seen.", "One model for external and own.", "Store test"],
  ["Manage mode", "Square cards, edit metadata, favorite, tag, filter.", "Home address management.", "Screenshot"],
  ["Select mode", "Search/filter cards and return selected address.", "Send tx, BO send/mint, gallery, subaccount flows.", "Integration test"],
  ["Profile cache", "Show cached profile BO summary/avatar when available.", "Richer contact cards.", "Screenshot"],
  ["Persistence", "localStorage or IndexedDB store.", "Favorites/tags survive reload.", "Reload test"],
];

const visualRows = [
  ["SB02 shell", "AccountApp home after unlock", "1920x1080", "Workspace starts higher; no success card stack; tooltip labels work.", "Validated"],
  ["SB03 tags", "BO create/edit/details", "1920x1080", "TagEditor suggestions visible; badges readable; no clipping.", "Validated"],
  ["SB04 address manager", "Manage/select dialogs", "1920x1080", "Square cards scannable; no overflow; selection clear.", "Validated"],
  ["SB05 backup", "Settings backup dialog", "1920x1080", "Export/import preview in dialog; no accordion sprawl.", "Validated"],
  ["SB06 QR", "Tx/BO/ticket QR dialogs", "1920x1080", "Real QR rendered; payload/copy visible; dialog unclipped.", "Validated"],
  ["SB06 verification", "Ownership/ticket verification", "1920x1080", "Step results clear; Used confirmation explicit.", "Validated"],
  ["SB07 final", "Full route set", "1920x1080", "No overlays, overflows, clipped text, or excessive long-scroll regression.", "Validated"],
];

const riskRows = [
  ["Shared component repo coordination", "High", "Mitigated", "Shared QR build/tests and Ledger solution build captured separately.", "SB01/SB07"],
  ["Scanner camera permissions", "Medium", "Mitigated", "ZXing scanner wrapped with manual entry proof for camera-unavailable environments.", "SB06/SB07"],
  ["Tag counter ambiguity", "Medium", "Mitigated", "Tests separately prove unique and general occurrence semantics.", "SB03"],
  ["Address manager scope creep", "Medium", "Mitigated", "Generic local model implemented; cloud sync remains out of scope.", "SB04"],
  ["Settings import corruption", "High", "Mitigated", "Versioned payload, section selection, preview, and invalid input handling implemented.", "SB05"],
  ["Visual proof weakness", "High", "Mitigated", "SB07 screenshot set reviewed after live node/account-app run and repairs.", "SB07"],
];

const referenceRows = [
  ["Current AccountApp shell", "repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor", "SB02"],
  ["Shared account workspace services", "repo://src/CanDoItAll.Economy.Components/Services/AccountWorkspaceLocalServices.cs", "SB03/SB04/SB05/SB06"],
  ["Shared QR components", "C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.QRCode", "SB01/SB06"],
  ["Account session status", "repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppSession.cs", "SB02/SB05"],
  ["Economy workspace", "repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor", "SB02/SB05/SB07"],
  ["BO wizard tags", "repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor", "SB03/SB04"],
  ["BO details QR/tickets", "repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor", "SB03/SB06"],
  ["Ticket verification service", "repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTicketVerification.cs", "SB06"],
  ["Address bookmark manager", "repo://src/CanDoItAll.Economy.Components/Components/Account/AddressBookmarkManager.razor", "SB04"],
  ["BaseLib TagEditor", "C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.BaseLib/Components/Forms/TagEditor.razor", "SB03/SB04"],
  ["BaseLib QR-adjacent dialog primitives", "C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.BaseLib/Components/Modals/DialogScaffold.razor", "SB01/SB05/SB06"],
  ["VEBlazor SpaceTags", "C:/repositories/VirtualEconomyFramework/VirtualEconomyFramework/VEBlazor/Components/Minting/SpaceTags.razor", "SB03 reference only"],
  ["Microsoft QR doc", "https://learn.microsoft.com/en-us/aspnet/core/blazor/security/qrcodes-for-authenticator-apps?view=aspnetcore-10.0", "SB01"],
  ["QrCodeGenerator repo", "https://github.com/manuelbl/QrCodeGenerator", "SB01"],
  ["ZXingBlazor repo", "https://github.com/densen2014/ZXingBlazor", "SB01/SB06"],
];

const sheets = [
  ["Readiness Summary", "Preparation status and immediate gates.", [28, 18, 70, 54], ["Area", "Status", "Finding", "Reference"], readinessRows, 1, 66],
  ["Requirement Matrix", "Normalized request coverage and closure proof type.", [12, 62, 20, 40, 18], ["ID", "Requirement", "Subbundle", "Proof", "Status"], requirementRows, 4, 56],
  ["Subbundle Plan", "Execution order, dependencies, and closure gates.", [10, 36, 16, 16, 34, 58, 58], ["ID", "Subbundle", "Priority", "Status", "Prerequisites", "Scope", "Closure Gate"], subbundleRows, 3, 64],
  ["Shared Component Reuse", "Prepared components and ownership boundaries.", [30, 28, 14, 52, 54], ["Component", "Source", "Decision", "Use", "Constraint"], componentRows, 2, 58],
  ["QR Decision", "QR generation and scanner package choices.", [34, 70, 18, 72], ["Candidate", "Finding", "Decision", "Reference"], qrRows, 2, 62],
  ["Tag Occurrence Design", "Tag counting, suggestions, colors, and UI proof.", [28, 54, 50, 28], ["Capability", "Design", "Reason", "Proof"], tagRows, null, 58],
  ["Address Manager Design", "Generic address directory requirements.", [24, 70, 46, 32], ["Capability", "Design", "Use", "Proof"], addressRows, null, 62],
  ["Visual QA Checklist", "Large-screen browser validation checklist.", [22, 34, 18, 70, 18], ["Subbundle", "Route", "Viewport", "Visual Criteria", "Status"], visualRows, 4, 60],
  ["Risk Register", "Risks, mitigations, and owning gates.", [42, 16, 16, 72, 18], ["Risk", "Severity", "Status", "Mitigation", "Owner"], riskRows, 2, 62],
  ["Source References", "Primary files and web sources used for the architecture plan.", [34, 88, 24], ["Reference", "Path / URL", "Used By"], referenceRows, null, 48],
];

for (const [name, subtitle, widths, headers, rows, statusIndex, rowHeight] of sheets) {
  const sheet = workbook.worksheets.add(name);
  styleSheet(sheet, widths);
  title(sheet, name, subtitle, headers.length - 1);
  addTable(sheet, 3, 0, headers, rows, { rowHeight });
  if (statusIndex !== null) {
    colorStatusCells(sheet, 3, 0, rows, statusIndex);
  }
  const used = sheet.getUsedRange();
  used.format.autofitRows();
}

const readinessInspect = await workbook.inspect({
  kind: "table",
  range: "Readiness Summary!A4:D9",
  include: "values,formulas",
  tableMaxRows: 12,
  tableMaxCols: 6,
  maxChars: 6000,
});
console.log(readinessInspect.ndjson);

const errorInspect = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "formula error scan",
  maxChars: 6000,
});
console.log(errorInspect.ndjson);

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
await xlsx.save(path.join(outputDir, "accountapp-ui-shared-components-plan.xlsx"));

console.log(`Workbook saved to ${path.join(outputDir, "accountapp-ui-shared-components-plan.xlsx")}`);
console.log(`Previews saved to ${previewDir}`);
process.exit(0);
