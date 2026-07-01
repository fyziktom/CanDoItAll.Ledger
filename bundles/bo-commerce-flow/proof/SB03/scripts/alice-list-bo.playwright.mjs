import fs from 'node:fs/promises';
import path from 'node:path';

const objectId = 'bo:post:new-ledger-post';
const encodedObjectId = encodeURIComponent(objectId);
const proofRoot = path.resolve(process.cwd(), 'bundles/bo-commerce-flow/proof/SB03');
const screenshotRoot = path.join(proofRoot, 'browser');
const aliceBackupPath = path.resolve(
  process.cwd(),
  'docker/demo-keys/accountapp-demo/imports/alice-accountapp-backup.json');

export default async function aliceListBusinessObject(page) {
  await fs.mkdir(screenshotRoot, { recursive: true });
  page.setDefaultTimeout(120000);

  await page.goto('http://localhost:5081/');
  await page.locator('[data-testid="account-app-root"]').waitFor();

  await importBackupIfNeeded(page);
  await page.locator('[data-testid="adapter-workspace-host"]').waitFor();
  await page.locator('[data-testid="ssr-bo-gallery"]').waitFor();

  await openBusinessObjectDetails(page);
  await page.locator('[data-testid="cda-bo-details-dialog"]').waitFor();
  await page.getByText('Actions', { exact: true }).click();
  await page.locator('[data-testid="cda-bo-details-dialog"]')
    .getByRole('button', { name: /Set Sale/i })
    .click();

  const dialog = page.locator('[data-testid="cda-bo-set-price-dialog"]');
  await dialog.waitFor();
  await page.locator('[data-testid="bo-sale-mode"]').selectOption('Copy');
  await page.locator('[data-testid="bo-sale-price"]').fill('7.25');
  await page.locator('[data-testid="bo-sale-max-clones"]').fill('5');
  await page.screenshot({
    path: path.join(screenshotRoot, 'alice-set-sale-dialog.png'),
    fullPage: true
  });

  await dialog.getByRole('button', { name: /^Set Sale$/i }).click();
  const listing = await waitForListedSale(page);

  await page.goto('http://localhost:5081/');
  await page.locator('[data-testid="adapter-workspace-host"]').waitFor();
  await page.locator('[data-testid="ssr-bo-gallery"]').waitFor();
  await filterGallery(page);
  await page.locator('[data-testid="cda-bo-sale-badge"]').waitFor();
  await page.screenshot({
    path: path.join(screenshotRoot, 'alice-sale-badge-gallery.png'),
    fullPage: true
  });

  const buyUrl = `http://localhost:5082/buy-bo/${encodedObjectId}`;
  await fs.writeFile(
    path.join(proofRoot, 'transcripts', 'alice-listed-object.json'),
    JSON.stringify({ objectId, buyUrl, listing }, null, 2),
    'utf8');

  return {
    objectId,
    buyUrl,
    saleMode: listing.sale.mode,
    priceAmount: listing.sale.priceAmount,
    maxClones: listing.sale.maxClones,
    soldCount: listing.sale.soldCount,
    screenshots: [
      'alice-set-sale-dialog.png',
      'alice-sale-badge-gallery.png'
    ]
  };
}

async function importBackupIfNeeded(page) {
  if (await page.locator('[data-testid="adapter-workspace-host"]').count() > 0) {
    return;
  }

  const backup = await fs.readFile(aliceBackupPath, 'utf8');
  await page.locator('[data-testid="backup-import-json"]').fill(backup);
  await page.locator('[data-testid="backup-import-password"]').fill('accountapp-demo-password');
  await page.locator('[data-testid="backup-new-password"]').fill('commerce-proof-alice');
  await page.locator('[data-testid="backup-confirm-password"]').fill('commerce-proof-alice');
  await page.locator('[data-testid="import-backup"]').click();
  await page.locator('[data-testid="adapter-workspace-host"]').waitFor();
}

async function openBusinessObjectDetails(page) {
  await filterGallery(page);
  const card = page.locator('.cda-vef-gallery-card')
    .filter({ hasText: 'New ledger post' })
    .first();
  await card.waitFor();
  await card.locator('button[aria-label="Open BO details"]').click();
}

async function filterGallery(page) {
  const search = page.getByPlaceholder('Search BOs...');
  await search.fill('new-ledger-post');
  await page.locator('.cda-vef-gallery-card').filter({ hasText: 'New ledger post' }).first().waitFor();
}

async function waitForListedSale(page) {
  for (let attempt = 0; attempt < 60; attempt += 1) {
    const result = await page.evaluate(async currentObjectId => {
      const response = await fetch('/api/v1/ledger/default/business-objects/query', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ objectId: currentObjectId, skip: 0, take: 1 })
      });
      if (!response.ok) {
        return { error: `${response.status} ${response.statusText}` };
      }

      const holdings = await response.json();
      const holding = holdings[0];
      if (!holding?.stateJson) {
        return null;
      }

      const state = JSON.parse(holding.stateJson);
      return state.commerce?.sale
        ? { version: holding.version, sale: state.commerce.sale, state }
        : null;
    }, objectId);

    if (result?.sale?.status === 'Listed' || result?.sale?.status === 1) {
      return result;
    }

    await page.waitForTimeout(1000);
  }

  throw new Error(`Timed out waiting for ${objectId} sale terms to persist.`);
}
