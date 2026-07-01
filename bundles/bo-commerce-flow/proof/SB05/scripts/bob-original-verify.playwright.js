async page => {
  const objectId = 'bo:post:purchase-order-post';
  const buyerAddress = 'cdia1NTXHdDX3kHtpHwtEFyCFuHPNkqBiV8k4uU';
  const screenshotRoot = 'C:/repositories/CanDoItAll.Ledger/bundles/bo-commerce-flow/proof/SB05/browser';

  await page.setViewportSize({ width: 1600, height: 950 });
  page.setDefaultTimeout(120000);
  await page.goto('http://localhost:5082/');
  await page.getByTestId('account-app-root').waitFor();
  await unlockOrImportBob();
  await page.getByTestId('adapter-workspace-host').waitFor();
  await page.getByTestId('refresh-workspace').click();

  const payment = await latestPaymentForTarget();
  const target = await loadHolding(objectId);
  const purchase = await loadHolding(payment.purchaseObjectId);
  if (target?.ownerAddress !== buyerAddress) {
    throw new Error(`${objectId} is not owned by Bob.`);
  }
  if (purchase?.ownerAddress !== buyerAddress) {
    throw new Error(`${payment.purchaseObjectId} is not owned by Bob.`);
  }

  const dashboard = page.getByTestId('cda-commerce-dashboard');
  await dashboard.waitFor();
  await dashboard.scrollIntoViewIfNeeded();
  const purchaseRow = dashboard.getByTestId('cda-commerce-record')
    .filter({ hasText: `Purchase for ${objectId}` })
    .filter({ hasText: 'Dispatched' })
    .first();
  await purchaseRow.waitFor();
  await page.screenshot({
    path: `${screenshotRoot}/bob-original-purchase-dashboard.png`,
    fullPage: true
  });

  await openGallery();
  await page.getByPlaceholder('Search BOs...').fill(objectId);
  await page.locator('.cda-vef-gallery-card')
    .filter({ hasText: 'Purchase' })
    .first()
    .waitFor();
  await page.screenshot({
    path: `${screenshotRoot}/bob-original-bo-gallery.png`,
    fullPage: true
  });

  return {
    objectId,
    paymentObjectId: payment.paymentObjectId,
    purchaseObjectId: payment.purchaseObjectId,
    receiptObjectId: payment.receiptObjectId,
    ownerAddress: target.ownerAddress,
    paymentStatus: payment.status,
    screenshots: [
      'bob-original-purchase-dashboard.png',
      'bob-original-bo-gallery.png'
    ]
  };

  async function unlockOrImportBob() {
    if (await page.getByTestId('adapter-workspace-host').count() > 0) {
      return;
    }

    const unlockButton = page.getByTestId('unlock-account');
    if (await unlockButton.count() > 0 && !(await unlockButton.isDisabled())) {
      await page.getByTestId('unlock-password').fill('commerce-proof-bob');
      await unlockButton.click();
      await page.getByTestId('adapter-workspace-host').waitFor();
      return;
    }

    await page.getByTestId('backup-import-json').fill(bobBackupJson());
    await page.getByTestId('backup-import-password').fill('accountapp-demo-password');
    await page.getByTestId('backup-new-password').fill('commerce-proof-bob');
    await page.getByTestId('backup-confirm-password').fill('commerce-proof-bob');
    await page.getByTestId('import-backup').click();
    await page.getByTestId('adapter-workspace-host').waitFor();
  }

  async function latestPaymentForTarget() {
    return await page.evaluate(async args => {
      const response = await fetch('/api/v1/ledger/default/business-objects/query', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ownerAddress: args.sellerAddress,
          objectType: 'payment',
          skip: 0,
          take: 100
        })
      });
      if (!response.ok) {
        throw new Error(`${response.status} ${response.statusText}`);
      }

      const rows = (await response.json())
        .map(holding => {
          if (!holding?.stateJson) {
            return null;
          }
          const payment = JSON.parse(holding.stateJson).commerce?.payment;
          return payment?.targetObjectId === args.objectId
            ? {
              paymentObjectId: holding.objectId?.value ?? holding.objectId,
              status: payment.status,
              createdAtUtc: payment.createdAtUtc,
              purchaseObjectId: payment.purchaseObjectId,
              receiptObjectId: payment.receiptObjectId
            }
            : null;
        })
        .filter(Boolean)
        .sort((a, b) => String(b.createdAtUtc).localeCompare(String(a.createdAtUtc)));

      return rows[0] ?? null;
    }, { objectId, sellerAddress: 'cdia1Ngb3miLzYorvFSfKzLFwYPvZH6Kw6LPsSu' });
  }

  async function loadHolding(currentObjectId) {
    return await page.evaluate(async id => {
      const response = await fetch('/api/v1/ledger/default/business-objects/query', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ objectId: id, skip: 0, take: 1 })
      });
      if (!response.ok) {
        throw new Error(`${response.status} ${response.statusText}`);
      }

      const holding = (await response.json())[0];
      return holding
        ? {
          objectId: holding.objectId?.value ?? id,
          ownerAddress: holding.ownerAddress?.value,
          version: holding.version
        }
        : null;
    }, currentObjectId);
  }

  async function openGallery() {
    const galleryButton = page.getByRole('button', { name: /^Gallery$/i }).first();
    await galleryButton.waitFor();
    await galleryButton.click();
    await page.getByTestId('ssr-bo-gallery').waitFor();
  }

  function bobBackupJson() {
    return JSON.stringify({
      format: 'candoitall-economy-accountapp-vault-backup-v1',
      exportedAtUtc: '2026-06-29T22:32:08.0610606+00:00',
      record: {
        vaultId: 'accountapp-demo-bob-source',
        displayName: 'Bob demo account',
        keyId: 'compose-demo-bob-918c8c21f3624fd08eb1d8db14ed6ec0',
        address: { value: buyerAddress },
        algorithm: 1,
        publicKeyHash: '5376d4cba4967d0325bc702414a19a9ca82c6ee30fdf75314415487eabe23333',
        publicKeyMaterialBase64: 'MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEwZUkVAgWsEBeZsjIcjwKqDMiet+JYRL6DYbEdMallslXESVkbDql8Jia0e4WyRLM/RHYdT6Wk1uPpGNvvftJMg==',
        encryptedPrivateKeyBase64: 'PIv6tMTT1Q5hYu9aZhp9HpJ+Gn2zsYQZ8mSUJbwx/96wTrSQTKx74SI3O4j+YcTKriJjys324UlXizt71384kPXZRdgd5TcC+tVhpOwuoat+069o7c/fipRUjEv6pfCL7U8ord8rI04BFcaLCYSxwillm9sVl3UK4c7rZBlFs9awfu38TbHNZRlzXemHl4k9oZF7DPAAxOjJfg==',
        kdfSaltBase64: 'brwKX4rq+wmQ35CqL8v5kQ==',
        nonceBase64: 'vN/4M/qzYzqxyl/o',
        kdfIterations: 150000,
        kdfAlgorithm: 'PBKDF2-SHA256',
        encryptionAlgorithm: 'AES-256-GCM',
        createdAtUtc: '2026-06-29T22:32:08.061012+00:00',
        updatedAtUtc: '2026-06-29T22:32:08.061012+00:00'
      }
    }, null, 2);
  }
}
