import fs from "node:fs/promises";
import path from "node:path";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const bundleRoot = path.resolve("bundles/2026-07-03-secure-message-bo-privacy");
const outputPath = path.join(bundleRoot, "inventories", "secure-message-privacy-impact-map.xlsx");
const previewPath = path.join(bundleRoot, "proof", "workbook", "impact-map-preview.png");

const workbook = Workbook.create();

function addSheet(name, headers, rows, widths = []) {
  const sheet = workbook.worksheets.add(name);
  sheet.showGridLines = false;
  const matrix = [headers, ...rows];
  const range = sheet.getRangeByIndexes(0, 0, matrix.length, headers.length);
  range.values = matrix;
  range.format.wrapText = true;
  range.format.verticalAlignment = "top";
  range.format.borders = { preset: "all", style: "thin", color: "#D9E2EF" };
  const header = sheet.getRangeByIndexes(0, 0, 1, headers.length);
  header.format.fill.color = "#1F4E78";
  header.format.font.color = "#FFFFFF";
  header.format.font.bold = true;
  header.format.horizontalAlignment = "center";
  sheet.freezePanes.freezeRows(1);
  widths.forEach((width, index) => {
    sheet.getRangeByIndexes(0, index, matrix.length, 1).format.columnWidth = width;
  });
  return sheet;
}

addSheet(
  "Requirements",
  ["ID", "Raw note", "Invariant", "Subbundle", "Proof gate", "Status"],
  [
    ["REQ-01", "Subject appears in BO objectId", "No subject/name text after bo:secure-message prefix", "SB02", "Adapter tx raw assertion", "Completed"],
    ["REQ-02", "Attachment IPFS hash/URI public", "No attachment locator or metadata in public tx data", "SB03", "SecureLedgerMessage red-team test", "Completed"],
    ["REQ-03", "Attachments still must open", "Sender and receiver decrypt message and attachment with ECDH/AES-GCM", "SB03", "Alice/Bob integration test", "Completed"],
    ["REQ-04", "Old messages exist", "Legacy v1 payloads remain parseable/openable", "SB03", "Compatibility fixture test", "Completed"],
    ["REQ-05", "Escaped stateJson raw view", "Raw tx expands parseable nested JSON", "SB04/SB05", "Component/browser proof", "Completed"],
    ["REQ-06", "Newest-to-oldest tx list", "Transaction history keeps ledger-sequence ordering", "SB05", "Ordering tests", "Completed"],
    ["REQ-07", "Docker WASM proof", "Rebuilt containers send/read messages with attachments", "SB06", "Docker/browser proof", "Completed"]
  ],
  [14, 34, 44, 18, 34, 14]
);

addSheet(
  "Impact Surface",
  ["Area", "File", "Current behavior", "Privacy / compatibility risk", "Planned change", "Tests"],
  [
    ["Message builder", "src/CanDoItAll.Economy.BusinessObjects/SecureLedgerMessages.cs", "Encrypts subject/body but stores public attachment DataItem list.", "CID/URI and file metadata visible in raw tx.", "Move attachment locator and metadata into encrypted content; public list empty for new messages; v1 fallback.", "SecureLedgerMessageTests"],
    ["WASM adapter", "src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs", "Creates BO id from secure-message subject.", "Subject visible in object id and external ref.", "Use opaque secure-message id factory.", "AccountWorkspaceAdapterTests"],
    ["ID factory", "src/CanDoItAll.Economy.Components/BusinessObjectCreateIdFactory.cs", "Normalizes display names into public BO ids.", "Correct for public BOs, wrong for encrypted messages.", "Add scoped opaque id helper.", "BusinessObjectCreateIdentityTests plus adapter tests"],
    ["Payload envelope", "src/CanDoItAll.Economy.BusinessObjects/BusinessObjectPayload.cs", "Writes stateJson as string.", "Nested JSON appears escaped and readers require string.", "Add tolerant envelope parsing; consider object-state writer only after readers support it.", "Envelope compatibility tests"],
    ["In-memory read model", "src/CanDoItAll.Economy.BusinessObjects/InProcessLedgerGateway.cs", "Requires stateJson string.", "Object-valued state would be ignored.", "Accept stateJson string, stateJson object, or state object.", "InProcess gateway tests"],
    ["Node read model", "src/CanDoItAll.Economy.Node/EconomyLedgerReadModelService.cs", "Deserializes BusinessObjectPayloadEnvelope directly.", "Object-valued state would fail projection.", "Use tolerant parser returning compact string state.", "Node/API contract tests"],
    ["Raw tx dialog", "src/CanDoItAll.Economy.Components/Components/Transactions/TransactionDetailsDialogContent.razor", "Parses outer inlineJson only.", "Nested stateJson displays escaped JSON.", "Expand parseable nested state for raw tab.", "TransactionUiComponentTests and screenshot"],
    ["Docker account apps", "docker-compose.yml; examples/CanDoItAll.Economy.AccountApp", "Runtime proof required.", "Local tests may miss WASM/API/IPFS behavior.", "Rebuild/restart and validate Alice/Bob flows.", "Docker/browser proof"]
  ],
  [20, 58, 42, 42, 48, 34]
);

addSheet(
  "Data Classification",
  ["Field", "Public allowed?", "Reason", "Storage location after bundle", "Negative search values"],
  [
    ["BO type secure-message", "Yes", "Needed to classify transaction", "BO object type/id prefix", "none"],
    ["BO id opaque suffix", "Yes", "Identifier only; no user text", "businessObject.objectId", "subject words"],
    ["Sender/receiver addresses", "Yes", "Ledger routing facts", "inputs/outputs and minimal payload", "none"],
    ["Signature public key", "Yes", "Required for transaction signature verification/public-key discovery", "vin signatures", "none"],
    ["Subject/body", "No", "Message content", "Encrypted content cipherText", "subject, body"],
    ["Attachment file name/description/content type/size", "No", "Attachment metadata is message content", "Encrypted content descriptor", "filename, description, type, size"],
    ["Attachment IPFS CID/URI/DataItem", "No", "Locator can correlate file with message", "Encrypted content descriptor", "Qm..., ipfs-api/cat"],
    ["Attachment ciphertext hash", "No", "Can help correlate storage object", "Encrypted content descriptor", "sha256 hash"]
  ],
  [34, 18, 42, 34, 28]
);

addSheet(
  "Test Matrix",
  ["Layer", "Test / Proof", "Covers", "Command / Evidence", "Status"],
  [
    ["Unit", "SecureLedgerMessage public payload red-team", "REQ-02", "dotnet test ... SecureLedgerMessageTests", "Completed"],
    ["Integration", "WASM adapter send/open Alice/Bob", "REQ-01, REQ-03", "dotnet test ... AccountWorkspaceAdapterTests", "Completed"],
    ["Compatibility", "Legacy v1 secure-message fixture", "REQ-04", "dotnet test ... legacy fixture", "Completed"],
    ["Component", "Raw tx nested state expansion", "REQ-05", "dotnet test ... TransactionUiComponentTests", "Completed"],
    ["Regression", "Transaction ordering newest-first", "REQ-06", "dotnet test ... TransactionUiComponentTests", "Completed"],
    ["Build", "AccountApp build", "WASM compile", "dotnet build examples/CanDoItAll.Economy.AccountApp", "Completed"],
    ["Docker", "Compose rebuild/restart", "REQ-07", "docker compose build/up + health", "Completed"],
    ["Browser", "Raw tx large-screen screenshot", "REQ-05, REQ-07", "output/playwright/*.png", "Completed"]
  ],
  [18, 38, 20, 48, 14]
);

addSheet(
  "Closure Checklist",
  ["Item", "Owner subbundle", "Pass criteria", "Proof path", "Status"],
  [
    ["Prepared bundle validation", "Preparation", "validate_bundle.py --stage prepared passes", "command transcript", "Completed"],
    ["Subject not in public tx", "SB02", "No forbidden subject tokens in raw transaction JSON", "test output", "Completed"],
    ["CID/URI not in public tx", "SB03", "No CID/URI/file metadata before decrypt", "test output", "Completed"],
    ["Message attachments decrypt", "SB03/SB06", "Sender and receiver can open attachments", "test and Docker proof", "Completed"],
    ["Raw tx structured", "SB05/SB06", "Nested state is expanded in raw tab", "screenshot", "Completed"],
    ["Docker up", "SB06", "Node/account apps healthy after rebuild", "docker ps + health", "Completed"]
  ],
  [34, 22, 48, 34, 14]
);

const preview = await workbook.render({ sheetName: "Impact Surface", autoCrop: "all", scale: 1, format: "png" });
await fs.writeFile(previewPath, new Uint8Array(await preview.arrayBuffer()));
const inspect = await workbook.inspect({ kind: "sheet", include: "id,name", sheetName: "Impact Surface", range: "A1:F9" });
console.log(JSON.stringify(inspect, null, 2));
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(outputPath);
