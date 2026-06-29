const { chromium } = require("playwright");
const { spawn, spawnSync } = require("child_process");
const fs = require("fs/promises");
const http = require("http");
const path = require("path");

const proofDir = __dirname;
const repoRoot = path.resolve(proofDir, "../../../..");
const appProject = path.join(repoRoot, "examples", "CanDoItAll.Economy.AccountApp", "CanDoItAll.Economy.AccountApp.csproj");
const appOrigin = "http://127.0.0.1:5045";
const apiOrigin = "http://127.0.0.1:5299";
const screenshotsDir = path.join(proofDir, "screenshots");
const transcriptPath = path.join(proofDir, "accountapp-browser-proof-output.json");
const databaseName = "CanDoItAll.Economy.AccountVault";
const browserCandidates = [
  process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE,
  "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
  "C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe",
  "C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe",
  "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe"
].filter(Boolean);

function wrap(value) {
  return { value };
}

function amount(value) {
  return { value };
}

function hash(value) {
  return wrap(value);
}

function outPoint(txId, outputIndex) {
  return { txId: wrap(txId), outputIndex };
}

function context() {
  return {
    ledgerId: wrap("account-app-ledger"),
    scenarioId: wrap("browser-demo")
  };
}

function fakeSpendableOutput(address) {
  return {
    context: context(),
    outPointKey: { value: hash("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa") },
    outPoint: outPoint("tx-account-app-seed", 0),
    address: wrap(address),
    mainAmount: amount(100),
    token: null,
    businessObject: null,
    createdAt: "2026-06-29T12:00:00Z"
  };
}

function fakeTransactionSummary(address, txId = "tx-account-app-seed") {
  return {
    context: context(),
    txId: wrap(txId),
    kind: 2,
    status: 1,
    timestamp: "2026-06-29T12:00:00Z",
    inputAddresses: [wrap(address)],
    outputAddresses: [wrap(address)],
    inputs: [
      {
        inputIndex: 0,
        previousOutPoint: outPoint("tx-source", 0),
        address: wrap(address),
        outPointKey: null,
        signatureBundle: { signatures: [] },
        signaturePayloadHash: null
      }
    ],
    outputs: [
      {
        outputIndex: 0,
        address: wrap(address),
        mainAmount: amount(1),
        token: null,
        businessObject: null,
        metadataHash: null
      }
    ],
    data: {
      contentType: "application/json",
      dataHash: null,
      inlineJson: "{\"source\":\"fake-node-api\"}",
      externalRef: null,
      itemType: 0
    }
  };
}

function createFakeApiServer() {
  const state = {
    requests: [],
    broadcasts: [],
    lastAddress: null
  };

  const server = http.createServer(async (request, response) => {
    response.setHeader("Access-Control-Allow-Origin", "*");
    response.setHeader("Access-Control-Allow-Headers", "content-type, authorization, x-cda-economy-key");
    response.setHeader("Access-Control-Allow-Methods", "GET,POST,OPTIONS");

    if (request.method === "OPTIONS") {
      response.writeHead(204);
      response.end();
      return;
    }

    const url = new URL(request.url, apiOrigin);
    const body = await readBody(request);
    state.requests.push({
      method: request.method,
      path: url.pathname + url.search,
      body
    });

    try {
      if (request.method === "GET") {
        const accountMatch = url.pathname.match(/^\/api\/v1\/ledger\/([^/]+)\/accounts\/([^/]+)\/([^/]+)$/);
        if (accountMatch) {
          const address = decodeURIComponent(accountMatch[2]);
          const resource = accountMatch[3];
          state.lastAddress = address;
          if (resource === "spendable-outputs") {
            return writeJson(response, 200, [fakeSpendableOutput(address)]);
          }

          if (resource === "business-objects") {
            return writeJson(response, 200, []);
          }

          if (resource === "transactions") {
            return writeJson(response, 200, [fakeTransactionSummary(address)]);
          }
        }

        const transactionMatch = url.pathname.match(/^\/api\/v1\/ledger\/([^/]+)\/transactions\/([^/]+)$/);
        if (transactionMatch) {
          const address = state.lastAddress ?? "cdia1placeholder";
          return writeJson(response, 200, fakeTransactionSummary(address, decodeURIComponent(transactionMatch[2])));
        }

        if (url.pathname === "/api/v1/health" || url.pathname === "/api/v1/health/readiness") {
          return writeJson(response, 200, { status: "Healthy" });
        }
      }

      if (request.method === "POST" && url.pathname === "/api/v1/ledger/transactions") {
        const transaction = JSON.parse(body);
        state.broadcasts.push(transaction);
        return writeJson(response, 200, {
          accepted: true,
          txId: wrap("tx-account-app-broadcast"),
          ledgerSequence: 77,
          errors: []
        });
      }

      return writeJson(response, 404, { title: "Not found", detail: `${request.method} ${url.pathname}` });
    } catch (error) {
      return writeJson(response, 500, { title: "Fake API error", detail: error.message });
    }
  });

  return { server, state };
}

function readBody(request) {
  return new Promise((resolve, reject) => {
    let body = "";
    request.setEncoding("utf8");
    request.on("data", chunk => {
      body += chunk;
    });
    request.on("end", () => resolve(body));
    request.on("error", reject);
  });
}

function writeJson(response, statusCode, body) {
  response.writeHead(statusCode, { "Content-Type": "application/json" });
  response.end(JSON.stringify(body));
}

async function listen(server, port) {
  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(port, "127.0.0.1", resolve);
  });
}

function startAccountApp() {
  const child = spawn(
    "dotnet",
    [
      "run",
      "--no-build",
      "--no-launch-profile",
      "--project",
      appProject,
      "--urls",
      appOrigin
    ],
    {
      cwd: repoRoot,
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: "Development"
      },
      stdio: ["ignore", "pipe", "pipe"]
    });

  const lines = [];
  child.stdout.on("data", data => {
    lines.push(data.toString());
  });
  child.stderr.on("data", data => {
    lines.push(data.toString());
  });

  return { child, lines };
}

async function waitForHttp(url, timeoutMs = 90000) {
  const start = Date.now();
  let lastError;
  while (Date.now() - start < timeoutMs) {
    try {
      const response = await fetch(url);
      if (response.status < 500) {
        return;
      }
      lastError = new Error(`HTTP ${response.status}`);
    } catch (error) {
      lastError = error;
    }
    await delay(500);
  }

  throw new Error(`Timed out waiting for ${url}: ${lastError?.message ?? "no response"}`);
}

async function waitFor(predicate, description, timeoutMs = 30000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (predicate()) {
      return;
    }
    await delay(100);
  }

  throw new Error(`Timed out waiting for ${description}`);
}

function delay(milliseconds) {
  return new Promise(resolve => setTimeout(resolve, milliseconds));
}

async function deleteVaultDatabase(page) {
  await page.evaluate(dbName => new Promise((resolve, reject) => {
    const request = indexedDB.deleteDatabase(dbName);
    request.onsuccess = () => resolve();
    request.onerror = () => reject(request.error);
    request.onblocked = () => resolve();
  }), databaseName);
}

async function generateP256KeyPair(page) {
  return await page.evaluate(async () => {
    const keyPair = await crypto.subtle.generateKey(
      { name: "ECDSA", namedCurve: "P-256" },
      true,
      ["sign", "verify"]);
    const publicKey = new Uint8Array(await crypto.subtle.exportKey("spki", keyPair.publicKey));
    const privateKey = new Uint8Array(await crypto.subtle.exportKey("pkcs8", keyPair.privateKey));
    const toBase64 = bytes => {
      let binary = "";
      for (let index = 0; index < bytes.length; index += 0x8000) {
        binary += String.fromCharCode(...bytes.subarray(index, index + 0x8000));
      }
      return btoa(binary);
    };
    return {
      publicKeyBase64: toBase64(publicKey),
      privateKeyBase64: toBase64(privateKey)
    };
  });
}

async function gotoFresh(page) {
  await page.goto(appOrigin, { waitUntil: "domcontentloaded" });
  await page.waitForSelector("[data-testid='account-app-root']", { timeout: 60000 });
}

async function screenshot(page, name) {
  await page.screenshot({
    path: path.join(screenshotsDir, name),
    fullPage: true
  });
}

async function inspectBrowserStorage(page) {
  return await page.evaluate(dbName => new Promise(resolve => {
    const audit = {
      indexedDbRecords: [],
      localStorage: Object.fromEntries(Object.entries(localStorage)),
      sessionStorage: Object.fromEntries(Object.entries(sessionStorage))
    };

    const request = indexedDB.open(dbName);
    request.onerror = () => resolve(audit);
    request.onsuccess = () => {
      const database = request.result;
      if (!database.objectStoreNames.contains("accounts")) {
        database.close();
        resolve(audit);
        return;
      }

      const transaction = database.transaction("accounts", "readonly");
      const store = transaction.objectStore("accounts");
      const getAll = store.getAll();
      getAll.onsuccess = () => {
        audit.indexedDbRecords = getAll.result ?? [];
      };
      transaction.oncomplete = () => {
        database.close();
        resolve(audit);
      };
      transaction.onabort = () => {
        database.close();
        resolve(audit);
      };
    };
    request.onupgradeneeded = () => {
      request.result.close();
      resolve(audit);
    };
  }), databaseName);
}

async function runMainBrowserFlow(browser, apiState, consoleMessages, setCurrentPage) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1050 }, serviceWorkers: "allow" });
  const page = await context.newPage();
  setCurrentPage(page);
  page.on("console", message => consoleMessages.push({ type: message.type(), text: message.text() }));
  page.on("pageerror", error => consoleMessages.push({ type: "pageerror", text: error.message }));

  await gotoFresh(page);
  await deleteVaultDatabase(page);
  await page.reload({ waitUntil: "domcontentloaded" });
  await page.waitForSelector("[data-testid='first-run-account-flow']", { timeout: 60000 });
  await screenshot(page, "01-first-run-desktop.png");

  const password = "CorrectHorse123!";
  await page.getByTestId("account-name").fill("Browser proof account");
  await page.getByTestId("create-password").fill(password);
  await page.getByTestId("create-confirm-password").fill(password);
  await page.getByTestId("create-account").click();
  await page.waitForSelector("[data-testid='unlocked-account']", { timeout: 60000 });
  await page.waitForSelector("[data-testid='wasm-workspace']", { timeout: 60000 });
  await screenshot(page, "02-unlocked-workspace.png");

  const address = await page.locator("[data-testid='unlocked-account'] strong").nth(1).innerText();

  await page.getByTestId("load-transaction-detail").click();
  await page.waitForSelector("[data-testid='transaction-detail']", { timeout: 30000 });
  await screenshot(page, "03-transaction-detail.png");

  await page.getByTestId("submit-signed-payment").click();
  await waitFor(() => apiState.broadcasts.length > 0, "signed broadcast request");
  await page.waitForSelector(".cda-economy-transaction-receipt", { timeout: 30000 }).catch(() => undefined);
  await screenshot(page, "04-signed-broadcast-result.png");

  await page.getByTestId("export-backup").click();
  await page.waitForSelector("[data-testid='backup-export'] textarea", { timeout: 30000 });
  const backupJson = await page.locator("[data-testid='backup-export'] textarea").inputValue();
  const backup = JSON.parse(backupJson);
  await screenshot(page, "05-backup-export.png");

  const manifest = await page.evaluate(async () => {
    const response = await fetch("/manifest.webmanifest");
    return await response.json();
  });
  const serviceWorkerRegistrations = await page.evaluate(async () => {
    if (!("serviceWorker" in navigator)) {
      return -1;
    }
    const registrations = await navigator.serviceWorker.getRegistrations();
    return registrations.length;
  });
  const storageAudit = await inspectBrowserStorage(page);

  await context.close();
  return { address, backupJson, backup, manifest, serviceWorkerRegistrations, storageAudit, password };
}

async function runBackupImportFlow(browser, backupJson, originalPassword, consoleMessages, setCurrentPage) {
  const context = await browser.newContext({ viewport: { width: 1366, height: 950 }, serviceWorkers: "allow" });
  const page = await context.newPage();
  setCurrentPage(page);
  page.on("console", message => consoleMessages.push({ type: message.type(), text: message.text() }));
  page.on("pageerror", error => consoleMessages.push({ type: "pageerror", text: error.message }));

  await gotoFresh(page);
  await page.waitForSelector("[data-testid='first-run-account-flow']", { timeout: 60000 });
  const newPassword = "ImportedHorse123!";
  await page.getByTestId("backup-import-json").fill(backupJson);
  await page.getByTestId("backup-import-password").fill(originalPassword);
  await page.getByTestId("backup-new-password").fill(newPassword);
  await page.getByTestId("backup-confirm-password").fill(newPassword);
  await page.getByTestId("import-backup").click();
  await page.waitForSelector("[data-testid='unlocked-account']", { timeout: 60000 });
  await screenshot(page, "06-imported-backup-unlocked.png");

  await page.reload({ waitUntil: "domcontentloaded" });
  await page.waitForSelector("[data-testid='first-run-account-flow']", { timeout: 60000 });
  await page.getByTestId("unlock-password").fill(newPassword);
  await page.getByTestId("unlock-account").click();
  await page.waitForSelector("[data-testid='unlocked-account']", { timeout: 60000 });
  await screenshot(page, "07-unlock-stored-vault-after-reload.png");
  const storageAudit = await inspectBrowserStorage(page);

  await context.close();
  return { storageAudit };
}

async function runPastedKeyFlow(browser, consoleMessages, setCurrentPage) {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, isMobile: true, serviceWorkers: "allow" });
  const page = await context.newPage();
  setCurrentPage(page);
  page.on("console", message => consoleMessages.push({ type: message.type(), text: message.text() }));
  page.on("pageerror", error => consoleMessages.push({ type: "pageerror", text: error.message }));

  await gotoFresh(page);
  await page.waitForSelector("[data-testid='first-run-account-flow']", { timeout: 60000 });
  await screenshot(page, "08-first-run-mobile.png");

  const keyPair = await generateP256KeyPair(page);
  await page.getByTestId("pasted-key-name").fill("Pasted proof key");
  await page.getByTestId("pasted-key-password").fill("PastedHorse123!");
  await page.getByTestId("pasted-key-confirm-password").fill("PastedHorse123!");
  await page.getByTestId("pasted-public-key").fill(keyPair.publicKeyBase64);
  await page.getByTestId("pasted-private-key").fill(keyPair.privateKeyBase64);
  await page.getByTestId("import-pasted-key").click();
  await page.waitForSelector("[data-testid='unlocked-account']", { timeout: 60000 });
  await screenshot(page, "09-pasted-key-import-mobile.png");
  const storageAudit = await inspectBrowserStorage(page);

  await context.close();
  return { storageAudit };
}

function findForbiddenPrivateKeyKeys(value, pathPrefix = "$") {
  const matches = [];
  if (Array.isArray(value)) {
    value.forEach((item, index) => {
      matches.push(...findForbiddenPrivateKeyKeys(item, `${pathPrefix}[${index}]`));
    });
    return matches;
  }

  if (value && typeof value === "object") {
    for (const [key, nested] of Object.entries(value)) {
      const nestedPath = `${pathPrefix}.${key}`;
      if (/privateKey/i.test(key)) {
        matches.push(nestedPath);
      }
      matches.push(...findForbiddenPrivateKeyKeys(nested, nestedPath));
    }
  }

  return matches;
}

function assertBroadcastSafe(apiState) {
  if (apiState.broadcasts.length === 0) {
    throw new Error("No signed broadcast request was captured.");
  }

  const broadcast = apiState.broadcasts[0];
  const input = broadcast.inputs?.[0];
  const signatures = input?.signatureBundle?.signatures ?? [];
  if (signatures.length === 0) {
    throw new Error("Broadcast transaction did not include a browser signature.");
  }

  const signature = signatures[0];
  if (!signature.signatureBytes || !signature.publicKeyMaterial || !signature.payloadHash) {
    throw new Error("Broadcast signature envelope is missing signature bytes, public key material, or payload hash.");
  }

  const forbiddenKeys = findForbiddenPrivateKeyKeys(broadcast);
  if (forbiddenKeys.length > 0) {
    throw new Error(`Broadcast contained private-key-like field names: ${forbiddenKeys.join(", ")}`);
  }

  const broadcastBody = JSON.stringify(broadcast);
  if (/privateKey/i.test(broadcastBody)) {
    throw new Error("Broadcast body contained private-key-like text.");
  }
}

function assertStorageSafe(storageAudits) {
  for (const [name, audit] of Object.entries(storageAudits)) {
    if (!Array.isArray(audit.indexedDbRecords) || audit.indexedDbRecords.length === 0) {
      throw new Error(`${name} storage audit did not find an IndexedDB vault record.`);
    }

    for (const record of audit.indexedDbRecords) {
      if (!record.encryptedPrivateKeyBase64 || !record.kdfSaltBase64 || !record.nonceBase64) {
        throw new Error(`${name} vault record is missing encrypted private key, KDF salt, or nonce.`);
      }

      const forbiddenRecordKeys = Object.keys(record)
        .filter(key => /password|privateKeyMaterial|rawPrivate/i.test(key));
      if (forbiddenRecordKeys.length > 0) {
        throw new Error(`${name} vault record contains raw secret-like fields: ${forbiddenRecordKeys.join(", ")}`);
      }
    }

    const browserStorageText = JSON.stringify({
      localStorage: audit.localStorage,
      sessionStorage: audit.sessionStorage
    });
    if (/password|privateKey|privateKeyMaterial/i.test(browserStorageText)) {
      throw new Error(`${name} local/session storage contains secret-like text.`);
    }
  }
}

async function main() {
  await fs.mkdir(screenshotsDir, { recursive: true });
  await clearScreenshots();
  const fakeApi = createFakeApiServer();
  let appProcess;
  let browser;
  let currentPage;
  const consoleMessages = [];
  const result = {
    appOrigin,
    apiOrigin,
    screenshots: [],
    apiRequests: [],
    broadcastAssertion: null,
    manifest: null,
    serviceWorkerRegistrations: null,
    storageAuditAssertion: null,
    storageAudits: null,
    accountAddress: null,
    backupFormat: null,
    consoleMessages,
    appOutputTail: []
  };

  try {
    await listen(fakeApi.server, 5299);
    appProcess = startAccountApp();
    await waitForHttp(appOrigin);

    browser = await chromium.launch({
      headless: true,
      executablePath: await findBrowserExecutable()
    });
    const mainFlow = await runMainBrowserFlow(browser, fakeApi.state, consoleMessages, page => currentPage = page);
    currentPage = null;
    const backupImportFlow = await runBackupImportFlow(browser, mainFlow.backupJson, mainFlow.password, consoleMessages, page => currentPage = page);
    currentPage = null;
    const pastedKeyFlow = await runPastedKeyFlow(browser, consoleMessages, page => currentPage = page);
    currentPage = null;

    assertBroadcastSafe(fakeApi.state);
    const storageAudits = {
      createdVault: mainFlow.storageAudit,
      importedVault: backupImportFlow.storageAudit,
      pastedKeyVault: pastedKeyFlow.storageAudit
    };
    assertStorageSafe(storageAudits);
    result.broadcastAssertion = "signed transaction included browser signature/public key material and no private key fields";
    result.storageAuditAssertion = "IndexedDB vault records contain encrypted private keys only; local/session storage contains no private key or password text";
    result.storageAudits = storageAudits;
    result.accountAddress = mainFlow.address;
    result.backupFormat = mainFlow.backup.format;
    result.manifest = mainFlow.manifest;
    result.serviceWorkerRegistrations = mainFlow.serviceWorkerRegistrations;
    result.apiRequests = fakeApi.state.requests;
    result.screenshots = (await fs.readdir(screenshotsDir)).sort();
    result.appOutputTail = appProcess.lines.join("").split(/\r?\n/).slice(-30);
    await fs.writeFile(transcriptPath, JSON.stringify(result, null, 2));
  } catch (error) {
    result.error = error.message;
    result.stack = error.stack;
    result.apiRequests = fakeApi.state.requests;
    result.screenshots = await fs.readdir(screenshotsDir).catch(() => []);
    result.appOutputTail = appProcess?.lines.join("").split(/\r?\n/).slice(-50) ?? [];
    if (currentPage) {
      result.failureBodyText = await currentPage.locator("body").innerText().catch(innerError => innerError.message);
      await screenshot(currentPage, "failure.png").catch(() => undefined);
      result.screenshots = await fs.readdir(screenshotsDir).catch(() => []);
    }

    await fs.writeFile(transcriptPath, JSON.stringify(result, null, 2));
    throw error;
  } finally {
    if (browser) {
      await browser.close().catch(() => undefined);
    }

    await new Promise(resolve => fakeApi.server.close(resolve));
    if (appProcess?.child && !appProcess.child.killed) {
      killProcessTree(appProcess.child.pid);
    }
  }
}

async function clearScreenshots() {
  const entries = await fs.readdir(screenshotsDir).catch(() => []);
  await Promise.all(entries
    .filter(entry => entry.toLowerCase().endsWith(".png"))
    .map(entry => fs.rm(path.join(screenshotsDir, entry), { force: true })));
}

async function findBrowserExecutable() {
  for (const candidate of browserCandidates) {
    try {
      await fs.access(candidate);
      return candidate;
    } catch {
    }
  }

  return undefined;
}

function killProcessTree(pid) {
  if (!pid) {
    return;
  }

  if (process.platform === "win32") {
    spawnSync("taskkill", ["/pid", String(pid), "/t", "/f"], { stdio: "ignore" });
  } else {
    try {
      process.kill(-pid, "SIGTERM");
    } catch {
      try {
        process.kill(pid, "SIGTERM");
      } catch {
      }
    }
  }
}

main().catch(async error => {
  try {
    await fs.access(transcriptPath);
  } catch {
    await fs.writeFile(transcriptPath, JSON.stringify({
      error: error.message,
      stack: error.stack
    }, null, 2));
  }
  console.error(error);
  process.exitCode = 1;
});
