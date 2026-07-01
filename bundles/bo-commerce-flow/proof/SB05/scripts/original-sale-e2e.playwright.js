async page => {
  const originalObjectId = 'bo:post:purchase-order-post';
  const sellerAddress = 'cdia1Ngb3miLzYorvFSfKzLFwYPvZH6Kw6LPsSu';
  const buyerAddress = 'cdia1NTXHdDX3kHtpHwtEFyCFuHPNkqBiV8k4uU';
  const screenshotRoot = 'C:/repositories/CanDoItAll.Ledger/bundles/bo-commerce-flow/proof/SB05/browser';

  await page.setViewportSize({ width: 1600, height: 950 });
  page.setDefaultTimeout(300000);
  const alice = page;
  const bob = await page.context().newPage();
  await bob.setViewportSize({ width: 1600, height: 950 });
  bob.setDefaultTimeout(300000);

  let current = await loadHolding(alice, originalObjectId);
  let paymentId = await findLatestPaymentId(alice, originalObjectId);

  if (current?.ownerAddress !== buyerAddress) {
    await ensureAliceHome(alice);
    await closeKnownDialogs(alice);
    await listOriginalSale(alice);
    await waitForOriginalSale(alice);

    await buyOriginalWithBob(bob);
    paymentId = await waitForPaymentForTarget(bob, originalObjectId);

    await alice.goto('http://localhost:5081/');
    await ensureAliceHome(alice);
    await alice.getByTestId('refresh-workspace').click();
    await waitForOriginalDispatch(alice, paymentId);
  }

  const finalPaymentId = paymentId ?? await findLatestPaymentId(alice, originalObjectId);
  if (!finalPaymentId) {
    throw new Error(`No payment record found for ${originalObjectId}.`);
  }

  const dispatched = await waitForOriginalDispatch(alice, finalPaymentId);
  await verifyBobOwnsOriginal(bob, finalPaymentId, dispatched.payment);

  return {
    objectId: originalObjectId,
    paymentObjectId: finalPaymentId,
    ownerAddress: dispatched.target.ownerAddress,
    paymentStatus: dispatched.payment.commerce.payment.status,
    deliveredObjectId: dispatched.payment.commerce.payment.deliveredObjectId,
    receiptObjectId: dispatched.payment.commerce.payment.receiptObjectId,
    purchaseObjectId: dispatched.payment.commerce.payment.purchaseObjectId,
    screenshots: [
      'alice-original-sale-dialog.png',
      'bob-original-buy-ready.png',
      'bob-original-buy-submitted.png',
      'alice-original-dispatch-dashboard.png',
      'bob-original-purchase-dashboard.png',
      'bob-original-bo-gallery.png'
    ]
  };

  async function ensureAliceHome(alicePage) {
    await alicePage.goto('http://localhost:5081/');
    await alicePage.getByTestId('account-app-root').waitFor();
    if (await alicePage.getByTestId('adapter-workspace-host').count() > 0) {
      return;
    }

    if (await canUnlockStoredVault(alicePage, 'unlock-account')) {
      await alicePage.getByTestId('unlock-password').fill('commerce-proof-alice');
      await alicePage.getByTestId('unlock-account').click();
      await alicePage.getByTestId('adapter-workspace-host').waitFor();
      return;
    }

    const backup = aliceBackupJson();
    await alicePage.getByTestId('backup-import-json').fill(backup);
    await alicePage.getByTestId('backup-import-password').fill('accountapp-demo-password');
    await alicePage.getByTestId('backup-new-password').fill('commerce-proof-alice');
    await alicePage.getByTestId('backup-confirm-password').fill('commerce-proof-alice');
    await alicePage.getByTestId('import-backup').click();
    await waitForWorkspaceOrUnlock(alicePage, 'unlock-account');
    if (await alicePage.getByTestId('adapter-workspace-host').count() === 0) {
      await alicePage.getByTestId('unlock-password').fill('commerce-proof-alice');
      await alicePage.getByTestId('unlock-account').click();
    }

    await alicePage.getByTestId('adapter-workspace-host').waitFor();
  }

  async function listOriginalSale(alicePage) {
    await openGallery(alicePage);
    await alicePage.getByPlaceholder('Search BOs...').fill(originalObjectId);
    const card = alicePage.locator('.cda-vef-gallery-card')
      .filter({ hasText: 'Purchase' })
      .first();
    await card.waitFor();
    await card.locator('button[aria-label="Open BO details"]').click();
    const details = alicePage.getByTestId('cda-bo-details-dialog');
    await details.waitFor();
    await details.getByText('Actions', { exact: true }).click().catch(() => {});
    await details.getByRole('button', { name: /Set Sale/i }).click();

    const dialog = alicePage.getByTestId('cda-bo-set-price-dialog');
    await dialog.waitFor();
    await alicePage.getByTestId('bo-sale-mode').selectOption('Original');
    await alicePage.getByTestId('bo-sale-price').fill('4.5');
    await alicePage.screenshot({
      path: `${screenshotRoot}/alice-original-sale-dialog.png`,
      fullPage: true
    });
    await dialog.getByRole('button', { name: /^Set Sale$/i }).click();
  }

  async function buyOriginalWithBob(bobPage) {
    await bobPage.goto(`http://localhost:5082/buy-bo/${encodeURIComponent(originalObjectId)}`);
    await bobPage.getByTestId('buy-bo-page').waitFor();
    await bobPage.getByTestId('buy-bo-listing').waitFor();
    await unlockOrImportBobOnBuyPage(bobPage);
    await bobPage.getByTestId('buy-bo-account').waitFor();
    await bobPage.waitForFunction(() => {
      const button = document.querySelector('[data-testid="buy-bo-submit"]');
      return button && !button.disabled;
    });
    await bobPage.screenshot({
      path: `${screenshotRoot}/bob-original-buy-ready.png`,
      fullPage: true
    });
    await bobPage.getByTestId('buy-bo-submit').click();
    await bobPage.getByTestId('buy-bo-payment-result').getByText('Payment submitted').waitFor();
    await bobPage.screenshot({
      path: `${screenshotRoot}/bob-original-buy-submitted.png`,
      fullPage: true
    });
  }

  async function unlockOrImportBobOnBuyPage(bobPage) {
    if (await bobPage.getByTestId('buy-bo-submit').count() > 0) {
      return;
    }

    const unlockButton = bobPage.getByTestId('buy-unlock-account');
    if (await unlockButton.count() > 0 && !(await unlockButton.isDisabled())) {
      await bobPage.getByTestId('buy-unlock-password').fill('commerce-proof-bob');
      await unlockButton.click();
      await bobPage.getByTestId('buy-bo-submit').waitFor();
      return;
    }

    await bobPage.getByTestId('buy-backup-import-json').fill(bobBackupJson());
    await bobPage.getByTestId('buy-backup-import-password').fill('accountapp-demo-password');
    await bobPage.getByTestId('buy-backup-new-password').fill('commerce-proof-bob');
    await bobPage.getByTestId('buy-backup-confirm-password').fill('commerce-proof-bob');
    await bobPage.getByTestId('buy-import-backup-submit').click();
    await bobPage.getByTestId('buy-bo-submit').waitFor();
  }

  async function verifyBobOwnsOriginal(bobPage, paymentObjectId, paymentPayload) {
    await bobPage.goto('http://localhost:5082/');
    await bobPage.getByTestId('account-app-root').waitFor();
    if (await bobPage.getByTestId('adapter-workspace-host').count() === 0) {
      if (await canUnlockStoredVault(bobPage, 'unlock-account')) {
        await bobPage.getByTestId('unlock-password').fill('commerce-proof-bob');
        await bobPage.getByTestId('unlock-account').click();
      } else {
        await bobPage.getByTestId('backup-import-json').fill(bobBackupJson());
        await bobPage.getByTestId('backup-import-password').fill('accountapp-demo-password');
        await bobPage.getByTestId('backup-new-password').fill('commerce-proof-bob');
        await bobPage.getByTestId('backup-confirm-password').fill('commerce-proof-bob');
        await bobPage.getByTestId('import-backup').click();
      }
      await bobPage.getByTestId('adapter-workspace-host').waitFor();
    }

    await bobPage.getByTestId('refresh-workspace').click();
    const dashboard = bobPage.getByTestId('cda-commerce-dashboard');
    await dashboard.waitFor();
    await dashboard.scrollIntoViewIfNeeded();
    const purchaseId = paymentPayload.commerce.payment.purchaseObjectId;
    const purchaseRow = dashboard.getByTestId('cda-commerce-record')
      .filter({ hasText: `Purchase for ${originalObjectId}` })
      .filter({ hasText: 'Dispatched' })
      .first();
    await purchaseRow.waitFor();
    await bobPage.screenshot({
      path: `${screenshotRoot}/bob-original-purchase-dashboard.png`,
      fullPage: true
    });

    await openGallery(bobPage);
    await bobPage.getByPlaceholder('Search BOs...').fill(originalObjectId);
    await bobPage.locator('.cda-vef-gallery-card')
      .filter({ hasText: 'Purchase' })
      .first()
      .waitFor();
    await bobPage.screenshot({
      path: `${screenshotRoot}/bob-original-bo-gallery.png`,
      fullPage: true
    });

    const purchasePayload = await loadHolding(bobPage, purchaseId);
    if (purchasePayload?.ownerAddress !== buyerAddress) {
      throw new Error(`Purchase record ${purchaseId} is not owned by Bob.`);
    }

    const targetPayload = await loadHolding(bobPage, originalObjectId);
    if (targetPayload?.ownerAddress !== buyerAddress) {
      throw new Error(`${originalObjectId} is not owned by Bob after dispatch.`);
    }
  }

  async function waitForOriginalSale(anyPage) {
    for (let attempt = 0; attempt < 90; attempt += 1) {
      const holding = await loadHolding(anyPage, originalObjectId);
      const sale = holding?.commerce?.sale;
      if ((sale?.status === 'Listed' || sale?.status === 1) && (sale?.mode === 'Original' || sale?.mode === 0)) {
        return holding;
      }
      await anyPage.waitForTimeout(1000);
    }
    throw new Error(`Timed out waiting for ${originalObjectId} original sale terms.`);
  }

  async function waitForPaymentForTarget(anyPage, targetObjectId) {
    for (let attempt = 0; attempt < 90; attempt += 1) {
      const payment = await findLatestPaymentId(anyPage, targetObjectId, true);
      if (payment) {
        return payment;
      }
      await anyPage.waitForTimeout(1000);
    }
    throw new Error(`Timed out waiting for payment for ${targetObjectId}.`);
  }

  async function waitForOriginalDispatch(anyPage, paymentObjectId) {
    for (let attempt = 0; attempt < 120; attempt += 1) {
      const payment = await loadHolding(anyPage, paymentObjectId);
      const target = await loadHolding(anyPage, originalObjectId);
      const record = payment?.commerce?.payment;
      if ((record?.status === 'Dispatched' || record?.status === 1)
        && record?.deliveredObjectId === originalObjectId
        && target?.ownerAddress === buyerAddress) {
        await anyPage.goto('http://localhost:5081/');
        await anyPage.getByTestId('adapter-workspace-host').waitFor();
        const dashboard = anyPage.getByTestId('cda-commerce-dashboard');
        await dashboard.waitFor();
        await dashboard.scrollIntoViewIfNeeded();
        await anyPage.screenshot({
          path: `${screenshotRoot}/alice-original-dispatch-dashboard.png`,
          fullPage: true
        });
        return { payment, target };
      }
      await anyPage.waitForTimeout(1000);
    }
    throw new Error(`Timed out waiting for original dispatch for ${paymentObjectId}.`);
  }

  async function findLatestPaymentId(anyPage, targetObjectId, preferPending = false) {
    return await anyPage.evaluate(async args => {
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
          const state = JSON.parse(holding.stateJson);
          const payment = state.commerce?.payment;
          return payment?.targetObjectId === args.targetObjectId
            ? {
              id: holding.objectId?.value ?? holding.objectId,
              status: payment.status,
              createdAtUtc: payment.createdAtUtc
            }
            : null;
        })
        .filter(Boolean)
        .sort((a, b) => String(b.createdAtUtc).localeCompare(String(a.createdAtUtc)));

      if (args.preferPending) {
        const pending = rows.find(row => row.status === 'Pending' || row.status === 0);
        if (pending) {
          return pending.id;
        }
      }

      return rows[0]?.id ?? null;
    }, { sellerAddress, targetObjectId, preferPending });
  }

  async function loadHolding(anyPage, objectId) {
    return await anyPage.evaluate(async id => {
      const response = await fetch('/api/v1/ledger/default/business-objects/query', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ objectId: id, skip: 0, take: 1 })
      });
      if (!response.ok) {
        throw new Error(`${response.status} ${response.statusText}`);
      }

      const holdings = await response.json();
      const holding = holdings[0];
      if (!holding?.stateJson) {
        return null;
      }

      return {
        objectId: holding.objectId?.value ?? id,
        ownerAddress: holding.ownerAddress?.value,
        version: holding.version,
        commerce: JSON.parse(holding.stateJson).commerce ?? null
      };
    }, objectId);
  }

  async function openGallery(anyPage) {
    const galleryButton = anyPage.getByRole('button', { name: /^Gallery$/i }).first();
    await galleryButton.waitFor();
    await galleryButton.click();
    await anyPage.getByTestId('ssr-bo-gallery').waitFor();
  }

  async function closeKnownDialogs(anyPage) {
    for (const testId of ['cda-commerce-details-dialog', 'cda-bo-details-dialog', 'cda-bo-set-price-dialog']) {
      while (await anyPage.getByTestId(testId).count() > 0) {
        const dialog = anyPage.getByTestId(testId).first();
        const close = dialog.getByRole('button', { name: /^Close$/i });
        if (await close.count() === 0) {
          break;
        }
        await close.click();
        await anyPage.waitForTimeout(200);
      }
    }
  }

  async function canUnlockStoredVault(anyPage, unlockButtonTestId) {
    const button = anyPage.getByTestId(unlockButtonTestId);
    return await button.count() > 0 && !(await button.isDisabled());
  }

  async function waitForWorkspaceOrUnlock(anyPage, unlockButtonTestId) {
    await anyPage.waitForFunction(testId => {
      const workspace = document.querySelector('[data-testid="adapter-workspace-host"]');
      const button = document.querySelector(`[data-testid="${testId}"]`);
      return Boolean(workspace) || Boolean(button && !button.disabled);
    }, unlockButtonTestId);
  }

  function aliceBackupJson() {
    return JSON.stringify({
      format: 'candoitall-economy-accountapp-vault-backup-v1',
      exportedAtUtc: '2026-06-29T22:32:07.6130809+00:00',
      record: {
        vaultId: 'accountapp-demo-alice-source',
        displayName: 'Alice demo account',
        keyId: 'compose-demo-alice-b8082499ad8e49e588a7ade91533c821',
        address: { value: sellerAddress },
        algorithm: 1,
        publicKeyHash: 'e2c690e20e607774bc888fae8af35dd9a91ae55a3f33c87372f9e707907220fa',
        publicKeyMaterialBase64: 'MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE3t0q1H39Qe+F9AtCLw4aMITrQPi8hPWXA0AWcGSvpZRQmfUHbPubMLjIJIwoz/a1mwAa4BkXk9ArbAQC7xY/8g==',
        encryptedPrivateKeyBase64: '0Da3E1mGIPbnEHRXLsgJFnBOSeBvAxqfQ4k28kPdqgz1zb4L3lbLAfjVfHi1l0J2EXvxkPiSkttS26g2Byk6OT5tFYo41bMl8Qvv3xdzQyFLsYDQD8LLXPPlMI2gvbQme/6GZCOaBXMiulARGxUKECxLDEjvZtdH5/UWZchxZ6qur9Qj6D7FaM9F/RzgieqtVDDkQNOA5QT5mg==',
        kdfSaltBase64: 'eStErXC1j/+WdpkJ4t36tw==',
        nonceBase64: 'sRkGalmuZww82BEV',
        kdfIterations: 150000,
        kdfAlgorithm: 'PBKDF2-SHA256',
        encryptionAlgorithm: 'AES-256-GCM',
        createdAtUtc: '2026-06-29T22:32:07.6119885+00:00',
        updatedAtUtc: '2026-06-29T22:32:07.6119885+00:00'
      }
    }, null, 2);
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
