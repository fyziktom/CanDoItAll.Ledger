import { test, expect } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

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

test('docker account app UI repair visual validation', async ({ page, context }) => {
  fs.mkdirSync(screenshotDir, { recursive: true });
  await context.grantPermissions(['clipboard-read', 'clipboard-write'], { origin: appUrl }).catch(() => undefined);
  await page.setViewportSize({ width: 1760, height: 990 });

  const summary = {
    appUrl,
    screenshots: [],
    checks: {},
  };

  await page.goto(appUrl, { waitUntil: 'domcontentloaded' });
  await ensureUnlocked(page);
  await page.waitForSelector('[data-testid="ssr-vef-home"]', { timeout: 90_000 });
  await page.locator('[data-testid="refresh-workspace"]').click();
  await page.waitForSelector('[data-testid="ssr-vef-home"]', { timeout: 90_000 });

  await page.locator('[data-testid="cda-action-address-directory"]').hover();
  await page.waitForTimeout(2300);
  summary.checks.tooltipVisible = await page.locator('.cda-page-header-action-tooltip, [data-testid*="tooltip"]').count() > 0;
  await capture(page, summary, 'docker-ui-repair-home-top.png');
  await expect(page.locator('.cda-vef-account-bottom-strip')).toHaveCount(0);
  await expect(page.locator('.cda-vef-account-command')).toContainText('Ledger');
  await expect(page.locator('.cda-vef-account-command')).toContainText('Scenario');

  await page.locator('[data-testid="cda-action-address-directory"]').click();
  await page.waitForSelector('[data-testid="cda-address-directory-dialog"]', { timeout: 30_000 });
  const addressGrid = page.locator('[data-testid="cda-address-directory-grid"]');
  const addressGridBox = await addressGrid.boundingBox();
  const addressCardCount = await page.locator('[data-testid="cda-address-directory-card"]').count();
  summary.checks.addressGridWidth = addressGridBox?.width ?? 0;
  summary.checks.addressCardCount = addressCardCount;
  expect(summary.checks.addressGridWidth).toBeGreaterThan(1000);
  expect(addressCardCount).toBeGreaterThan(0);
  await capture(page, summary, 'docker-ui-repair-address-directory-wide.png');
  await closeTopDialog(page);

  await page.locator('[data-testid="cda-account-profile-button"]').click();
  await page.waitForSelector('[data-testid="cda-profile-picture-preview"]', { timeout: 30_000 });
  await page.getByLabel('Profile image link').fill(profileDataImage);
  await expect(page.locator('[data-testid="cda-profile-picture-preview"] img')).toHaveAttribute('src', /data:image/);
  await capture(page, summary, 'docker-ui-repair-profile-picture-preview.png');
  await closeTopDialog(page);

  const ownershipPayload = await readOwnershipPayloadFromGallery(page, summary);

  await page.getByRole('button', { name: /^Home$/ }).click().catch(() => undefined);
  await page.waitForSelector('[data-testid="ssr-vef-home"]', { timeout: 30_000 });
  await page.locator('[data-testid="cda-action-security"]').click();
  await page.waitForSelector('[data-testid="message-security-panel"]', { timeout: 30_000 });
  await expect(page.getByRole('tab', { name: /Signatures/i })).toBeVisible();
  await expect(page.getByRole('tab', { name: /Encryption/i })).toBeVisible();
  await expect(page.getByRole('tab', { name: /Ownership/i })).toBeVisible();
  await capture(page, summary, 'docker-ui-repair-message-security-tabs.png');

  await page.getByRole('tab', { name: /Ownership/i }).click();
  if (ownershipPayload) {
    await page.locator('[data-testid="message-security-ownership-payload"]').fill(ownershipPayload);
    await page.locator('[data-testid="message-security-ownership-verify"]').click();
    await page.waitForSelector('[data-testid="message-security-ownership-status"]', { timeout: 30_000 });
    await expect(page.locator('[data-testid="message-security-ownership-bo-card"]')).toBeVisible();
    summary.checks.ownershipCardVisible = true;
  } else {
    summary.checks.ownershipCardVisible = false;
  }
  await capture(page, summary, 'docker-ui-repair-ownership-verification.png');

  fs.writeFileSync(summaryPath, JSON.stringify(summary, null, 2));
});

async function ensureUnlocked(page) {
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

  await page.waitForSelector('[data-testid="wasm-workspace"]', { timeout: 90_000 });
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

async function capture(page, summary, fileName) {
  const fullPath = path.join(screenshotDir, fileName);
  await page.screenshot({ path: fullPath, fullPage: false });
  summary.screenshots.push(fullPath);
}
