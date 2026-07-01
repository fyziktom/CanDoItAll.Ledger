import fs from 'node:fs/promises';
import path from 'node:path';

const objectId = 'bo:post:new-ledger-post';
const encodedObjectId = encodeURIComponent(objectId);
const proofRoot = path.resolve(process.cwd(), 'bundles/bo-commerce-flow/proof/SB03');
const screenshotRoot = path.join(proofRoot, 'browser');
const bobBackupPath = path.resolve(
  process.cwd(),
  'docker/demo-keys/accountapp-demo/imports/bob-accountapp-backup.json');

export default async function bobBuyBusinessObject(page) {
  await fs.mkdir(screenshotRoot, { recursive: true });
  page.setDefaultTimeout(120000);

  await page.goto(`http://localhost:5082/buy-bo/${encodedObjectId}`);
  await page.locator('[data-testid="buy-bo-page"]').waitFor();
  await page.locator('[data-testid="buy-bo-listing"]').waitFor();

  await importBackupIfNeeded(page);
  await page.locator('[data-testid="buy-bo-account"]').waitFor();
  await page.waitForFunction(() => {
    const button = document.querySelector('[data-testid="buy-bo-submit"]');
    return button && !button.disabled;
  });

  await page.screenshot({
    path: path.join(screenshotRoot, 'bob-buy-bo-ready.png'),
    fullPage: true
  });

  await page.locator('[data-testid="buy-bo-submit"]').click();
  const result = page.locator('[data-testid="buy-bo-payment-result"]');
  await result.waitFor();
  await result.getByText('Payment submitted').waitFor();
  await page.screenshot({
    path: path.join(screenshotRoot, 'bob-buy-bo-payment-submitted.png'),
    fullPage: true
  });

  const payment = await waitForAlicePaymentRecord(page);
  await fs.writeFile(
    path.join(proofRoot, 'transcripts', 'bob-payment-result.json'),
    JSON.stringify({ objectId, payment }, null, 2),
    'utf8');

  return {
    objectId,
    paymentObjectId: payment.objectId,
    paymentStatus: payment.payment.status,
    priceAmount: payment.payment.priceAmount,
    buyerAddress: payment.payment.buyerAddress,
    sellerAddress: payment.payment.sellerAddress,
    screenshots: [
      'bob-buy-bo-ready.png',
      'bob-buy-bo-payment-submitted.png'
    ]
  };
}

async function importBackupIfNeeded(page) {
  if (await page.locator('[data-testid="buy-bo-submit"]').count() > 0) {
    return;
  }

  const backup = await fs.readFile(bobBackupPath, 'utf8');
  await page.locator('[data-testid="buy-backup-import-json"]').fill(backup);
  await page.locator('[data-testid="buy-backup-import-password"]').fill('accountapp-demo-password');
  await page.locator('[data-testid="buy-backup-new-password"]').fill('commerce-proof-bob');
  await page.locator('[data-testid="buy-backup-confirm-password"]').fill('commerce-proof-bob');
  await page.locator('[data-testid="buy-import-backup-submit"]').click();
  await page.locator('[data-testid="buy-bo-submit"]').waitFor();
}

async function waitForAlicePaymentRecord(page) {
  for (let attempt = 0; attempt < 90; attempt += 1) {
    const result = await page.evaluate(async currentObjectId => {
      const response = await fetch('/api/v1/ledger/default/business-objects/query', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ownerAddress: 'cdia1Ngb3miLzYorvFSfKzLFwYPvZH6Kw6LPsSu',
          objectType: 'payment',
          skip: 0,
          take: 20
        })
      });
      if (!response.ok) {
        return { error: `${response.status} ${response.statusText}` };
      }

      const holdings = await response.json();
      for (const holding of holdings) {
        if (!holding?.stateJson) {
          continue;
        }

        const state = JSON.parse(holding.stateJson);
        const payment = state.commerce?.payment;
        if (payment?.targetObjectId === currentObjectId) {
          return {
            objectId: holding.objectId?.value ?? holding.objectId,
            version: holding.version,
            payment,
            state
          };
        }
      }

      return null;
    }, objectId);

    if (result?.payment) {
      return result;
    }

    await page.waitForTimeout(1000);
  }

  throw new Error(`Timed out waiting for Alice payment record for ${objectId}.`);
}
