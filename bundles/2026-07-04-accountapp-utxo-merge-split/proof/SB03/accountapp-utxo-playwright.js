const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { chromium } = require('playwright');

const appUrl = 'http://localhost:5081/?view=wallet';
const proofDir = path.resolve('bundles/2026-07-04-accountapp-utxo-merge-split/proof/SB03');
const screenshotDir = path.join(proofDir, 'screenshots');
const backupPath = path.resolve('docker/demo-keys/accountapp-demo/imports/alice-accountapp-backup.json');
const backupPassword = 'accountapp-demo-password';
const localVaultPassword = 'alice-utxo-ui-proof-password';

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});

async function main() {
  await fs.mkdir(screenshotDir, { recursive: true });

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await context.newPage();
  const consoleMessages = [];

  page.on('console', message => {
    if (message.type() === 'error' || message.type() === 'warning') {
      consoleMessages.push(`${message.type()}: ${message.text()}`);
    }
  });
  page.on('pageerror', error => consoleMessages.push(`pageerror: ${error.message}`));

  try {
    await page.goto(appUrl, { waitUntil: 'networkidle' });
    await visible(page.getByTestId('account-app-root'));
    await importAliceIfNeeded(page);
    await waitForWorkspace(page);

    await page.getByTestId('cda-action-refresh').click();
    await visible(page.getByTestId('cda-action-send'), 60_000);

    await openSendDialog(page);
    const sendDialog = page.getByTestId('cda-send-transaction-dialog');
    await clickTextButton(sendDialog, 'Payment');
    const ownerAddress = await readOwnerAddress(sendDialog);
    await sendDialog.getByTestId('cda-send-payment-receiver').fill(ownerAddress);
    await sendDialog.getByTestId('cda-send-payment-amount').fill('1');
    await sendDialog.getByTestId('cda-send-payment-memo').fill('utxo proof payment');
    assert.deepEqual(await horizontalOverflowIssues(page, '[data-testid="cda-send-transaction-dialog"]'), []);
    await page.screenshot({ path: path.join(screenshotDir, '00-send-dialog.png'), fullPage: true });
    await submitActiveComposer(sendDialog);
    await waitForWorkspace(page);

    const sendRaw = await openLatestDetailsAndCapture(page, '00-send-details-io.png');
    assert.ok(sendRaw.vin.length >= 1, `send transaction should spend at least one input, got ${sendRaw.vin.length}`);
    assert.ok(sendRaw.vout.length >= 1, `send transaction should create at least one output, got ${sendRaw.vout.length}`);
    await resetToWorkspace(page);

    await openSendDialog(page);
    const splitDialog = page.getByTestId('cda-send-transaction-dialog');
    await clickTextButton(splitDialog, 'Split');
    await visible(splitDialog.getByTestId('cda-split-utxo-selector-row').first(), 30_000);
    await selectRowWithMainAtLeast(splitDialog.getByTestId('cda-split-utxo-selector-row'), 5);
    await splitDialog.getByTestId('cda-split-equal-count').fill('5');
    await splitDialog.getByTestId('cda-split-equal-amount').fill('1');
    await splitDialog.getByTestId('cda-split-apply-equal').click();
    await countEquals(splitDialog.getByTestId('cda-split-output-row'), 5);
    assert.equal(await splitDialog.getByText('Split output total exceeds selected inputs.').count(), 0);
    assert.deepEqual(await horizontalOverflowIssues(page, '[data-testid="cda-send-transaction-dialog"]'), []);
    await page.screenshot({ path: path.join(screenshotDir, '01-split-dialog.png'), fullPage: true });
    await submitActiveComposer(splitDialog);
    await waitForWorkspace(page);

    const splitRaw = await openLatestDetailsAndCapture(page, '02-split-details-io.png');
    assert.equal(splitRaw.vin.length, 1, 'split transaction should spend one selected input');
    assert.ok(splitRaw.vout.length >= 5, `split transaction should create requested outputs plus possible change, got ${splitRaw.vout.length}`);
    await resetToWorkspace(page);

    await openSendDialog(page);
    const mergeDialog = page.getByTestId('cda-send-transaction-dialog');
    await clickTextButton(mergeDialog, 'Merge');
    const mergeRows = mergeDialog.getByTestId('cda-merge-utxo-selector-row');
    await visible(mergeRows.first(), 30_000);
    const mergeRowCount = await mergeRows.count();
    assert.ok(mergeRowCount > 2, `expected more than two merge-eligible UTXOs after split, got ${mergeRowCount}`);
    for (let index = 0; index < 3; index++) {
      await toggleRowCheckbox(mergeRows.nth(index));
    }

    assert.deepEqual(await horizontalOverflowIssues(page, '[data-testid="cda-send-transaction-dialog"]'), []);
    await page.screenshot({ path: path.join(screenshotDir, '03-merge-dialog.png'), fullPage: true });
    await submitActiveComposer(mergeDialog);
    await waitForWorkspace(page);

    const mergeRaw = await openLatestDetailsAndCapture(page, '04-merge-details-io.png');
    assert.ok(mergeRaw.vin.length >= 3, `merge transaction should spend at least three selected inputs, got ${mergeRaw.vin.length}`);
    assert.equal(mergeRaw.vout.length, 1, 'merge transaction should create one output');

    await fs.writeFile(
      path.join(proofDir, 'playwright-results.json'),
      JSON.stringify(
        {
          url: appUrl,
          viewport: { width: 1920, height: 1080 },
          send: {
            txid: sendRaw.txid,
            inputCount: sendRaw.vin.length,
            outputCount: sendRaw.vout.length
          },
          split: {
            txid: splitRaw.txid,
            inputCount: splitRaw.vin.length,
            outputCount: splitRaw.vout.length
          },
          merge: {
            txid: mergeRaw.txid,
            inputCount: mergeRaw.vin.length,
            outputCount: mergeRaw.vout.length
          },
          screenshots: [
            'screenshots/00-send-dialog.png',
            'screenshots/00-send-details-io.png',
            'screenshots/01-split-dialog.png',
            'screenshots/02-split-details-io.png',
            'screenshots/03-merge-dialog.png',
            'screenshots/04-merge-details-io.png'
          ],
          consoleMessages
        },
        null,
        2),
      'utf8');

    console.log(JSON.stringify({
      status: 'passed',
      send: { txid: sendRaw.txid, inputs: sendRaw.vin.length, outputs: sendRaw.vout.length },
      split: { txid: splitRaw.txid, inputs: splitRaw.vin.length, outputs: splitRaw.vout.length },
      merge: { txid: mergeRaw.txid, inputs: mergeRaw.vin.length, outputs: mergeRaw.vout.length },
      screenshots: 6
    }, null, 2));
  } finally {
    await browser.close();
  }
}

async function importAliceIfNeeded(page) {
  if (await page.getByTestId('unlocked-account').isVisible({ timeout: 2_000 }).catch(() => false)) {
    return;
  }

  await visible(page.getByTestId('first-run-account-flow'), 30_000);
  const unlockButton = page.getByTestId('unlock-account');
  if (await unlockButton.isEnabled().catch(() => false)) {
    await page.getByTestId('unlock-password').fill(localVaultPassword);
    await unlockButton.click();
    await visible(page.getByTestId('unlocked-account'), 60_000);
    return;
  }

  const backupJson = await fs.readFile(backupPath, 'utf8');
  await page.getByTestId('backup-import-json').fill(backupJson);
  await page.getByTestId('backup-import-password').fill(backupPassword);
  await page.getByTestId('backup-new-password').fill(localVaultPassword);
  await page.getByTestId('backup-confirm-password').fill(localVaultPassword);
  await page.getByTestId('import-backup').click();
  await visible(page.getByTestId('unlocked-account'), 60_000);
}

async function resetToWorkspace(page) {
  await page.goto(appUrl, { waitUntil: 'networkidle' });
  await importAliceIfNeeded(page);
  await waitForWorkspace(page);
}

async function waitForWorkspace(page) {
  await visible(page.getByTestId('unlocked-account'), 60_000);
  await visible(page.getByTestId('ssr-vef-home'), 90_000);
  await visible(page.getByTestId('cda-recent-transactions'), 90_000);
}

async function openSendDialog(page) {
  await page.getByTestId('cda-action-send').click();
  await visible(page.getByTestId('cda-send-transaction-dialog'), 30_000);
}

async function readOwnerAddress(dialog) {
  const text = await dialog.textContent();
  const match = text?.match(/cdia[0-9A-Za-z]+/);
  assert.ok(match, 'owner address should be visible in the send dialog subtitle');
  return match[0];
}

async function submitActiveComposer(dialog) {
  await clickTextButton(dialog, 'Submit transaction');
  await hidden(dialog, 90_000);
}

async function openLatestDetailsAndCapture(page, screenshotName) {
  await page.getByTestId('cda-action-refresh').click();
  await visible(page.getByTestId('cda-transaction-details-button').first(), 60_000);
  await page.getByTestId('cda-transaction-details-button').first().click();
  const details = page.getByTestId('tx-details-layout');
  await visible(details, 60_000);

  await clickTextButton(details, 'Inputs & outputs');
  assert.deepEqual(await horizontalOverflowIssues(page, '[data-testid="tx-details-layout"]'), []);
  await page.screenshot({ path: path.join(screenshotDir, screenshotName), fullPage: true });

  await clickTextButton(details, 'Raw tx');
  const rawText = await page.getByTestId('tx-raw-json').innerText();
  return JSON.parse(rawText);
}

async function clickTextButton(scope, text) {
  const escaped = text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const button = scope.locator('button').filter({ hasText: new RegExp(escaped, 'i') }).first();
  await visible(button, 30_000);
  await button.click();
}

async function toggleRowCheckbox(row) {
  const checkbox = row.locator('input[type="checkbox"], [role="checkbox"], .rz-chkbox-box, .rz-chkbox').first();
  await visible(row, 30_000);
  if (await checkbox.count()) {
    await checkbox.click({ force: true });
    return;
  }

  await row.click({ force: true });
}

async function selectRowWithMainAtLeast(rows, minimumAmount) {
  const count = await rows.count();
  for (let index = 0; index < count; index++) {
    const row = rows.nth(index);
    const text = await row.textContent();
    const amount = parseMainAmount(text || '');
    if (amount >= minimumAmount) {
      await toggleRowCheckbox(row);
      return amount;
    }
  }

  throw new Error(`No selectable plain UTXO row had at least ${minimumAmount} main.`);
}

function parseMainAmount(text) {
  const matches = [...text.matchAll(/([0-9]+(?:\.[0-9]+)?)\s*main/gi)];
  if (matches.length === 0) {
    return 0;
  }

  return Math.max(...matches.map(match => Number.parseFloat(match[1])));
}

async function horizontalOverflowIssues(page, rootSelector) {
  return await page.evaluate(selector => {
    const root = document.querySelector(selector);
    if (!root) {
      return [`missing root ${selector}`];
    }

    const candidates = [
      root,
      ...Array.from(root.querySelectorAll('[data-testid], .cda-split-output-row, .cda-utxo-selector__row, .cda-vef-tx-scroll-list, .cda-vef-tx-io-grid'))
    ];

    return candidates
      .map(element => {
        const html = element;
        const rect = html.getBoundingClientRect();
        const testId = html.getAttribute('data-testid') || html.className?.toString() || html.tagName;
        const overflowsOwnBox = html.scrollWidth > html.clientWidth + 6;
        const overflowsViewport = rect.left < -6 || rect.right > window.innerWidth + 6;
        return overflowsOwnBox || overflowsViewport
          ? `${testId}: scroll=${html.scrollWidth}/${html.clientWidth}, rect=${Math.round(rect.left)}..${Math.round(rect.right)}`
          : null;
      })
      .filter(value => value !== null)
      .slice(0, 20);
  }, rootSelector);
}

async function visible(locator, timeout = 30_000) {
  await locator.waitFor({ state: 'visible', timeout });
}

async function hidden(locator, timeout = 30_000) {
  await locator.waitFor({ state: 'hidden', timeout });
}

async function countEquals(locator, expected, timeout = 30_000) {
  const started = Date.now();
  while (Date.now() - started < timeout) {
    const count = await locator.count();
    if (count === expected) {
      return;
    }

    await new Promise(resolve => setTimeout(resolve, 250));
  }

  assert.equal(await locator.count(), expected);
}
