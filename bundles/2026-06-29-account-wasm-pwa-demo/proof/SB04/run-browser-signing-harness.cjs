const fs = require("node:fs");
const http = require("node:http");
const path = require("node:path");
const { chromium } = require("playwright");

const repoRoot = path.resolve(__dirname, "../../../..");
const proofDir = __dirname;
const port = 48204;
let capturedBroadcastBody = "";

function contentType(filePath) {
  const extension = path.extname(filePath).toLowerCase();
  switch (extension) {
    case ".html":
      return "text/html; charset=utf-8";
    case ".js":
      return "text/javascript; charset=utf-8";
    case ".json":
      return "application/json; charset=utf-8";
    default:
      return "application/octet-stream";
  }
}

function createServer() {
  return http.createServer((request, response) => {
    const url = new URL(request.url, `http://127.0.0.1:${port}`);
    if (request.method === "POST" && url.pathname === "/api/v1/ledger/transactions") {
      const chunks = [];
      request.on("data", (chunk) => chunks.push(chunk));
      request.on("end", () => {
        capturedBroadcastBody = Buffer.concat(chunks).toString("utf8");
        response.writeHead(200, { "Content-Type": "application/json; charset=utf-8" });
        response.end(JSON.stringify({
          accepted: true,
          txId: { value: "tx-browser-draft" },
          ledgerSequence: 2,
          errors: []
        }));
      });
      return;
    }

    const safePath = path.normalize(decodeURIComponent(url.pathname)).replace(/^(\.\.[/\\])+/, "");
    const filePath = path.resolve(repoRoot, `.${safePath}`);

    if (!filePath.startsWith(repoRoot)) {
      response.writeHead(403);
      response.end("Forbidden");
      return;
    }

    fs.readFile(filePath, (error, data) => {
      if (error) {
        response.writeHead(404);
        response.end("Not found");
        return;
      }

      response.writeHead(200, { "Content-Type": contentType(filePath) });
      response.end(data);
    });
  });
}

function findBrowserExecutable() {
  const candidates = [
    "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
    "C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe",
    "C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe",
    "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe"
  ];

  return candidates.find((candidate) => fs.existsSync(candidate));
}

(async () => {
  const server = createServer();
  await new Promise((resolve) => server.listen(port, "127.0.0.1", resolve));

  let browser;
  try {
    const executablePath = findBrowserExecutable();
    browser = await chromium.launch({
      headless: true,
      executablePath
    });
    const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
    const harnessPath = "/bundles/2026-06-29-account-wasm-pwa-demo/proof/SB04/browser-signing-harness.html";
    await page.goto(`http://127.0.0.1:${port}${harnessPath}`, { waitUntil: "networkidle" });
    await page.waitForFunction(() => globalThis.__sb04Result?.success === true, null, { timeout: 10000 });
    const result = await page.evaluate(() => globalThis.__sb04Result);

    fs.writeFileSync(
      path.join(proofDir, "browser-signing-harness-result.json"),
      JSON.stringify(result, null, 2));
    fs.writeFileSync(
      path.join(proofDir, "sign-broadcast-network.json"),
      JSON.stringify({
        method: "POST",
        path: "/api/v1/ledger/transactions",
        body: capturedBroadcastBody,
        containsPrivateKeyField: /privateKey/i.test(capturedBroadcastBody),
        containsUnsignedDraft: capturedBroadcastBody.includes("\"signatureBundle\":{\"signatures\":[]")
      }, null, 2));
    await page.screenshot({
      path: path.join(proofDir, "signed-transaction-result.png"),
      fullPage: true
    });
    await page.screenshot({
      path: path.join(proofDir, "sign-broadcast-network.png"),
      fullPage: true
    });

    console.log(JSON.stringify(result, null, 2));
  } finally {
    if (browser) {
      await browser.close();
    }

    await new Promise((resolve) => server.close(resolve));
  }
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
