const { chromium } = require("playwright");
const { spawn, spawnSync } = require("child_process");
const fs = require("fs/promises");
const http = require("http");
const path = require("path");

const scriptDir = __dirname;
const proofDir = path.resolve(scriptDir, "..");
const bundleRoot = path.resolve(proofDir, "..", "..");
const repoRoot = path.resolve(bundleRoot, "..", "..");
const appProject = path.join(repoRoot, "examples", "CanDoItAll.Economy.AccountApp", "CanDoItAll.Economy.AccountApp.csproj");
const appOrigin = "http://127.0.0.1:5046";
const apiOrigin = "http://127.0.0.1:5299";
const browserDir = path.join(proofDir, "browser");
const transcriptPath = path.join(proofDir, "transcripts", "playwright-large-home.txt");
const databaseName = "CanDoItAll.Economy.AccountVault";
const permissionDataHash = "1111111111111111111111111111111111111111111111111111111111111111";
const ruleDataHash = "2222222222222222222222222222222222222222222222222222222222222222";
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
    ledgerId: wrap("default"),
    scenarioId: null
  };
}

function spendableOutput(address, txId, outputIndex, mainAmount, token, businessObject = null) {
  return {
    context: context(),
    outPointKey: { value: hash(outPointKeyHash(txId, outputIndex)) },
    outPoint: outPoint(txId, outputIndex),
    address: wrap(address),
    mainAmount: amount(mainAmount),
    token,
    businessObject,
    createdAt: "2026-07-05T12:00:00Z"
  };
}

function outPointKeyHash(txId, outputIndex) {
  return Buffer.from(`${txId}:${outputIndex}`, "utf8").toString("hex").padEnd(64, "0").slice(0, 64);
}

function token(tokenId, value) {
  return {
    tokenId: wrap(tokenId),
    amount: amount(value)
  };
}

function businessObject(objectId, dataHash) {
  return {
    objectId: wrap(objectId),
    version: 1,
    operation: 0,
    dataHash: hash(dataHash)
  };
}

function fakeSpendableOutputs(address) {
  return [
    spendableOutput(address, "tx-main-source", 0, 125, null),
    spendableOutput(address, "tx-token-source", 1, 0, token("ops.credit", 1000)),
    spendableOutput(address, "tx-permission-source", 0, 0, token("permission", 1)),
    spendableOutput(
      address,
      "tx-rule-permission-source",
      0,
      0,
      token("permission", 5),
      businessObject("permission:mint_new_tok_type:ops:browser-proof", permissionDataHash)),
    spendableOutput(
      address,
      "tx-rule-cashpool",
      0,
      0,
      token("rule", 1),
      businessObject("rule:cashpool:browser-proof", ruleDataHash))
  ];
}

function fakeTransactionSummary(address, txId = "tx-browser-proof") {
  return {
    context: context(),
    txId: wrap(txId),
    kind: 2,
    status: 1,
    timestamp: "2026-07-05T12:00:00Z",
    inputAddresses: [wrap(address)],
    outputAddresses: [wrap(address)],
    inputs: [
      {
        inputIndex: 0,
        previousOutPoint: outPoint("tx-browser-seed", 0),
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
      contentType: "application/vnd.candoitall.permission+json",
      dataHash: hash(permissionDataHash),
      inlineJson: "{\"act\":\"mint_new_tok_type\",\"ns\":\"ops\",\"rep\":true}",
      externalRef: null,
      itemType: 6
    }
  };
}

function createFakeApiServer() {
  const state = {
    requests: [],
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
    state.requests.push({ method: request.method, path: url.pathname + url.search, body });

    try {
      if (request.method === "GET") {
        const accountMatch = url.pathname.match(/^\/api\/v1\/ledger\/([^/]+)\/accounts\/([^/]+)\/([^/]+)$/);
        if (accountMatch) {
          const address = decodeURIComponent(accountMatch[2]);
          const resource = accountMatch[3];
          state.lastAddress = address;

          if (resource === "spendable-outputs") {
            return writeJson(response, 200, fakeSpendableOutputs(address));
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

function delay(milliseconds) {
  return new Promise(resolve => setTimeout(resolve, milliseconds));
}

async function gotoFresh(page) {
  await page.goto(appOrigin, { waitUntil: "domcontentloaded" });
  await page.waitForSelector("[data-testid='account-app-root']", { timeout: 60000 });
  await page.evaluate(dbName => new Promise(resolve => {
    localStorage.clear();
    sessionStorage.clear();
    const request = indexedDB.deleteDatabase(dbName);
    request.onsuccess = () => resolve();
    request.onerror = () => resolve();
    request.onblocked = () => resolve();
  }), databaseName);
  await page.reload({ waitUntil: "domcontentloaded" });
  await page.waitForSelector("[data-testid='first-run-account-flow']", { timeout: 60000 });
}

async function createAccount(page) {
  await page.getByTestId("account-name").fill("Browser proof account");
  await page.getByTestId("create-password").fill("ProofHorse123!");
  await page.getByTestId("create-confirm-password").fill("ProofHorse123!");
  await page.getByTestId("create-account").click();
  await page.waitForSelector("[data-testid='unlocked-account']", { timeout: 60000 });
  await page.waitForSelector("[data-testid='adapter-workspace-host']", { timeout: 60000 });
  await page.waitForSelector("[data-testid='ssr-vef-home']", { timeout: 60000 });
  await page.waitForFunction(() => document.body.innerText.includes("tx-token-source"), null, { timeout: 60000 });
}

async function assertHome(page) {
  const assertions = [];
  const bodyText = await page.locator("body").innerText();
  const normalizedBodyText = bodyText.toLowerCase();
  const panelHeaders = await page.$$eval(".cda-vef-panel-header", headers => headers.map(header => header.innerText.trim()));

  assert(bodyText.includes("Balances"), "Balances card is rendered", assertions);
  assert(!panelHeaders.some(text => text === "Token Sources"), "Standalone Token Sources card is absent", assertions);
  assert(normalizedBodyText.includes("source utxos"), "Balance card contains source UTXO section", assertions);
  assert(!bodyText.includes("COPY LEDGER ID") && !bodyText.includes("COPY SCENARIO ID"), "Compact metrics hide copy-control text", assertions);

  const sourceRows = await page.locator(".cda-vef-balance-source-row").count();
  assert(sourceRows === 1, `Exactly one token source row is rendered (${sourceRows})`, assertions);
  assert(bodyText.includes("tx-token-source:1"), "Real token source outpoint is visible", assertions);
  assert(bodyText.includes("1000"), "Real token source amount is visible", assertions);
  assert(!bodyText.includes("tx-permission-source"), "Unique one-token UTXO is hidden from source rows", assertions);
  assert(!bodyText.includes("tx-rule-permission-source"), "BO-backed permission source is hidden from source rows", assertions);
  assert(!bodyText.includes("tx-rule-cashpool"), "BO-backed rule token is hidden from source rows", assertions);

  const actionLabels = {
    "cda-action-send": "Send",
    "cda-action-token": "Token",
    "cda-action-address-directory": "Addresses",
    "cda-action-security": "Security",
    "cda-action-settings": "Settings",
    "cda-action-settings-backup": "Backup",
    "cda-action-refresh": "Refresh",
    "cda-action-subaccount": "Subaccount"
  };

  for (const [testId, label] of Object.entries(actionLabels)) {
    const labelLocator = page.locator(`[data-testid='${testId}'] .rz-button-text`);
    await labelLocator.waitFor({ state: "visible", timeout: 10000 });
    const actual = (await labelLocator.innerText()).trim();
    const isClipped = await labelLocator.evaluate(element => element.scrollWidth > element.clientWidth + 1);
    assert(actual === label, `${testId} renders visible label '${label}'`, assertions);
    assert(!isClipped, `${testId} label fits without clipping`, assertions);
  }

  const topRefreshBox = await page.getByTestId("refresh-workspace").boundingBox();
  const homeSendBox = await page.getByTestId("cda-action-send").boundingBox();
  assert(topRefreshBox && topRefreshBox.width <= 52, `Top-bar refresh remains icon-sized (${topRefreshBox?.width ?? "missing"}px)`, assertions);
  assert(homeSendBox && homeSendBox.width > 58, `Home action button has room for label (${homeSendBox?.width ?? "missing"}px)`, assertions);

  return assertions;
}

function assert(condition, message, assertions) {
  if (!condition) {
    throw new Error(message);
  }

  assertions.push(message);
}

async function screenshot(page, name) {
  await page.screenshot({
    path: path.join(browserDir, name),
    fullPage: true
  });
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

async function main() {
  await fs.mkdir(browserDir, { recursive: true });
  await fs.mkdir(path.dirname(transcriptPath), { recursive: true });
  await clearBrowserScreenshots();
  const fakeApi = createFakeApiServer();
  let appProcess;
  let browser;
  let page;
  const consoleMessages = [];
  const result = {
    Command: "NODE_PATH=%TEMP%\\cda-playwright-proof\\node_modules node bundles\\2026-07-05-accountapp-token-source-home-actions\\proof\\SB02\\scripts\\accountapp-home-large-proof.js",
    ExitCode: null,
    appOrigin,
    apiOrigin,
    viewport: { width: 1600, height: 1000 },
    assertions: [],
    screenshot: null,
    apiRequests: [],
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
    const context = await browser.newContext({
      viewport: result.viewport,
      serviceWorkers: "block"
    });
    page = await context.newPage();
    page.on("console", message => consoleMessages.push({ type: message.type(), text: message.text() }));
    page.on("pageerror", error => consoleMessages.push({ type: "pageerror", text: error.message }));

    await gotoFresh(page);
    await createAccount(page);
    result.assertions = await assertHome(page);
    await screenshot(page, "accountapp-home-large.png");
    result.screenshot = path.relative(repoRoot, path.join(browserDir, "accountapp-home-large.png")).replace(/\\/g, "/");
    result.apiRequests = fakeApi.state.requests;
    result.appOutputTail = appProcess.lines.join("").split(/\r?\n/).slice(-30);
    result.ExitCode = 0;
    await fs.writeFile(transcriptPath, JSON.stringify(result, null, 2));
  } catch (error) {
    result.ExitCode = 1;
    result.error = error.message;
    result.stack = error.stack;
    result.apiRequests = fakeApi.state.requests;
    result.appOutputTail = appProcess?.lines.join("").split(/\r?\n/).slice(-50) ?? [];
    if (page) {
      result.failureBodyText = await page.locator("body").innerText().catch(innerError => innerError.message);
      await screenshot(page, "failure.png").catch(() => undefined);
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

async function clearBrowserScreenshots() {
  const entries = await fs.readdir(browserDir).catch(() => []);
  await Promise.all(entries
    .filter(entry => entry.toLowerCase().endsWith(".png"))
    .map(entry => fs.rm(path.join(browserDir, entry), { force: true })));
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
