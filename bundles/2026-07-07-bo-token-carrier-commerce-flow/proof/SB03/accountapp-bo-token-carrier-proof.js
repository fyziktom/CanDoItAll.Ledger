const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { chromium } = require('playwright');

const proofDir = path.resolve('bundles/2026-07-07-bo-token-carrier-commerce-flow/proof/SB03');
const screenshotDir = path.join(proofDir, 'screenshots');
const aliceUrl = 'http://localhost:5081/';
const bobUrl = 'http://localhost:5082/';
const sellerAddress = 'cdia1Ngb3miLzYorvFSfKzLFwYPvZH6Kw6LPsSu';
const buyerAddress = 'cdia1NTXHdDX3kHtpHwtEFyCFuHPNkqBiV8k4uU';
const tokenId = 'bo.posts';
const alicePassword = 'carrier-proof-alice';
const bobPassword = 'carrier-proof-bob';
const salePrice = '7.25';
const proofRunId = Date.now().toString();
const targetName = `Carrier proof post ${proofRunId}`;

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});

async function main() {
  await fs.mkdir(screenshotDir, { recursive: true });
  const aliceBackup = await fs.readFile(
    path.resolve('docker/demo-keys/accountapp-demo/imports/alice-accountapp-backup.json'),
    'utf8');
  const bobBackup = await fs.readFile(
    path.resolve('docker/demo-keys/accountapp-demo/imports/bob-accountapp-backup.json'),
    'utf8');

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1600, height: 950 } });
  const alice = await context.newPage();
  const bob = await context.newPage();
  alice.setDefaultTimeout(300000);
  bob.setDefaultTimeout(300000);

  try {
    await ensureAliceHome(alice, aliceBackup);
    const commerceTabOrder = await assertCommerceTabPlacement(alice);

    const created = await createAliceBusinessObject(alice);
    const targetObjectId = created.objectId;
    const createToken = await assertCarrierOutput(alice, created, 'created BO');
    await captureGalleryObjectTransaction(alice, targetName, 'alice-created-bo-token-detail.png');

    await ensureCopySale(alice, targetObjectId, targetName);

    const existingPaymentIds = new Set(await listPaymentIds(alice));
    await submitBobPayment(bob, bobBackup, targetObjectId);
    const payment = await waitForNewPayment(alice, existingPaymentIds, targetObjectId);
    const paymentToken = await assertCarrierOutput(alice, payment, 'payment', { expectedMainAmount: '0' });
    const paymentMainOutput = await assertPlainMainPaymentOutput(alice, payment, 'payment', sellerAddress, salePrice);
    await captureTransactionFromHistory(bob, paymentToken.txId, 'bob-payment-create-separate-output-transaction.png', ensureBobWorkspace);

    await waitForDispatch(alice, payment.objectId);
    const dispatchedPayment = await loadHolding(alice, payment.objectId);
    const paymentRecord = dispatchedPayment.commerce.payment;
    const receipt = await loadHolding(alice, paymentRecord.receiptObjectId);
    const purchase = await loadHolding(bob, paymentRecord.purchaseObjectId);
    const delivered = await loadHolding(bob, paymentRecord.deliveredObjectId);

    const receiptToken = await assertCarrierOutput(alice, receipt, 'receipt');
    const purchaseToken = await assertCarrierOutput(bob, purchase, 'purchase');
    const deliveredToken = await assertCarrierOutput(bob, delivered, 'delivered clone');

    await captureAliceDashboard(alice, payment.objectId, receipt.objectId, targetObjectId);
    await captureBobPurchase(bob, purchase.objectId, targetObjectId);
    const receiptRecycle = await recycleReceiptAndAssertMerge(alice, receipt.objectId);

    const result = {
      status: 'passed',
      commerceTabOrder,
      targetName,
      targetObjectId,
      paymentObjectId: payment.objectId,
      receiptObjectId: receipt.objectId,
      purchaseObjectId: purchase.objectId,
      deliveredObjectId: delivered.objectId,
      carrierToken: tokenId,
      outputs: {
        created: createToken,
        payment: paymentToken,
        paymentMainOutput,
        receipt: receiptToken,
        purchase: purchaseToken,
        delivered: deliveredToken
      },
      receiptRecycle,
      screenshots: [
        'alice-created-bo-token-detail.png',
        'bob-payment-submitted.png',
        'bob-payment-create-separate-output-transaction.png',
        'alice-dispatch-dashboard.png',
        'alice-payment-token-detail.png',
        'alice-receipt-token-detail.png',
        'alice-payment-related-bo-detail.png',
        'alice-receipt-related-bo-detail.png',
        'bob-purchase-token-detail.png',
        'alice-receipt-recycle-merge-transaction.png'
      ]
    };
    await fs.writeFile(path.join(proofDir, 'accountapp-bo-token-carrier-proof.json'), JSON.stringify(result, null, 2));
    console.log(JSON.stringify(result, null, 2));
  } finally {
    await browser.close();
  }
}

async function ensureAliceHome(page, backupJson) {
  await page.goto(aliceUrl, { waitUntil: 'domcontentloaded' });
  await page.getByTestId('account-app-root').waitFor();
  if (await page.getByTestId('adapter-workspace-host').count() > 0) {
    return;
  }

  if (await page.getByTestId('backup-import-json').count() === 0
    && await page.getByTestId('unlock-account').count() > 0) {
    await page.getByTestId('unlock-password').fill(alicePassword);
    await page.waitForFunction(() => {
      const button = document.querySelector('[data-testid="unlock-account"]');
      return button && !button.disabled;
    });
    await page.getByTestId('unlock-account').click();
    await page.getByTestId('adapter-workspace-host').waitFor();
    return;
  }

  await page.getByTestId('backup-import-json').fill(backupJson);
  await page.getByTestId('backup-import-password').fill('accountapp-demo-password');
  await page.getByTestId('backup-new-password').fill(alicePassword);
  await page.getByTestId('backup-confirm-password').fill(alicePassword);
  await page.getByTestId('import-backup').click();
  await waitForWorkspaceOrUnlock(page, 'unlock-account');
  if (await page.getByTestId('adapter-workspace-host').count() === 0) {
    await page.getByTestId('unlock-password').fill(alicePassword);
    await page.getByTestId('unlock-account').click();
  }
  await page.getByTestId('adapter-workspace-host').waitFor();
}

async function createAliceBusinessObject(page) {
  await page.getByTestId('adapter-workspace-host').waitFor();
  await page.getByRole('button', { name: /^New Post$/i }).click();
  await page.getByTestId('bo-create-wizard').waitFor();

  await clickButton(page, /^Next$/i);
  await page.getByLabel('BusinessObject name').fill(targetName);
  await page.getByLabel('BusinessObject description').fill('Token-backed BO creation proof from docker AccountApp.');
  await page.getByLabel('BusinessObject text').fill('This BO was created by the browser AccountApp proof flow and must carry one bo.posts token.');
  await clickButton(page, /^Next$/i);
  await page.getByTestId('bo-create-token-selector').getByRole('radio', { name: new RegExp(tokenId) }).waitFor();
  await clickButton(page, /^Stage BO Transaction$/i);

  return await waitFor(async () => {
    await page.waitForTimeout(1000);
    return await findBusinessObjectByName(page, sellerAddress, 'post', targetName);
  }, `created BO ${targetName}`, 120);
}

async function ensureCopySale(page, targetObjectId, targetDisplayName) {
  const current = await loadHolding(page, targetObjectId);
  const sale = current?.commerce?.sale;
  if ((sale?.status === 'Listed' || sale?.status === 1)
    && (sale?.mode === 'Copy' || sale?.mode === 1)
    && Number(sale?.maxClones ?? 0) > Number(sale?.soldCount ?? 0)) {
    return;
  }

  await openGallery(page);
  await page.getByPlaceholder('Search BOs...').fill(targetDisplayName);
  const card = page.locator('.cda-vef-gallery-card').filter({ hasText: targetDisplayName }).first();
  await card.waitFor();
  await card.locator('button[aria-label="Open BO details"]').click();
  const details = page.getByTestId('cda-bo-details-dialog');
  await details.waitFor();
  await clickInDialog(details, /Actions/i);
  await details.getByRole('button', { name: /Set Sale/i }).click();
  const dialog = page.getByTestId('cda-bo-set-price-dialog');
  await dialog.waitFor();
  await page.getByTestId('bo-sale-mode').selectOption('Copy');
  await page.getByTestId('bo-sale-price').fill(salePrice);
  await page.getByTestId('bo-sale-max-clones').fill('50');
  await dialog.getByRole('button', { name: /^Set Sale$/i }).click();

  await waitFor(async () => {
    const updated = await loadHolding(page, targetObjectId);
    const nextSale = updated?.commerce?.sale;
    return (nextSale?.status === 'Listed' || nextSale?.status === 1)
      && (nextSale?.mode === 'Copy' || nextSale?.mode === 1);
  }, 'copy sale terms');

  await closeDialogIfPresent(page, 'cda-bo-details-dialog');
}

async function submitBobPayment(page, backupJson, targetObjectId) {
  await page.goto(`${bobUrl}buy-bo/${encodeURIComponent(targetObjectId)}`, { waitUntil: 'domcontentloaded' });
  await page.getByTestId('buy-bo-page').waitFor();
  await page.getByTestId('buy-bo-listing').waitFor();
  if (await page.getByTestId('buy-bo-submit').count() === 0) {
    if (await page.getByTestId('buy-backup-import-json').count() === 0
      && await page.getByTestId('buy-unlock-account').count() > 0) {
      await page.getByTestId('buy-unlock-password').fill(bobPassword);
      await page.waitForFunction(() => {
        const button = document.querySelector('[data-testid="buy-unlock-account"]');
        return button && !button.disabled;
      });
      await page.getByTestId('buy-unlock-account').click();
    } else {
      await page.getByTestId('buy-backup-import-json').fill(backupJson);
      await page.getByTestId('buy-backup-import-password').fill('accountapp-demo-password');
      await page.getByTestId('buy-backup-new-password').fill(bobPassword);
      await page.getByTestId('buy-backup-confirm-password').fill(bobPassword);
      await page.getByTestId('buy-import-backup-submit').click();
    }
    await page.getByTestId('buy-bo-submit').waitFor();
  }

  await page.waitForFunction(() => {
    const button = document.querySelector('[data-testid="buy-bo-submit"]');
    return button && !button.disabled;
  });
  await page.getByTestId('buy-bo-submit').click();
  const outcome = await waitForPurchaseOutcome(page);
  if (outcome.kind !== 'result' || !outcome.text.includes('Payment submitted')) {
    await page.screenshot({ path: path.join(screenshotDir, 'bob-payment-failure.png'), fullPage: true });
    throw new Error(`Bob payment did not submit: ${outcome.text}`);
  }
  await page.screenshot({ path: path.join(screenshotDir, 'bob-payment-submitted.png'), fullPage: true });
}

async function waitForPurchaseOutcome(page) {
  return await waitFor(async () => {
    const error = page.getByTestId('buy-bo-error');
    if (await error.count() > 0) {
      return { kind: 'error', text: normalizeText(await error.textContent()) };
    }

    const result = page.getByTestId('buy-bo-payment-result');
    if (await result.count() > 0) {
      return { kind: 'result', text: normalizeText(await result.textContent()) };
    }

    return null;
  }, 'Bob purchase result', 300);
}

async function waitForNewPayment(page, existingPaymentIds, targetObjectId) {
  for (let attempt = 0; attempt < 120; attempt += 1) {
    const payments = await listPayments(page);
    const newest = payments
      .filter(payment => payment.commerce.payment.targetObjectId === targetObjectId)
      .filter(payment => !existingPaymentIds.has(payment.objectId))
      .sort((a, b) => String(b.commerce.payment.createdAtUtc).localeCompare(String(a.commerce.payment.createdAtUtc)))[0];
    if (newest) {
      return newest;
    }
    await page.waitForTimeout(1000);
  }
  throw new Error('Timed out waiting for a fresh payment BO.');
}

async function waitForDispatch(page, paymentObjectId) {
  await ensureAliceWorkspace(page);
  await page.getByTestId('refresh-workspace').click();

  for (let attempt = 0; attempt < 180; attempt += 1) {
    const payment = await loadHolding(page, paymentObjectId);
    const record = payment?.commerce?.payment;
    if ((record?.status === 'Dispatched' || record?.status === 1)
      && record?.receiptObjectId
      && record?.purchaseObjectId
      && record?.deliveredObjectId) {
      return payment;
    }
    if (attempt % 10 === 0) {
      await page.getByTestId('refresh-workspace').click().catch(() => {});
    }
    await page.waitForTimeout(1000);
  }
  throw new Error(`Timed out waiting for dispatch of ${paymentObjectId}.`);
}

async function captureGalleryObjectTransaction(page, targetDisplayName, screenshotName) {
  await openGallery(page);
  await page.getByPlaceholder('Search BOs...').fill(targetDisplayName);
  const card = page.locator('.cda-vef-gallery-card').filter({ hasText: targetDisplayName }).first();
  await card.waitFor();
  await card.locator('button[aria-label="Open BO details"]').click();
  const details = page.getByTestId('cda-bo-details-dialog');
  await details.waitFor();
  await captureOpenTransactionFromDetails(page, details, screenshotName);
  await closeDialogIfPresent(page, 'cda-bo-details-dialog');
}

async function captureAliceDashboard(page, paymentObjectId, receiptObjectId, targetObjectId) {
  await ensureAliceWorkspace(page);
  await page.getByTestId('refresh-workspace').click();
  await openAccountHomeTab(page, /Commerce/i);
  const dashboard = page.getByTestId('cda-commerce-dashboard');
  await dashboard.waitFor();
  await dashboard.scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(screenshotDir, 'alice-dispatch-dashboard.png'), fullPage: true });

  const paymentRow = await findCommerceRow(dashboard, 'payment', [paymentObjectId, targetObjectId]);
  await openCommerceTransactionDetail(page, paymentRow, 'alice-payment-token-detail.png');
  await openCommerceRelatedBusinessObjectDetail(page, paymentRow, 'alice-payment-related-bo-detail.png');

  const receiptRow = await findCommerceRow(dashboard, 'receipt', [receiptObjectId, targetObjectId]);
  await openCommerceTransactionDetail(page, receiptRow, 'alice-receipt-token-detail.png');
  await openCommerceRelatedBusinessObjectDetail(page, receiptRow, 'alice-receipt-related-bo-detail.png');
}

async function captureBobPurchase(page, purchaseObjectId, targetObjectId) {
  await page.goto(bobUrl, { waitUntil: 'domcontentloaded' });
  await page.getByTestId('account-app-root').waitFor();
  if (await page.getByTestId('adapter-workspace-host').count() === 0) {
    await page.getByTestId('unlock-password').fill(bobPassword);
    await page.getByTestId('unlock-account').click();
    await page.getByTestId('adapter-workspace-host').waitFor();
  }
  await page.getByTestId('refresh-workspace').click();
  await openAccountHomeTab(page, /Commerce/i);
  const dashboard = page.getByTestId('cda-commerce-dashboard');
  await dashboard.waitFor();
  const purchaseRow = await findCommerceRow(dashboard, 'receipt', [purchaseObjectId, targetObjectId]);
  await openCommerceTransactionDetail(page, purchaseRow, 'bob-purchase-token-detail.png');
}

async function openCommerceTransactionDetail(page, row, screenshotName) {
  await row.waitFor();
  await row.scrollIntoViewIfNeeded();
  await row.getByTestId('cda-commerce-record-details').click();
  const details = page.getByTestId('cda-commerce-details-dialog');
  await details.waitFor();
  await captureOpenTransactionFromDetails(page, details, screenshotName);
  await closeDialogIfPresent(page, 'cda-commerce-details-dialog');
}

async function openCommerceRelatedBusinessObjectDetail(page, row, screenshotName) {
  await row.waitFor();
  await row.scrollIntoViewIfNeeded();
  const button = row.getByTestId('cda-commerce-related-details');
  await button.waitFor();
  const disabled = await button.evaluate(element => element.disabled);
  if (disabled) {
    throw new Error('Related BO details button is disabled for a visible commerce record.');
  }
  await button.click();
  const details = page.getByTestId('cda-commerce-details-dialog');
  await details.waitFor();
  await details.scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(screenshotDir, screenshotName), fullPage: true });
  await closeDialogIfPresent(page, 'cda-commerce-details-dialog');
}

async function recycleReceiptAndAssertMerge(page, receiptObjectId) {
  await ensureAliceWorkspace(page);
  await page.getByTestId('refresh-workspace').click();

  const receipt = await loadHolding(page, receiptObjectId);
  const receiptOutPoint = receipt.currentOutPoint;
  const beforeSources = await listTokenSources(page, sellerAddress);
  const mergeCandidate = selectTokenMergeCandidate(beforeSources);
  assert.ok(mergeCandidate, 'Alice has no token source UTXO to merge the receipt into.');
  const expectedMergedAmount = Number(amountValue(mergeCandidate.token.amount)) + 1;

  await openAccountHomeTab(page, /Commerce/i);
  const dashboard = page.getByTestId('cda-commerce-dashboard');
  await dashboard.waitFor();
  const receiptRow = await findCommerceRow(dashboard, 'receipt', [receiptObjectId]);
  await receiptRow.getByTestId('cda-commerce-record-details').click();
  const details = page.getByTestId('cda-commerce-details-dialog');
  await details.waitFor();
  await clickInDialog(details, /Actions/i);
  const recycleButton = details.getByTestId('cda-bo-recycle-button');
  await recycleButton.waitFor();
  const disabled = await recycleButton.evaluate(button => button.disabled);
  if (disabled) {
    throw new Error('Receipt recycle button is disabled.');
  }
  await recycleButton.click();
  await details.waitFor({ state: 'detached' }).catch(() => {});

  const merged = await waitFor(async () => {
    await page.getByTestId('refresh-workspace').click().catch(() => {});
    const receiptStillExists = await tryLoadHolding(page, receiptObjectId);
    if (receiptStillExists) {
      return null;
    }

    const sources = await listTokenSources(page, sellerAddress);
    return sources.find(source => Number(amountValue(source.token.amount)) === expectedMergedAmount) ?? null;
  }, `receipt ${receiptObjectId} recycle merge`, 120);

  const recycleTxId = unwrap(merged.outPoint.txId);
  const tx = await fetchJson(page, `/api/v1/ledger/default/transactions/${encodeURIComponent(recycleTxId)}?includeData=true`);
  const inputs = tx.inputs ?? tx.vin ?? tx.transaction?.inputs ?? [];
  const outputs = tx.outputs ?? tx.vout ?? tx.transaction?.outputs ?? [];
  assert.equal(inputs.length, 2, 'receipt recycle merge input count');
  const output = assertSingle(outputs, 'receipt recycle merge output');
  assert.equal(unwrap(output.token?.tokenId ?? output.tokenTransfer?.tokenId), tokenId, 'receipt recycle output token id');
  assert.equal(Number(amountValue(output.token?.amount ?? output.tokenTransfer?.amount)), expectedMergedAmount, 'receipt recycle output token amount');
  assert.equal(output.businessObject ?? output.businessObjectRef ?? null, null, 'receipt recycle output must not carry a BO');

  await captureTransactionFromHistory(page, recycleTxId, 'alice-receipt-recycle-merge-transaction.png');

  return {
    receiptObjectId,
    receiptOutPoint,
    mergeSourceOutPoint: mergeCandidate.outPoint,
    mergeSourceAmount: amountValue(mergeCandidate.token.amount),
    recycleTxId,
    mergedSourceOutPoint: merged.outPoint,
    mergedSourceAmount: amountValue(merged.token.amount)
  };
}

async function captureOpenTransactionFromDetails(page, details, screenshotName) {
  await clickInDialog(details, /Ledger details/i);
  const openTransaction = details.getByRole('button', { name: /Open transaction/i }).first();
  await openTransaction.waitFor();
  const disabled = await openTransaction.evaluate(button => button.disabled);
  if (disabled) {
    throw new Error('Open transaction button is disabled in BO details dialog.');
  }
  await openTransaction.click();

  const txDialog = page.getByTestId('cda-transaction-details-dialog');
  await txDialog.waitFor();
  await clickInDialog(txDialog, /Inputs & outputs/i);
  await page.screenshot({ path: path.join(screenshotDir, screenshotName), fullPage: true });
  await closeDialogIfPresent(page, 'cda-transaction-details-dialog');
}

async function assertCarrierOutput(page, holding, label, options = {}) {
  const txId = holding.currentOutPoint.txId.value ?? holding.currentOutPoint.txId;
  const outputIndex = Number(holding.currentOutPoint.outputIndex);
  const tx = await fetchJson(page, `/api/v1/ledger/default/transactions/${encodeURIComponent(txId)}?includeData=true`);
  const outputs = tx.outputs ?? tx.vout ?? tx.transaction?.outputs ?? tx.value?.outputs ?? [];
  const output = outputs.find(item => Number(item.outputIndex ?? item.index ?? item.n) === outputIndex);
  assert.ok(output, `${label} output ${outputIndex} not found in tx ${txId}`);
  const token = output.token ?? output.tokenTransfer;
  assert.ok(token, `${label} output has no token in tx ${txId}`);
  const actualTokenId = unwrap(token.tokenId ?? token.id);
  const actualAmount = amountValue(token.amount);
  const mainAmount = amountValue(output.mainAmount ?? output.amount);
  assert.equal(actualTokenId, tokenId, `${label} token id`);
  assert.equal(actualAmount, '1', `${label} token amount`);
  if (options.expectedMainAmount) {
    assert.equal(mainAmount, options.expectedMainAmount, `${label} main amount`);
  }
  return {
    txId,
    outputIndex,
    tokenId: actualTokenId,
    tokenAmount: actualAmount,
    mainAmount
  };
}

async function assertPlainMainPaymentOutput(page, holding, label, expectedAddress, expectedMainAmount) {
  const txId = holding.currentOutPoint.txId.value ?? holding.currentOutPoint.txId;
  const carrierOutputIndex = Number(holding.currentOutPoint.outputIndex);
  const tx = await fetchJson(page, `/api/v1/ledger/default/transactions/${encodeURIComponent(txId)}?includeData=true`);
  const outputs = tx.outputs ?? tx.vout ?? tx.transaction?.outputs ?? tx.value?.outputs ?? [];
  const output = outputs.find(item =>
    Number(item.outputIndex ?? item.index ?? item.n) !== carrierOutputIndex
    && unwrap(item.address ?? item.recipient ?? item.receiverAddress) === expectedAddress
    && amountValue(item.mainAmount ?? item.amount) === expectedMainAmount
    && !(item.token ?? item.tokenTransfer)
    && !(item.businessObject ?? item.businessObjectRef));
  assert.ok(output, `${label} plain main output not found in tx ${txId}`);

  return {
    txId,
    outputIndex: Number(output.outputIndex ?? output.index ?? output.n),
    address: unwrap(output.address ?? output.recipient ?? output.receiverAddress),
    mainAmount: amountValue(output.mainAmount ?? output.amount)
  };
}

async function listPaymentIds(page) {
  return (await listPayments(page)).map(payment => payment.objectId);
}

async function listPayments(page) {
  const rows = await queryBusinessObjects(page, {
    ownerAddress: sellerAddress,
    objectType: 'payment',
    skip: 0,
    take: 100
  });
  return rows
    .map(toHolding)
    .filter(holding => holding?.commerce?.payment);
}

async function loadHolding(page, objectId) {
  const rows = await queryBusinessObjects(page, { objectId, skip: 0, take: 1 });
  const holding = toHolding(rows[0]);
  if (!holding) {
    throw new Error(`BusinessObject ${objectId} not found.`);
  }
  return holding;
}

async function tryLoadHolding(page, objectId) {
  const rows = await queryBusinessObjects(page, { objectId, skip: 0, take: 1 });
  return toHolding(rows[0]);
}

async function listTokenSources(page, address) {
  const outputs = await fetchJson(page, `/api/v1/ledger/default/accounts/${encodeURIComponent(address)}/spendable-outputs`);
  return outputs.filter(output =>
    !output.businessObject
    && !output.businessObjectRef
    && output.token
    && unwrap(output.token.tokenId) === tokenId
    && Number(amountValue(output.token.amount)) > 1);
}

async function findBusinessObjectByName(page, ownerAddress, objectType, name) {
  const rows = await queryBusinessObjects(page, {
    ownerAddress,
    objectType,
    skip: 0,
    take: 100
  });
  return rows
    .map(toHolding)
    .find(holding => holding?.name === name) ?? null;
}

async function queryBusinessObjects(page, request) {
  return await fetchJson(page, '/api/v1/ledger/default/business-objects/query', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request)
  });
}

function toHolding(row) {
  if (!row?.stateJson) {
    return null;
  }
  const state = JSON.parse(row.stateJson);
  return {
    objectId: row.objectId?.value ?? row.objectId,
    ownerAddress: row.ownerAddress?.value ?? row.ownerAddress,
    version: row.version,
    currentOutPoint: row.currentOutPoint,
    name: state.name ?? state.Name ?? state.payload?.name ?? null,
    commerce: state.commerce ?? null
  };
}

async function fetchJson(page, url, options) {
  return await page.evaluate(async args => {
    const response = await fetch(args.url, args.options);
    if (!response.ok) {
      throw new Error(`${response.status} ${response.statusText} for ${args.url}`);
    }
    return await response.json();
  }, { url, options });
}

async function openGallery(page) {
  const galleryButton = page.getByRole('button', { name: /^Gallery$/i }).first();
  await galleryButton.waitFor();
  await galleryButton.click();
  await page.getByTestId('ssr-bo-gallery').waitFor();
}

async function assertCommerceTabPlacement(page) {
  await ensureAliceWorkspace(page);
  const tabs = await page.getByTestId('cda-account-home-tabs')
    .getByRole('tab')
    .evaluateAll(elements => elements.map(element => String(element.textContent ?? '').replace(/\s+/g, ' ').trim()));
  const balancesIndex = tabs.findIndex(text => text.includes('Balances'));
  const commerceIndex = tabs.findIndex(text => text.includes('Commerce'));
  const transactionsIndex = tabs.findIndex(text => text.includes('Transactions history'));
  assert.ok(balancesIndex >= 0, `Balances tab missing from ${JSON.stringify(tabs)}`);
  assert.ok(commerceIndex >= 0, `Commerce tab missing from ${JSON.stringify(tabs)}`);
  assert.ok(transactionsIndex >= 0, `Transactions history tab missing from ${JSON.stringify(tabs)}`);
  assert.ok(balancesIndex < commerceIndex, `Commerce tab must be after Balances: ${JSON.stringify(tabs)}`);
  assert.ok(commerceIndex < transactionsIndex, `Commerce tab must be before Transactions history: ${JSON.stringify(tabs)}`);
  return tabs;
}

async function openAccountHomeTab(page, name) {
  const tab = page.getByTestId('cda-account-home-tabs').getByRole('tab', { name }).first();
  await tab.waitFor();
  await tab.click();
}

async function ensureAliceWorkspace(page) {
  await page.goto(aliceUrl, { waitUntil: 'domcontentloaded' });
  await page.getByTestId('account-app-root').waitFor();
  if (await page.getByTestId('adapter-workspace-host').count() === 0) {
    await page.getByTestId('unlock-password').fill(alicePassword);
    await page.waitForFunction(() => {
      const button = document.querySelector('[data-testid="unlock-account"]');
      return button && !button.disabled;
    });
    await page.getByTestId('unlock-account').click();
  }
  await page.getByTestId('adapter-workspace-host').waitFor();
}

async function ensureBobWorkspace(page) {
  await page.goto(bobUrl, { waitUntil: 'domcontentloaded' });
  await page.getByTestId('account-app-root').waitFor();
  if (await page.getByTestId('adapter-workspace-host').count() === 0) {
    await page.getByTestId('unlock-password').fill(bobPassword);
    await page.waitForFunction(() => {
      const button = document.querySelector('[data-testid="unlock-account"]');
      return button && !button.disabled;
    });
    await page.getByTestId('unlock-account').click();
  }
  await page.getByTestId('adapter-workspace-host').waitFor();
}

async function findCommerceRow(dashboard, kind, candidates) {
  const rows = dashboard.locator(`[data-commerce-kind="${kind}"]`);
  await rows.first().waitFor();
  for (const candidate of candidates.filter(Boolean)) {
    const filtered = rows.filter({ hasText: candidate });
    if (await filtered.count() > 0) {
      return filtered.first();
    }
  }
  return rows.first();
}

async function captureTransactionFromHistory(page, txId, screenshotName, ensureWorkspace = ensureAliceWorkspace) {
  await ensureWorkspace(page);
  await page.getByTestId('refresh-workspace').click();
  await openAccountHomeTab(page, /Transactions history/i);
  const history = page.getByTestId('cda-recent-transactions');
  await history.waitFor();
  const row = history.locator('.cda-vef-transaction-item').filter({ hasText: txId.slice(0, 8) }).first();
  await row.waitFor();
  await row.getByTestId('cda-transaction-details-button').click();
  const txDialog = page.getByTestId('cda-transaction-details-dialog');
  await txDialog.waitFor();
  await clickInDialog(txDialog, /Inputs & outputs/i);
  await page.screenshot({ path: path.join(screenshotDir, screenshotName), fullPage: true });
  await closeDialogIfPresent(page, 'cda-transaction-details-dialog');
}

function selectTokenMergeCandidate(outputs) {
  return [...outputs].sort((left, right) => {
    const leftMulti = Number(amountValue(left.token.amount)) > 1 ? 1 : 0;
    const rightMulti = Number(amountValue(right.token.amount)) > 1 ? 1 : 0;
    if (leftMulti !== rightMulti) {
      return rightMulti - leftMulti;
    }

    const leftCreated = Date.parse(left.createdAt ?? left.CreatedAt ?? '1970-01-01T00:00:00Z');
    const rightCreated = Date.parse(right.createdAt ?? right.CreatedAt ?? '1970-01-01T00:00:00Z');
    if (leftCreated !== rightCreated) {
      return leftCreated - rightCreated;
    }

    const txCompare = unwrap(left.outPoint.txId).localeCompare(unwrap(right.outPoint.txId));
    if (txCompare !== 0) {
      return txCompare;
    }

    return Number(left.outPoint.outputIndex) - Number(right.outPoint.outputIndex);
  })[0] ?? null;
}

function assertSingle(items, label) {
  assert.equal(items.length, 1, `${label} count`);
  return items[0];
}

async function waitForWorkspaceOrUnlock(page, unlockButtonTestId) {
  await page.waitForFunction(testId => {
    const workspace = document.querySelector('[data-testid="adapter-workspace-host"]');
    const button = document.querySelector(`[data-testid="${testId}"]`);
    return Boolean(workspace) || Boolean(button && !button.disabled);
  }, unlockButtonTestId);
}

async function waitFor(predicate, label, attempts = 90) {
  for (let attempt = 0; attempt < attempts; attempt += 1) {
    const result = await predicate();
    if (result) {
      return result;
    }
    await new Promise(resolve => setTimeout(resolve, 1000));
  }
  throw new Error(`Timed out waiting for ${label}.`);
}

async function clickButton(page, name) {
  const button = page.getByRole('button', { name }).first();
  await button.waitFor();
  await button.click();
}

async function clickInDialog(dialog, text) {
  const byRole = dialog.getByRole('button', { name: text }).first();
  if (await byRole.count() > 0) {
    await byRole.click();
    return;
  }
  await dialog.getByText(text).first().click();
}

async function closeDialogIfPresent(page, testId) {
  const dialog = page.getByTestId(testId);
  if (await dialog.count() === 0) {
    return;
  }
  const closeButton = dialog.getByRole('button', { name: /^Close$/i }).last();
  if (await closeButton.count() > 0) {
    await closeButton.click();
  } else {
    await page.keyboard.press('Escape');
  }
  await dialog.waitFor({ state: 'detached' }).catch(() => {});
}

function unwrap(value) {
  if (value && typeof value === 'object' && 'value' in value) {
    return String(value.value);
  }
  return value == null ? null : String(value);
}

function amountValue(value) {
  const unwrapped = unwrap(value);
  if (unwrapped == null) {
    return null;
  }
  return String(Number(unwrapped)).replace(/\.0+$/, '');
}

function normalizeText(value) {
  return String(value ?? '').replace(/\s+/g, ' ').trim();
}
