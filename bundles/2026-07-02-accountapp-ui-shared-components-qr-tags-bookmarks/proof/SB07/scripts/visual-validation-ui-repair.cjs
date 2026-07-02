const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');

const root = process.cwd();
const proofRoot = path.join(root, 'bundles/2026-07-02-accountapp-ui-shared-components-qr-tags-bookmarks/proof/SB07');
const screenshotDir = path.join(proofRoot, 'screenshots');
const summaryPath = path.join(proofRoot, 'ui-repair-visual-validation.json');
const backupPath = path.join(root, 'docker/demo-keys/accountapp-demo/imports/alice-accountapp-backup.json');
const password = 'accountapp-demo-password';
const appUrl = 'http://localhost:5081/';
const profileDataImage =
  'data:image/svg+xml;utf8,' +
  encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128"><rect width="128" height="128" fill="#0f766e"/><circle cx="64" cy="44" r="24" fill="#f7f9fc"/><path d="M24 118c7-28 23-42 40-42s33 14 40 42" fill="#f7f9fc"/></svg>');

async function main() {
  fs.mkdirSync(screenshotDir, { recursive: true });

  const summary = {
    appUrl,
    viewport: { width: 1760, height: 990 },
    screenshots: [],
    checks: {},
    consoleErrors: [],
    pageErrors: [],
  };

  const browser = await chromium.launch({ headless: true });
  try {
    const context = await browser.newContext({
      viewport: summary.viewport,
      ignoreHTTPSErrors: true,
    });

    await context.grantPermissions(['clipboard-read', 'clipboard-write'], { origin: appUrl }).catch(() => undefined);
    const page = await context.newPage();
    page.on('console', (message) => {
      if (message.type() === 'error') {
        summary.consoleErrors.push(message.text());
      }
    });
    page.on('pageerror', (error) => summary.pageErrors.push(String(error?.message ?? error)));

    await page.goto(appUrl, { waitUntil: 'domcontentloaded' });
    await ensureUnlocked(page);
    await page.waitForSelector('[data-testid="ssr-vef-home"]', { timeout: 90_000 });
    await clickIfPresent(page, '[data-testid="refresh-workspace"]');
    await page.waitForSelector('[data-testid="ssr-vef-home"]', { timeout: 90_000 });

    await page.locator('[data-testid="cda-action-address-directory"]').hover();
    await page.waitForTimeout(2300);
    summary.checks.tooltipVisible = await page.locator('.cda-page-header-action-tooltip, [data-testid*="tooltip"]').count() > 0;
    assert.equal(summary.checks.tooltipVisible, true, 'TooltipService-backed action tooltip should be visible after hover.');
    await capture(page, summary, 'docker-ui-repair-home-top.png');

    assert.equal(await page.locator('.cda-vef-account-bottom-strip').count(), 0, 'Bottom account info strip should be removed.');
    await assertContains(page.locator('.cda-vef-account-command'), 'Ledger');
    await assertContains(page.locator('.cda-vef-account-command'), 'Scenario');

    await page.locator('[data-testid="cda-action-address-directory"]').click();
    await page.waitForSelector('[data-testid="cda-address-directory-dialog"]', { timeout: 30_000 });
    const addressGrid = page.locator('[data-testid="cda-address-directory-grid"]');
    const addressGridBox = await addressGrid.boundingBox();
    const addressCardCount = await page.locator('[data-testid="cda-address-directory-card"]').count();
    summary.checks.addressGridWidth = addressGridBox?.width ?? 0;
    summary.checks.addressCardCount = addressCardCount;
    assert(summary.checks.addressGridWidth > 1000, `Address grid should use large-screen width, got ${summary.checks.addressGridWidth}.`);
    assert(addressCardCount > 0, 'Address directory should show address cards.');
    await capture(page, summary, 'docker-ui-repair-address-directory-wide.png');
    await closeTopDialog(page);

    await page.locator('[data-testid="cda-account-profile-button"]').click();
    await page.waitForSelector('[data-testid="cda-profile-picture-preview"]', { timeout: 30_000 });
    await page.getByLabel('Profile image link').fill(profileDataImage);
    const previewSrc = await page.locator('[data-testid="cda-profile-picture-preview"] img').getAttribute('src');
    summary.checks.profilePreviewSrc = previewSrc;
    assert(previewSrc?.startsWith('data:image'), 'Profile setup should load and preview the picture link.');
    await capture(page, summary, 'docker-ui-repair-profile-picture-preview.png');
    await closeTopDialog(page);

    const ownershipPayload = await readOwnershipPayloadFromGallery(page, summary);

    await page.getByRole('button', { name: /^Home$/ }).click().catch(() => undefined);
    await page.waitForSelector('[data-testid="ssr-vef-home"]', { timeout: 30_000 });
    await page.locator('[data-testid="cda-action-security"]').click();
    await page.waitForSelector('[data-testid="message-security-panel"]', { timeout: 30_000 });
    await assertVisible(page.getByRole('tab', { name: /Signatures/i }), 'Signatures tab should be visible.');
    await assertVisible(page.getByRole('tab', { name: /Encryption/i }), 'Encryption tab should be visible.');
    await assertVisible(page.getByRole('tab', { name: /Ownership/i }), 'Ownership tab should be visible.');
    await capture(page, summary, 'docker-ui-repair-message-security-tabs.png');

    await page.getByRole('tab', { name: /Ownership/i }).click();
    assert(ownershipPayload, 'Gallery details should provide an ownership verification payload.');
    assert.equal(summary.checks.ownershipPayloadLoaded, true, 'Ownership payload should use the BO ownership payload schema.');
    const ownershipPayloadInput = page.locator('[data-testid="message-security-ownership-payload"]');
    await ownershipPayloadInput.fill(ownershipPayload);
    await ownershipPayloadInput.dispatchEvent('change');
    await page.waitForFunction(() => {
      const button = document.querySelector('[data-testid="message-security-ownership-verify"]');
      return button && !button.disabled;
    }, null, { timeout: 10_000 });
    await page.locator('[data-testid="message-security-ownership-verify"]').click();
    await page.waitForSelector('[data-testid="message-security-ownership-status"]', { timeout: 30_000 });
    const ownershipCard = page.locator('[data-testid="message-security-ownership-bo-card"]');
    await assertVisible(ownershipCard, 'Ownership verification should show a gallery-style BO card.');
    summary.checks.ownershipCardVisible = true;
    summary.checks.ownershipStatus = await page.locator('[data-testid="message-security-ownership-status"]').innerText();
    await ownershipCard.scrollIntoViewIfNeeded();
    await capture(page, summary, 'docker-ui-repair-ownership-verification.png');

    await ownershipCard.locator('button[aria-label="Open BO details"]').click();
    await page.waitForSelector('[data-testid="cda-bo-details-layout"]', { timeout: 30_000 });
    summary.checks.ownershipDetailsDialogVisible = true;
    await capture(page, summary, 'docker-ui-repair-ownership-details-dialog.png');
    await closeTopDialog(page);

    fs.writeFileSync(summaryPath, JSON.stringify(summary, null, 2));
  } finally {
    await browser.close();
  }
}

async function ensureUnlocked(page) {
  await page.waitForSelector('[data-testid="ssr-vef-home"], [data-testid="first-run-account-flow"], [data-testid="unlock-password"]', { timeout: 90_000 });

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

  await page.waitForSelector('[data-testid="wasm-workspace"], [data-testid="ssr-vef-home"]', { timeout: 90_000 });
}

async function readOwnershipPayloadFromGallery(page, summary) {
  try {
    await page.getByRole('button', { name: /^Gallery$/ }).click();
    await page.waitForSelector('.cda-vef-gallery-card', { timeout: 45_000 });
    await page.locator('.cda-vef-gallery-card .cda-vef-info-button').first().click();
    await page.getByRole('tab', { name: /Actions/i }).click();
    await page.getByRole('button', { name: /Verify ownership/i }).click();
    await page.waitForSelector('textarea[aria-label="BusinessObject verification payload"]', { timeout: 30_000 });
    const payload = await page.locator('textarea[aria-label="BusinessObject verification payload"]').inputValue();
    summary.checks.ownershipPayloadLoaded = payload.includes('candoitall-bo-ownership-v1');
    await closeTopDialog(page);
    await closeTopDialog(page);
    return payload;
  } catch (error) {
    summary.checks.ownershipPayloadError = String(error?.message ?? error);
    await page.keyboard.press('Escape').catch(() => undefined);
    await page.keyboard.press('Escape').catch(() => undefined);
    return '';
  }
}

async function closeTopDialog(page) {
  const closeButtons = page.getByRole('button', { name: /^Close$/ });
  const count = await closeButtons.count();
  if (count > 0) {
    await closeButtons.nth(count - 1).click();
    await page.waitForTimeout(400);
    return;
  }

  await page.keyboard.press('Escape');
  await page.waitForTimeout(400);
}

async function clickIfPresent(page, selector) {
  const locator = page.locator(selector);
  if (await locator.count()) {
    await locator.first().click();
  }
}

async function assertContains(locator, expected) {
  await locator.first().waitFor({ timeout: 30_000 });
  const text = await locator.first().innerText();
  assert(text.toLocaleLowerCase().includes(expected.toLocaleLowerCase()), `Expected "${expected}" in "${text}".`);
}

async function assertVisible(locator, message) {
  await locator.first().waitFor({ state: 'visible', timeout: 30_000 });
  assert.equal(await locator.first().isVisible(), true, message);
}

async function capture(page, summary, fileName) {
  const fullPath = path.join(screenshotDir, fileName);
  await page.screenshot({ path: fullPath, fullPage: false });
  summary.screenshots.push(fullPath);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
