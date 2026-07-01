import fs from 'node:fs/promises';
import path from 'node:path';

const objectId = 'bo:post:new-ledger-post';
const proofRoot = path.resolve(process.cwd(), 'bundles/bo-commerce-flow/proof/SB03');
const screenshotRoot = path.join(proofRoot, 'browser');

export default async function aliceCommerceDashboard(page) {
  await fs.mkdir(screenshotRoot, { recursive: true });
  page.setDefaultTimeout(120000);

  await page.goto('http://localhost:5081/');
  await page.locator('[data-testid="adapter-workspace-host"]').waitFor();
  await page.locator('[data-testid="refresh-workspace"]').click();
  await page.locator('[data-testid="cda-commerce-dashboard"]').waitFor();

  const dashboard = page.locator('[data-testid="cda-commerce-dashboard"]');
  await dashboard.scrollIntoViewIfNeeded();
  const row = dashboard.locator('[data-testid="cda-commerce-record"]')
    .filter({ hasText: objectId })
    .first();
  await row.waitFor();
  await page.screenshot({
    path: path.join(screenshotRoot, 'alice-commerce-dashboard-payment.png'),
    fullPage: true
  });

  await row.click();
  await page.locator('[data-testid="cda-commerce-details-dialog"]').waitFor();
  await page.screenshot({
    path: path.join(screenshotRoot, 'alice-commerce-payment-detail.png'),
    fullPage: true
  });

  const rowText = await row.textContent();
  await fs.writeFile(
    path.join(proofRoot, 'transcripts', 'alice-commerce-dashboard.txt'),
    rowText ?? '',
    'utf8');

  return {
    objectId,
    rowText,
    screenshots: [
      'alice-commerce-dashboard-payment.png',
      'alice-commerce-payment-detail.png'
    ]
  };
}
