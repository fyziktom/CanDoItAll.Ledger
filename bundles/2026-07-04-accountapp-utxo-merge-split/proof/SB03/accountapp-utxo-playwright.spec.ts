import { expect, test, type Locator, type Page } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';

const appUrl = 'http://localhost:5081/?view=wallet';
const proofDir = path.resolve('bundles/2026-07-04-accountapp-utxo-merge-split/proof/SB03');
const screenshotDir = path.join(proofDir, 'screenshots');
const backupPath = path.resolve('docker/demo-keys/accountapp-demo/imports/alice-accountapp-backup.json');
const backupPassword = 'accountapp-demo-password';
const localVaultPassword = 'alice-utxo-ui-proof-password';

test.setTimeout(240_000);

test('AccountApp split and merge UTXO transactions from the send dialog', async ({ page }) => {
  await fs.mkdir(screenshotDir, { recursive: true });
  await page.setViewportSize({ width: 1920, height: 1080 });

  const consoleMessages: string[] = [];
  page.on('console', message => {
    if (message.type() === 'error' || message.type() === 'warning') {
      consoleMessages.push(`${message.type()}: ${message.text()}`);
    }
  });
  page.on('pageerror', error => consoleMessages.push(`pageerror: ${error.message}`));

  await page.goto(appUrl, { waitUntil: 'networkidle' });
  await expect(page.getByTestId('account-app-root')).toBeVisible();
  await importAliceIfNeeded(page);
  await waitForWorkspace(page);

  await page.getByTestId('cda-action-refresh').click();
  await expect(page.getByTestId('cda-action-send')).toBeVisible({ timeout: 60_000 });

  await openSendDialog(page);
  const splitDialog = page.getByTestId('cda-send-transaction-dialog');
  await clickTextButton(splitDialog, 'Split');
  await expect(splitDialog.getByTestId('cda-split-utxo-selector-row').first()).toBeVisible({ timeout: 30_000 });
  await toggleRowCheckbox(splitDialog.getByTestId('cda-split-utxo-selector-row').first());
  await splitDialog.getByTestId('cda-split-equal-count').fill('5');
  await splitDialog.getByTestId('cda-split-equal-amount').fill('1000');
  await splitDialog.getByTestId('cda-split-apply-equal').click();
  await expect(splitDialog.getByTestId('cda-split-output-row')).toHaveCount(5);
  const splitDialogOverflow = await horizontalOverflowIssues(page, '[data-testid="cda-send-transaction-dialog"]');
  expect(splitDialogOverflow).toEqual([]);
  await page.screenshot({ path: path.join(screenshotDir, '01-split-dialog.png'), fullPage: true });
  await submitActiveComposer(splitDialog);
  await waitForWorkspace(page);

  const splitRaw = await openLatestDetailsAndCapture(page, '02-split-details-io.png');
  expect(splitRaw.vin).toHaveLength(1);
  expect(splitRaw.vout.length).toBeGreaterThanOrEqual(5);
  await closeActiveDialog(page);

  await openSendDialog(page);
  const mergeDialog = page.getByTestId('cda-send-transaction-dialog');
  await clickTextButton(mergeDialog, 'Merge');
  const mergeRows = mergeDialog.getByTestId('cda-merge-utxo-selector-row');
  await expect(mergeRows.first()).toBeVisible({ timeout: 30_000 });
  expect(await mergeRows.count()).toBeGreaterThan(2);
  for (let index = 0; index < 3; index++) {
    await toggleRowCheckbox(mergeRows.nth(index));
  }

  const mergeDialogOverflow = await horizontalOverflowIssues(page, '[data-testid="cda-send-transaction-dialog"]');
  expect(mergeDialogOverflow).toEqual([]);
  await page.screenshot({ path: path.join(screenshotDir, '03-merge-dialog.png'), fullPage: true });
  await submitActiveComposer(mergeDialog);
  await waitForWorkspace(page);

  const mergeRaw = await openLatestDetailsAndCapture(page, '04-merge-details-io.png');
  expect(mergeRaw.vin.length).toBeGreaterThanOrEqual(3);
  expect(mergeRaw.vout).toHaveLength(1);

  await fs.writeFile(
    path.join(proofDir, 'playwright-results.json'),
    JSON.stringify(
      {
        url: appUrl,
        viewport: { width: 1920, height: 1080 },
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
});

async function importAliceIfNeeded(page: Page): Promise<void> {
  if (await page.getByTestId('unlocked-account').isVisible({ timeout: 2_000 }).catch(() => false)) {
    return;
  }

  const backupJson = await fs.readFile(backupPath, 'utf8');
  await expect(page.getByTestId('first-run-account-flow')).toBeVisible({ timeout: 30_000 });
  await page.getByTestId('backup-import-json').fill(backupJson);
  await page.getByTestId('backup-import-password').fill(backupPassword);
  await page.getByTestId('backup-new-password').fill(localVaultPassword);
  await page.getByTestId('backup-confirm-password').fill(localVaultPassword);
  await page.getByTestId('import-backup').click();
  await expect(page.getByTestId('unlocked-account')).toBeVisible({ timeout: 60_000 });
}

async function waitForWorkspace(page: Page): Promise<void> {
  await expect(page.getByTestId('unlocked-account')).toBeVisible({ timeout: 60_000 });
  await expect(page.getByTestId('ssr-vef-home')).toBeVisible({ timeout: 90_000 });
  await expect(page.getByTestId('cda-recent-transactions')).toBeVisible({ timeout: 90_000 });
}

async function openSendDialog(page: Page): Promise<void> {
  await page.getByTestId('cda-action-send').click();
  await expect(page.getByTestId('cda-send-transaction-dialog')).toBeVisible({ timeout: 30_000 });
}

async function submitActiveComposer(dialog: Locator): Promise<void> {
  await clickTextButton(dialog, 'Submit transaction');
  await expect(dialog).toBeHidden({ timeout: 90_000 });
}

async function openLatestDetailsAndCapture(page: Page, screenshotName: string): Promise<any> {
  await page.getByTestId('cda-action-refresh').click();
  await expect(page.getByTestId('cda-transaction-details-button').first()).toBeVisible({ timeout: 60_000 });
  await page.getByTestId('cda-transaction-details-button').first().click();
  const details = page.getByTestId('tx-details-layout');
  await expect(details).toBeVisible({ timeout: 60_000 });

  await clickTextButton(details, 'Inputs & outputs');
  const detailsOverflow = await horizontalOverflowIssues(page, '[data-testid="tx-details-layout"]');
  expect(detailsOverflow).toEqual([]);
  await page.screenshot({ path: path.join(screenshotDir, screenshotName), fullPage: true });

  await clickTextButton(details, 'Raw tx');
  const rawText = await page.getByTestId('tx-raw-json').innerText();
  return JSON.parse(rawText);
}

async function closeActiveDialog(page: Page): Promise<void> {
  await page.keyboard.press('Escape');
  if (await page.getByTestId('tx-details-layout').isHidden({ timeout: 2_000 }).catch(() => false)) {
    return;
  }

  const closeButton = page.locator('button').filter({ hasText: /close/i }).last();
  if (await closeButton.count()) {
    await closeButton.click({ force: true });
    await expect(page.getByTestId('tx-details-layout')).toBeHidden({ timeout: 10_000 });
    return;
  }

  throw new Error('Could not close active transaction detail dialog.');
}

async function clickTextButton(scope: Locator, text: string): Promise<void> {
  const escaped = text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const button = scope.locator('button').filter({ hasText: new RegExp(escaped, 'i') }).first();
  await expect(button).toBeVisible({ timeout: 30_000 });
  await button.click();
}

async function toggleRowCheckbox(row: Locator): Promise<void> {
  const checkbox = row.locator('input[type="checkbox"], [role="checkbox"], .rz-chkbox-box, .rz-chkbox').first();
  await expect(row).toBeVisible({ timeout: 30_000 });
  if (await checkbox.count()) {
    await checkbox.click({ force: true });
    return;
  }

  await row.click({ force: true });
}

async function horizontalOverflowIssues(page: Page, rootSelector: string): Promise<string[]> {
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
        const html = element as HTMLElement;
        const rect = html.getBoundingClientRect();
        const testId = html.getAttribute('data-testid') ?? html.className?.toString() ?? html.tagName;
        const overflowsOwnBox = html.scrollWidth > html.clientWidth + 6;
        const overflowsViewport = rect.left < -6 || rect.right > window.innerWidth + 6;
        return overflowsOwnBox || overflowsViewport
          ? `${testId}: scroll=${html.scrollWidth}/${html.clientWidth}, rect=${Math.round(rect.left)}..${Math.round(rect.right)}`
          : null;
      })
      .filter((value): value is string => value !== null)
      .slice(0, 20);
  }, rootSelector);
}
