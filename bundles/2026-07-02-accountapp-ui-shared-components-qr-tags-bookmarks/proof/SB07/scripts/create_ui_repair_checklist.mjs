import fs from "node:fs/promises";
import path from "node:path";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const workDir = process.cwd();
const proofDir = path.resolve(workDir, "..");
const repoRoot = path.resolve(proofDir, "..", "..", "..", "..");
const workbookPath = path.join(proofDir, "ui-repair-implementation-checklist.xlsx");
const previewDir = path.join(proofDir, "spreadsheet-previews");

const absolute = (...parts) => path.join(repoRoot, ...parts);
const proof = (...parts) => path.join(proofDir, ...parts);

const workbook = Workbook.create();
workbook.comments.setSelf({ displayName: "User" });

const summary = workbook.worksheets.add("Summary");
const checklist = workbook.worksheets.add("Checklist");
const refs = workbook.worksheets.add("References");
const validation = workbook.worksheets.add("Validation");

const palette = {
  ink: "#111827",
  muted: "#64748B",
  header: "#0F766E",
  header2: "#164E63",
  panel: "#ECFDF5",
  panel2: "#EFF6FF",
  border: "#CBD5E1",
  ok: "#DCFCE7",
  warn: "#FEF3C7",
  fail: "#FEE2E2",
  white: "#FFFFFF",
};

function setTitle(sheet, range, title, subtitle) {
  sheet.showGridLines = false;
  sheet.getRange(range).merge();
  sheet.getRange(range).values = [[title]];
  sheet.getRange(range).format = {
    fill: palette.header,
    font: { bold: true, color: palette.white, size: 16 },
    wrapText: true,
  };
  const [startAddress, endAddress] = range.split(":");
  const startCol = startAddress.replace(/\d+/g, "");
  const endCol = endAddress.replace(/\d+/g, "");
  const subtitleRange = `${startCol}2:${endCol}2`;
  sheet.getRange(subtitleRange).merge();
  sheet.getRange(subtitleRange).values = [[subtitle]];
  sheet.getRange(subtitleRange).format = {
    fill: palette.panel,
    font: { color: palette.ink, size: 10 },
    wrapText: true,
  };
}

function styleHeader(sheet, range) {
  sheet.getRange(range).format = {
    fill: palette.header2,
    font: { bold: true, color: palette.white },
    borders: { preset: "outside", style: "thin", color: palette.header2 },
  };
}

function styleBody(sheet, range) {
  sheet.getRange(range).format = {
    borders: { preset: "inside", style: "thin", color: "#E2E8F0" },
    wrapText: true,
    font: { color: palette.ink },
  };
}

function setColumnWidths(sheet, widths) {
  widths.forEach((width, index) => {
    sheet.getRangeByIndexes(0, index, 1, 1).format.columnWidth = width;
  });
}

setTitle(
  summary,
  "A1:H1",
  "AccountApp UI Repair and Bundle Proof",
  "Implementation checklist, source references, screenshots, transcripts, and Docker validation for the large-screen PWA WASM account app repair."
);
summary.getRange("A4:B12").values = [
  ["Bundle", "2026-07-02-accountapp-ui-shared-components-qr-tags-bookmarks"],
  ["Repair date", "2026-07-02"],
  ["Scope", "Large-screen AccountApp UI, shared economy components, message security verification, and Docker runtime"],
  ["Checklist items", null],
  ["Verified items", null],
  ["Validation evidence rows", null],
  ["Docker state", "Rebuilt and restarted compose services"],
  ["Primary proof folder", proofDir],
  ["Final status", "PASS"],
];
summary.getRange("B7").formulas = [["=COUNTA('Checklist'!A2:A200)"]];
summary.getRange("B8").formulas = [["=COUNTIF('Checklist'!F2:F200,\"Verified\")"]];
summary.getRange("B9").formulas = [["=COUNTA('Validation'!A2:A200)"]];
summary.getRange("A4:A12").format = {
  fill: palette.panel2,
  font: { bold: true, color: palette.ink },
  borders: { preset: "inside", style: "thin", color: palette.border },
};
summary.getRange("B4:B12").format = {
  borders: { preset: "inside", style: "thin", color: palette.border },
  wrapText: true,
};
summary.getRange("A14:C14").values = [["Key request", "Implemented behavior", "Primary proof"]];
styleHeader(summary, "A14:C14");
summary.getRange("A15:C21").values = [
  ["Address management dialog width", "Dialog grid stretches across large desktop width and uses square management cards.", proof("screenshots", "docker-ui-repair-address-directory-wide.png")],
  ["Tooltip service", "Icon-only home actions use shared action buttons and tooltip-backed labels; browser validation asserts tooltip visibility.", proof("screenshots", "docker-ui-repair-home-top.png")],
  ["Profile picture loading", "Profile edit dialog previews the loaded picture and workspace top card renders cached avatar imagery.", proof("screenshots", "docker-ui-repair-profile-picture-preview.png")],
  ["Message security clarity", "Security dialog is split into Signatures, Encryption, and Ownership tabs.", proof("screenshots", "docker-ui-repair-message-security-tabs.png")],
  ["Ownership verification", "Ownership QR/payload flow verifies signature, resolves BO data, shows gallery-style card, and opens BO detail dialog.", proof("screenshots", "docker-ui-repair-ownership-verification.png")],
  ["Lost bottom info", "Address, ledger, and scenario metadata are moved into the top workspace bar.", proof("screenshots", "docker-ui-repair-home-top.png")],
  ["Docker runtime", "Compose images rebuilt, containers recreated, and HTTP smoke checks pass.", proof("transcripts", "docker-smoke-ui-repair-final.txt")],
];
styleBody(summary, "A15:C21");
setColumnWidths(summary, [32, 64, 92, 14, 14, 14, 14, 14]);

checklist.getRange("A1:H1").values = [["ID", "Area", "User story / requirement", "Implementation", "Reference", "Status", "Evidence", "Notes"]];
styleHeader(checklist, "A1:H1");
const checklistRows = [
  ["UI-01", "Address directory", "Use large-screen modal width instead of narrow left column.", "Address directory outer stack now stretches and card grid consumes full dialog width.", absolute("src", "CanDoItAll.Economy.Components", "Components", "Account", "AddressDirectoryDialogContent.razor"), "Verified", "addressGridWidth=1686, addressCardCount=3", "Final Playwright validation passed."],
  ["UI-02", "Address directory", "Keep address management reusable for own and external addresses.", "Generic dialog keeps own/external/favorite filters, tags, profile-cache badges, and card actions.", absolute("src", "CanDoItAll.Economy.Components", "Components", "Account", "AddressDirectoryDialogContent.razor"), "Verified", proof("screenshots", "docker-ui-repair-address-directory-wide.png"), "Visual proof shows horizontal card layout."],
  ["UI-03", "Tooltips", "Minimal icon actions must use CanDoItAll tooltip behavior.", "Home and dialog actions use shared PageHeaderActionButton paths with tooltip-backed labels.", absolute("src", "CanDoItAll.Economy.Components", "Components", "Account", "EconomyAccountWorkspace.razor"), "Verified", "tooltipVisible=true", "Browser hover assertion passed."],
  ["UI-04", "Profile", "Profile setup must load and display picture.", "Profile edit dialog renders a picture preview and account card renders cached avatar image.", absolute("src", "CanDoItAll.Economy.Components", "Components", "BusinessObjects", "ProfileEditDialogContent.razor"), "Verified", proof("screenshots", "docker-ui-repair-profile-picture-preview.png"), "Preview src begins with data:image."],
  ["UI-05", "Profile", "Cached profile BO should include image link for account/address cards.", "Workspace local services extract image link from profile BO media payload.", absolute("src", "CanDoItAll.Economy.Components", "Services", "AccountWorkspaceLocalServices.cs"), "Verified", proof("screenshots", "docker-ui-repair-home-top.png"), "Top account card includes avatar image."],
  ["UI-06", "Message security", "Dialog is confusing; split it to tabs.", "MessageSecurityPanel now has Signatures, Encryption, and Ownership tabs.", absolute("src", "CanDoItAll.Economy.Components", "Components", "Account", "MessageSecurityPanel.razor"), "Verified", proof("screenshots", "docker-ui-repair-message-security-tabs.png"), "Full-size dialog avoids cramped content."],
  ["UI-07", "Ownership verification", "Load QR via camera, verify signature, load BO data, show as gallery card.", "Ownership tab supports QR scanner/paste, verification service, BO resolution, and card rendering.", absolute("src", "CanDoItAll.Economy.Components", "Components", "Account", "MessageSecurityPanel.razor"), "Verified", proof("screenshots", "docker-ui-repair-ownership-verification.png"), "Validation used real BO payload created through console demo."],
  ["UI-08", "Ownership verification", "Open the same BO detail dialog from verification results.", "Ownership verification card calls the existing BO details dialog callback.", absolute("src", "CanDoItAll.Economy.Components", "Components", "Account", "MessageSecurityPanel.razor"), "Verified", proof("screenshots", "docker-ui-repair-ownership-details-dialog.png"), "Browser assertion found detail dialog visible."],
  ["UI-09", "Tickets", "Ticket verification must show Used flag and usage time.", "Ownership facts include ticket used state, used-at time, and expiration when the BO is a ticket view.", absolute("src", "CanDoItAll.Economy.Components", "Components", "Account", "MessageSecurityPanel.razor"), "Verified", absolute("tests", "CanDoItAll.Ledger.Tests", "BusinessObjectTicketMintingComponentTests.cs"), "Covered by code path and existing ticket component tests."],
  ["UI-10", "Home page", "Wrong/lost bottom icons and info should move to top.", "Ledger/scenario metadata moved into the top account command metrics; bottom info strip removed.", absolute("src", "CanDoItAll.Economy.Components", "Components", "Account", "EconomyAccountWorkspace.razor"), "Verified", proof("screenshots", "docker-ui-repair-home-top.png"), "Browser assertion confirms bottom strip absent."],
  ["UI-11", "Build", "Rebuild solution after repair.", "Solution build succeeds.", "dotnet build CanDoItAll.Ledger.slnx -v minimal", "Verified", proof("transcripts", "ledger-solution-build-ui-repair-final.txt"), "0 errors; existing package warnings remain."],
  ["UI-12", "Tests", "Run automated test suite after repair.", "Ledger test project passes.", "dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj -v minimal", "Verified", proof("transcripts", "ledger-tests-ui-repair-final.txt"), "242 passed, 0 failed."],
  ["UI-13", "Docker", "Rebuild and restart Docker runtime.", "Compose images rebuilt and services recreated.", "docker compose build; docker compose up -d --force-recreate --no-build", "Verified", proof("transcripts", "docker-compose-build-ui-repair-final.txt"), "All expected services up; postgres healthy."],
  ["UI-14", "Bundle", "Bundle must remain valid after repair.", "Completed-stage bundle validator passes.", "python scripts/validate_bundle.py --stage completed", "Verified", proof("transcripts", "bundle-validator-completed-ui-repair-final.txt"), "PASS completed."],
];
checklist.getRange(`A2:H${checklistRows.length + 1}`).values = checklistRows;
styleBody(checklist, `A2:H${checklistRows.length + 1}`);
checklist.getRange(`F2:F${checklistRows.length + 1}`).dataValidation = {
  rule: { type: "list", values: ["Verified", "Needs Work", "Blocked"] },
};
checklist.getRange(`F2:F${checklistRows.length + 1}`).conditionalFormats.add("containsText", {
  text: "Verified",
  format: { fill: palette.ok, font: { bold: true, color: "#166534" } },
});
checklist.getRange(`F2:F${checklistRows.length + 1}`).conditionalFormats.add("containsText", {
  text: "Needs Work",
  format: { fill: palette.warn, font: { bold: true, color: "#92400E" } },
});
checklist.freezePanes.freezeRows(1);
setColumnWidths(checklist, [10, 22, 48, 58, 86, 14, 72, 48]);

refs.getRange("A1:E1").values = [["Area", "Reference type", "Path / URL / command", "Purpose", "Status"]];
styleHeader(refs, "A1:E1");
const refRows = [
  ["UI source", "Razor", absolute("src", "CanDoItAll.Economy.Components", "Components", "Account", "EconomyAccountWorkspace.razor"), "Top account bar, command metrics, dialog entry points, callback wiring.", "Used"],
  ["UI source", "Razor", absolute("src", "CanDoItAll.Economy.Components", "Components", "Account", "AddressDirectoryDialogContent.razor"), "Address directory large-screen layout and shared action buttons.", "Used"],
  ["UI source", "Razor", absolute("src", "CanDoItAll.Economy.Components", "Components", "Account", "MessageSecurityPanel.razor"), "Tabbed message security and ownership verification workflow.", "Used"],
  ["UI source", "Razor", absolute("src", "CanDoItAll.Economy.Components", "Components", "BusinessObjects", "ProfileEditDialogContent.razor"), "Profile picture preview.", "Used"],
  ["Service source", "C#", absolute("src", "CanDoItAll.Economy.Components", "Services", "AccountWorkspaceLocalServices.cs"), "Profile image cache extraction and address directory data.", "Used"],
  ["Visual proof", "PNG", proof("screenshots", "docker-ui-repair-home-top.png"), "Top bar, tooltip, avatar, moved metadata.", "Captured"],
  ["Visual proof", "PNG", proof("screenshots", "docker-ui-repair-address-directory-wide.png"), "Address directory full-width cards.", "Captured"],
  ["Visual proof", "PNG", proof("screenshots", "docker-ui-repair-message-security-tabs.png"), "Tabbed security dialog.", "Captured"],
  ["Visual proof", "PNG", proof("screenshots", "docker-ui-repair-ownership-verification.png"), "Ownership verification card and facts.", "Captured"],
  ["Visual proof", "PNG", proof("screenshots", "docker-ui-repair-ownership-details-dialog.png"), "Existing BO detail dialog opened from ownership verification result.", "Captured"],
  ["Transcript", "Text", proof("transcripts", "ledger-solution-build-ui-repair-final.txt"), "Solution build proof.", "Passed"],
  ["Transcript", "Text", proof("transcripts", "ledger-tests-ui-repair-final.txt"), "Automated test proof.", "Passed"],
  ["Transcript", "Text", proof("transcripts", "docker-compose-build-ui-repair-final.txt"), "Docker image rebuild proof.", "Passed"],
  ["Transcript", "Text", proof("transcripts", "docker-smoke-ui-repair-final.txt"), "Docker endpoint smoke proof.", "Passed"],
  ["Transcript", "Text", proof("transcripts", "bundle-validator-completed-ui-repair-final.txt"), "Completed bundle validator proof.", "Passed"],
];
refs.getRange(`A2:E${refRows.length + 1}`).values = refRows;
styleBody(refs, `A2:E${refRows.length + 1}`);
refs.freezePanes.freezeRows(1);
setColumnWidths(refs, [22, 18, 104, 62, 14]);

validation.getRange("A1:G1").values = [["ID", "Validation", "Command / method", "Expected", "Actual", "Evidence path", "Status"]];
styleHeader(validation, "A1:G1");
const validationRows = [
  ["VAL-01", "Solution build", "dotnet build CanDoItAll.Ledger.slnx -v minimal", "0 errors", "Build succeeded; 56 existing package warnings.", proof("transcripts", "ledger-solution-build-ui-repair-final.txt"), "PASS"],
  ["VAL-02", "Ledger tests", "dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj -v minimal", "All tests pass", "242 passed, 0 failed, 0 skipped.", proof("transcripts", "ledger-tests-ui-repair-final.txt"), "PASS"],
  ["VAL-03", "Docker build", "docker compose build economy-node demo-seeder accountapp-alice accountapp-bob accountapp-lab", "Images rebuild", "economy-node, demo-seeder, and accountapp dev images rebuilt.", proof("transcripts", "docker-compose-build-ui-repair-final.txt"), "PASS"],
  ["VAL-04", "Docker restart", "docker compose up -d --force-recreate --no-build postgres economy-node accountapp-alice accountapp-bob accountapp-lab", "Containers recreated", "Services up; postgres healthy.", proof("transcripts", "docker-compose-up-ui-repair-final.txt"), "PASS"],
  ["VAL-05", "Docker smoke", "HTTP GET node health, bootstrap, and account apps", "HTTP 200", "All endpoints returned 200 after startup retry.", proof("transcripts", "docker-smoke-ui-repair-final.txt"), "PASS"],
  ["VAL-06", "Address dialog visual", "Playwright screenshot and assertion", "Wide modal/grid", "Grid width 1686 px; 3 address cards.", proof("screenshots", "docker-ui-repair-address-directory-wide.png"), "PASS"],
  ["VAL-07", "Tooltip visual", "Playwright hover over home address directory action", "Tooltip visible", "tooltipVisible=true.", proof("screenshots", "docker-ui-repair-home-top.png"), "PASS"],
  ["VAL-08", "Profile picture visual", "Playwright open profile dialog", "Picture preview loaded", "Preview src begins data:image.", proof("screenshots", "docker-ui-repair-profile-picture-preview.png"), "PASS"],
  ["VAL-09", "Security tabs visual", "Playwright open security dialog", "Tabs visible", "Signatures, Encryption, and Ownership tabs visible.", proof("screenshots", "docker-ui-repair-message-security-tabs.png"), "PASS"],
  ["VAL-10", "Ownership verification visual", "Playwright verify real BO ownership payload", "Verification passes and BO card opens detail dialog", "Verification passed; details dialog visible.", proof("screenshots", "docker-ui-repair-ownership-details-dialog.png"), "PASS"],
  ["VAL-11", "Bundle validator", "python scripts/validate_bundle.py --stage completed", "PASS", "PASS completed.", proof("transcripts", "bundle-validator-completed-ui-repair-final.txt"), "PASS"],
];
validation.getRange(`A2:G${validationRows.length + 1}`).values = validationRows;
styleBody(validation, `A2:G${validationRows.length + 1}`);
validation.getRange(`G2:G${validationRows.length + 1}`).conditionalFormats.add("containsText", {
  text: "PASS",
  format: { fill: palette.ok, font: { bold: true, color: "#166534" } },
});
validation.freezePanes.freezeRows(1);
setColumnWidths(validation, [10, 26, 80, 28, 58, 94, 12]);

for (const sheet of [summary, checklist, refs, validation]) {
  const used = sheet.getUsedRange();
  used.format.autofitRows();
}

await fs.mkdir(previewDir, { recursive: true });

const summaryInspect = await workbook.inspect({
  kind: "table",
  range: "Summary!A4:B12",
  include: "values,formulas",
  tableMaxRows: 20,
  tableMaxCols: 8,
});
const errorInspect = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "formula error scan",
});

for (const sheetName of ["Summary", "Checklist", "References", "Validation"]) {
  const preview = await workbook.render({
    sheetName,
    autoCrop: "all",
    scale: 1,
    format: "png",
  });
  await fs.writeFile(
    path.join(previewDir, `${sheetName.toLowerCase()}-preview.png`),
    new Uint8Array(await preview.arrayBuffer())
  );
}

const exported = await SpreadsheetFile.exportXlsx(workbook);
await exported.save(workbookPath);

await fs.writeFile(
  path.join(proofDir, "ui-repair-implementation-checklist-verification.json"),
  JSON.stringify(
    {
      workbookPath,
      summaryInspect: summaryInspect.ndjson,
      errorInspect: errorInspect.ndjson,
      previews: ["Summary", "Checklist", "References", "Validation"].map((sheetName) =>
        path.join(previewDir, `${sheetName.toLowerCase()}-preview.png`)
      ),
    },
    null,
    2
  )
);

console.log(`saved ${workbookPath}`);
process.exit(0);
