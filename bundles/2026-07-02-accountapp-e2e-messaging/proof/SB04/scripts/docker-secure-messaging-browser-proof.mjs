import { chromium } from 'playwright';
import fs from 'node:fs';
import path from 'node:path';

export async function runDockerSecureMessagingProof(options = {}) {
  const root = options.root ?? (typeof nodeRepl !== 'undefined' ? nodeRepl.cwd : process.cwd());
  const proofRoot = path.join(root, 'bundles/2026-07-02-accountapp-e2e-messaging/proof/SB04');
  const screenshotDir = path.join(proofRoot, 'screenshots');
  const summaryPath = path.join(proofRoot, 'docker-secure-messaging-browser-proof.json');
  const transcriptPath = path.join(proofRoot, 'transcripts/docker-secure-messaging-browser-proof-node-repl.txt');
  const password = 'accountapp-demo-password';
  const aliceUrl = 'http://localhost:5081/';
  const bobUrl = 'http://localhost:5082/';
  const aliceBackupPath = path.join(root, 'docker/demo-keys/accountapp-demo/imports/alice-accountapp-backup.json');
  const bobBackupPath = path.join(root, 'docker/demo-keys/accountapp-demo/imports/bob-accountapp-backup.json');
  const attachmentPath = path.join(proofRoot, 'scripts/docker-secure-message-attachment.txt');

  fs.mkdirSync(screenshotDir, { recursive: true });
  fs.mkdirSync(path.dirname(transcriptPath), { recursive: true });
  fs.writeFileSync(attachmentPath, 'docker attachment plaintext ' + new Date().toISOString(), 'utf8');

  const consoleMessages = [];
  const apiErrors = [];
  const suffix = Date.now().toString(36);
  const bobToAlice = {
    subject: `Docker Bob to Alice ${suffix}`,
    body: `Bob secure body ${suffix}`,
  };
  const aliceToBob = {
    subject: `Docker Alice to Bob ${suffix}`,
    body: `Alice secure body ${suffix}`,
  };
  const attachmentText = fs.readFileSync(attachmentPath, 'utf8');
  const summary = {
    aliceUrl,
    bobUrl,
    suffix,
    checks: {},
    screenshots: [],
    consoleMessages,
    apiErrors,
  };

  const transcript = [`START ${new Date().toISOString()}`];
  let browser;
  try {
    browser = await chromium.launch({ headless: true });
    const aliceContext = await browser.newContext({ viewport: { width: 1760, height: 990 } });
    const bobContext = await browser.newContext({ viewport: { width: 1760, height: 990 } });
    const alice = await aliceContext.newPage();
    const bob = await bobContext.newPage();
    for (const page of [alice, bob]) {
      page.on('console', message => {
        if (['error', 'warning'].includes(message.type())) {
          consoleMessages.push({ page: page.url(), type: message.type(), text: message.text() });
        }
      });
      page.on('response', response => {
        const url = response.url();
        if (response.status() >= 400 && (url.includes('/api/') || url.includes('/ipfs-api/'))) {
          apiErrors.push({ page: page.url(), status: response.status(), url });
        }
      });
    }

    await ensureUnlocked(alice, aliceUrl, aliceBackupPath, password);
    await ensureUnlocked(bob, bobUrl, bobBackupPath, password);
    const aliceAddress = readBackupAddress(aliceBackupPath);
    const bobAddress = readBackupAddress(bobBackupPath);
    summary.checks.addresses = { alice: aliceAddress, bob: bobAddress };
    transcript.push(`UNLOCKED alice=${aliceAddress} bob=${bobAddress}`);

    await openMessaging(bob);
    const bobToAliceTxId = await sendSecureMessage(bob, aliceAddress, bobToAlice.subject, bobToAlice.body, attachmentPath);
    transcript.push(`SENT bob->alice attachment tx=${bobToAliceTxId}`);

    await openMessaging(alice, true);
    await openMessageByTxId(alice, bobToAliceTxId, bobToAlice.subject, bobToAlice.body);
    const beforeAttachmentDecrypt = await alice.locator('body').textContent();
    if ((beforeAttachmentDecrypt ?? '').includes(attachmentText)) {
      throw new Error('Attachment plaintext was visible before explicit decrypt.');
    }

    await alice.locator('[data-testid="secure-message-decrypt-attachment"]').first().click();
    await expectText(alice.locator('[data-testid="secure-message-decrypted-attachment"]'), attachmentText, 30_000);
    await capture(alice, summary, screenshotDir, 'docker-secure-mail-alice-decrypted-attachment.png');
    transcript.push('DECRYPTED alice attachment on request');

    const aliceToBobTxId = await sendSecureMessage(alice, bobAddress, aliceToBob.subject, aliceToBob.body);
    transcript.push(`SENT alice->bob no attachment tx=${aliceToBobTxId}`);

    await openMessaging(bob, true);
    await openMessageByTxId(bob, aliceToBobTxId, aliceToBob.subject, aliceToBob.body);
    await capture(bob, summary, screenshotDir, 'docker-secure-mail-bob-decrypted-reply.png');
    transcript.push('OPENED bob reply');

    summary.checks.aliceLayout = await messagingLayoutMetrics(alice);
    summary.checks.bobLayout = await messagingLayoutMetrics(bob);
    assert(summary.checks.aliceLayout.workspaceWidth > 1200, 'Alice messaging workspace is too narrow.');
    assert(summary.checks.aliceLayout.horizontalOverflow === false, 'Alice messaging page has horizontal overflow.');
    assert(summary.checks.bobLayout.workspaceWidth > 1200, 'Bob messaging workspace is too narrow.');
    assert(summary.checks.bobLayout.horizontalOverflow === false, 'Bob messaging page has horizontal overflow.');
    assert(apiErrors.length === 0, `API errors were captured: ${JSON.stringify(apiErrors)}`);

    await aliceContext.close();
    await bobContext.close();
    summary.passed = true;
    transcript.push('PASS');
  } catch (error) {
    summary.passed = false;
    summary.error = String(error?.stack ?? error);
    transcript.push(`FAIL ${summary.error}`);
    throw error;
  } finally {
    if (browser) {
      await browser.close();
    }

    transcript.push(`END ${new Date().toISOString()}`);
    fs.writeFileSync(summaryPath, JSON.stringify(summary, null, 2));
    fs.writeFileSync(transcriptPath, transcript.join('\n') + '\n');
  }

  return summary;
}

async function ensureUnlocked(page, appUrl, backupPath, password) {
  await page.goto(appUrl, { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() =>
    document.querySelector('[data-testid="ssr-vef-home"]')
      || document.querySelector('[data-testid="first-run-account-flow"]')
      || document.querySelector('[data-testid="unlock-password"]'),
    null,
    { timeout: 120_000 });

  if (await page.locator('[data-testid="ssr-vef-home"]').count()) {
    return;
  }

  if (await page.locator('[data-testid="first-run-account-flow"]').count()) {
    const backupJson = fs.readFileSync(backupPath, 'utf8');
    await page.locator('[data-testid="backup-import-json"]').fill(backupJson);
    await page.locator('[data-testid="backup-import-password"]').fill(password);
    await page.locator('[data-testid="backup-new-password"]').fill(password);
    await page.locator('[data-testid="backup-confirm-password"]').fill(password);
    await page.locator('button[data-testid="import-backup"]').click();
  } else if (await page.locator('[data-testid="unlock-password"]').count()) {
    await page.locator('[data-testid="unlock-password"]').fill(password);
    await page.locator('[data-testid="unlock-account"]').click();
  }

  await page.waitForSelector('[data-testid="wasm-workspace"]', { timeout: 120_000 });
  await page.waitForSelector('[data-testid="ssr-vef-home"]', { timeout: 120_000 });
}

async function openMessaging(page, refresh = false) {
  if (refresh) {
    await clickRefresh(page);
  }

  const nav = page.getByRole('button', { name: /^Messaging$/ });
  if (await nav.count()) {
    await nav.first().click();
  } else {
    await page.getByText('Messaging', { exact: true }).click();
  }

  await page.waitForSelector('[data-testid="secure-messaging-workspace"]', { timeout: 60_000 });
}

async function clickRefresh(page) {
  const refresh = page.locator('[data-testid="refresh-workspace"]').first();
  if (await refresh.count()) {
    await refresh.click();
  } else {
    await page.getByRole('button', { name: /Refresh/i }).first().click();
  }

  await page.waitForTimeout(900);
}

function readBackupAddress(backupPath) {
  const backup = JSON.parse(fs.readFileSync(backupPath, 'utf8'));
  const address = backup?.record?.address?.value;
  if (!address?.startsWith('cdia1')) {
    throw new Error(`Backup address was not found in ${backupPath}.`);
  }

  return address;
}

async function sendSecureMessage(page, receiver, subject, body, attachmentFile = null) {
  await openMessaging(page);
  await fillFirst(page, [
    '[data-testid="secure-message-receiver"] input',
    'input[data-testid="secure-message-receiver"]',
    'input[placeholder="Address or saved contact"]',
    '[data-testid="secure-message-receiver"]',
  ], receiver);
  await fillFirst(page, [
    '[data-testid="secure-message-subject"] input',
    'input[data-testid="secure-message-subject"]',
    '[data-testid="secure-message-subject"]',
  ], subject);
  await fillFirst(page, [
    '[data-testid="secure-message-body"] textarea',
    'textarea[data-testid="secure-message-body"]',
    '[data-testid="secure-message-body"]',
  ], body);

  if (attachmentFile) {
    await page.locator('[data-testid="secure-message-attachment-input"]').setInputFiles(attachmentFile);
    await expectText(page.locator('.cda-secure-mail__draft-attachments'), path.basename(attachmentFile), 30_000);
  }

  const submitResponse = page.waitForResponse(
    response => response.request().method() === 'POST'
      && /\/api\/v1\/ledger\/transactions$/.test(new URL(response.url()).pathname),
    { timeout: 120_000 });
  await page.locator('[data-testid="secure-message-send"]').click();
  const response = await submitResponse;
  const json = await response.json().catch(() => ({}));
  const accepted = json.accepted ?? json.Accepted;
  if (response.status() >= 400 || accepted === false) {
    throw new Error(`Secure message transaction was rejected for "${subject}": ${JSON.stringify(json)}`);
  }
  const txId = readAcceptedTxId(json);
  await clickRefresh(page);
  const text = await page.locator('body').textContent();
  if ((text ?? '').includes('Cannot send secure message') || (text ?? '').includes('failed')) {
    throw new Error(`Secure message send failed: ${text}`);
  }

  return txId;
}

async function openMessageWithSubject(page, subject, body) {
  const rows = page.locator('[data-testid="secure-message-row"]');
  await expectVisible(rows.first(), 60_000);
  const count = await rows.count();
  const attempts = count;
  let lastReaderText = '';

  for (let index = 0; index < attempts; index++) {
    await rows.nth(index).click();
    await page.waitForSelector('[data-testid="secure-message-plaintext"], .cda-secure-mail__reader', { timeout: 30_000 });
    await page.waitForTimeout(600);
    lastReaderText = await page.locator('.cda-secure-mail__reader').textContent() ?? '';
    if (lastReaderText.includes(subject) && lastReaderText.includes(body)) {
      return;
    }
  }

  throw new Error(`Message with subject "${subject}" was not opened. Last reader text: ${lastReaderText}`);
}

async function openMessageByTxId(page, txId, subject, body) {
  const row = page.locator(`[data-testid="secure-message-row"][data-txid="${escapeCssAttribute(txId)}"]`).first();
  await expectVisible(row, 90_000);
  await row.click();
  await page.waitForSelector('[data-testid="secure-message-plaintext"], .cda-secure-mail__reader', { timeout: 30_000 });
  await expectText(page.locator('.cda-secure-mail__reader'), subject, 30_000);
  await expectText(page.locator('.cda-secure-mail__reader'), body, 30_000);
}

function readAcceptedTxId(json) {
  const txId = json?.txId?.value
    ?? json?.TxId?.Value
    ?? json?.txId
    ?? json?.TxId;
  if (typeof txId !== 'string' || txId.length === 0) {
    throw new Error(`Accepted secure message response did not contain a tx id: ${JSON.stringify(json)}`);
  }

  return txId;
}

function escapeCssAttribute(value) {
  return String(value).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
}

async function fillFirst(page, selectors, value) {
  const errors = [];
  for (const selector of selectors) {
    const locator = page.locator(selector).first();
    if (await locator.count()) {
      try {
        await locator.fill(value);
        return;
      } catch (error) {
        errors.push(`${selector}: ${error.message}`);
      }
    }
  }

  throw new Error(`No fillable selector found. Tried: ${errors.join('; ')}`);
}

async function expectText(locator, expected, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const text = await locator.textContent().catch(() => '');
    if (expected instanceof RegExp ? expected.test(text ?? '') : (text ?? '').includes(expected)) {
      return;
    }

    await new Promise(resolve => setTimeout(resolve, 200));
  }

  throw new Error(`Expected text not found: ${expected}`);
}

async function expectVisible(locator, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (await locator.isVisible().catch(() => false)) {
      return;
    }

    await new Promise(resolve => setTimeout(resolve, 200));
  }

  throw new Error('Expected element to become visible.');
}

async function waitForCondition(predicate, timeoutMs, message) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (await predicate()) {
      return;
    }

    await new Promise(resolve => setTimeout(resolve, 250));
  }

  throw new Error(message);
}

async function messagingLayoutMetrics(page) {
  return await page.evaluate(() => {
    const workspace = document.querySelector('[data-testid="secure-messaging-workspace"]');
    const grid = document.querySelector('.cda-secure-mail__grid');
    const workspaceRect = workspace?.getBoundingClientRect();
    const gridRect = grid?.getBoundingClientRect();
    return {
      viewportWidth: window.innerWidth,
      documentScrollWidth: document.documentElement.scrollWidth,
      horizontalOverflow: document.documentElement.scrollWidth > window.innerWidth + 2,
      workspaceWidth: workspaceRect?.width ?? 0,
      gridWidth: gridRect?.width ?? 0,
    };
  });
}

async function capture(page, summary, screenshotDir, fileName) {
  const fullPath = path.join(screenshotDir, fileName);
  await page.screenshot({ path: fullPath, fullPage: false });
  summary.screenshots.push(fullPath);
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}
