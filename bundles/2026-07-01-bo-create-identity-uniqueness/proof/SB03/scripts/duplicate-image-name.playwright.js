async page => {
  const screenshotRoot = 'C:/repositories/CanDoItAll.Ledger/bundles/2026-07-01-bo-create-identity-uniqueness/proof/SB03/screenshots';
  const aliceBase = 'http://localhost:5081';
  const aliceAddress = 'cdia1Ngb3miLzYorvFSfKzLFwYPvZH6Kw6LPsSu';
  const backupPassword = 'accountapp-demo-password';
  const vaultPassword = 'playwright-identity-alice';
  const title = `Duplicate Image BO ${Date.now()}`;
  const expectedPrefix = `bo:image:${normalizeName(title)}:`;

  page.setDefaultTimeout(180000);
  await page.setViewportSize({ width: 1600, height: 950 });
  await unlockOrImportHome(`${aliceBase}/?view=image`);
  await page.getByTestId('bo-create-wizard').waitFor();

  await createImage(title, 'First duplicate-name image create from Playwright.');
  await page.getByRole('button', { name: /^New Image$/i }).click();
  await page.getByTestId('bo-create-wizard').waitFor();
  await createImage(title, 'Second duplicate-name image create from Playwright.');

  const created = await waitForDuplicateProjection(title);
  const ids = created.map(item => item.objectId);
  if (new Set(ids).size !== ids.length) {
    throw new Error(`Duplicate object IDs found for same-title image BOs: ${ids.join(', ')}`);
  }

  for (const id of ids) {
    if (!id.startsWith(expectedPrefix)) {
      throw new Error(`Created image id ${id} does not use expected prefix ${expectedPrefix}`);
    }
  }

  await page.getByPlaceholder('Search BOs...').fill(title);
  await page.locator('.cda-vef-gallery-card').filter({ hasText: title }).first().waitFor();
  await page.screenshot({
    path: `${screenshotRoot}/alice-duplicate-image-name-gallery.png`,
    fullPage: true
  });

  const bodyText = await page.locator('body').innerText();
  if (/already exists|Create must not overwrite/i.test(bodyText)) {
    throw new Error(`Duplicate-name create still surfaced a transition error: ${bodyText.slice(0, 1000)}`);
  }

  return {
    title,
    objectIds: ids,
    count: created.length,
    screenshot: 'alice-duplicate-image-name-gallery.png'
  };

  async function createImage(currentTitle, longText) {
    const wizard = page.getByTestId('bo-create-wizard');
    await wizard.getByRole('button', { name: /^Next$/i }).click();
    await page.getByLabel('BusinessObject name').fill(currentTitle);
    await page.getByLabel('BusinessObject description').fill('Same display title must not become the ledger identity.');
    await page.getByLabel('BusinessObject text').fill(longText);
    await wizard.getByRole('button', { name: /^Next$/i }).click();
    await wizard.getByRole('button', { name: /^Stage BO$/i }).click();
    await page.getByTestId('ssr-bo-gallery').waitFor();
    await page.getByPlaceholder('Search BOs...').fill(currentTitle);
    await page.locator('.cda-vef-gallery-card').filter({ hasText: currentTitle }).first().waitFor();
    await assertNoDuplicateTransitionError();
  }

  async function waitForDuplicateProjection(currentTitle) {
    for (let i = 0; i < 120; i += 1) {
      const items = await queryAliceImages(currentTitle);
      if (items.length >= 2) {
        return items;
      }

      await page.getByTestId('refresh-workspace').click();
      await page.waitForTimeout(1000);
    }

    const visibleText = await page.locator('body').innerText();
    throw new Error(`Timed out waiting for two same-title image BOs. Body: ${visibleText.slice(0, 1000)}`);
  }

  async function queryAliceImages(currentTitle) {
    return await page.evaluate(async args => {
      const response = await fetch('/api/v1/ledger/default/business-objects/query', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ownerAddress: args.aliceAddress,
          objectType: 'image',
          skip: 0,
          take: 200
        })
      });
      if (!response.ok) {
        throw new Error(`${response.status} ${response.statusText}`);
      }

      return (await response.json())
        .map(holding => {
          const payload = holding.stateJson ? JSON.parse(holding.stateJson) : {};
          return {
            objectId: holding.objectId?.value ?? holding.objectId,
            name: payload.name ?? '',
            text: payload.text ?? '',
            txId: holding.currentOutPoint?.txId?.value ?? holding.currentOutPoint?.txId
          };
        })
        .filter(item => item.name === args.title)
        .sort((left, right) => String(right.txId).localeCompare(String(left.txId)));
    }, { aliceAddress, title: currentTitle });
  }

  async function assertNoDuplicateTransitionError() {
    const bodyText = await page.locator('body').innerText();
    if (/already exists|Create must not overwrite/i.test(bodyText)) {
      throw new Error(`Unexpected duplicate transition text: ${bodyText.slice(0, 1000)}`);
    }
  }

  async function unlockOrImportHome(url) {
    await page.goto(url);
    await page.getByTestId('account-app-root').waitFor();
    if (await page.getByTestId('adapter-workspace-host').count() > 0) {
      return;
    }

    const unlock = page.getByTestId('unlock-account');
    if (await isUsableButton(unlock)) {
      await page.getByTestId('unlock-password').fill(vaultPassword);
      await unlock.click();
      await page.getByTestId('adapter-workspace-host').waitFor();
      return;
    }

    await page.getByTestId('backup-import-json').fill(aliceBackupJson());
    await page.getByTestId('backup-import-password').fill(backupPassword);
    await page.getByTestId('backup-new-password').fill(vaultPassword);
    await page.getByTestId('backup-confirm-password').fill(vaultPassword);
    await page.getByTestId('import-backup').click();
    await waitForWorkspaceOrUnlock();
    if (await page.getByTestId('adapter-workspace-host').count() > 0) {
      return;
    }

    await page.getByTestId('unlock-password').fill(vaultPassword);
    await page.getByTestId('unlock-account').click();
    await page.getByTestId('adapter-workspace-host').waitFor();
  }

  async function waitForWorkspaceOrUnlock() {
    for (let i = 0; i < 120; i += 1) {
      if (await page.getByTestId('adapter-workspace-host').count() > 0) {
        return;
      }

      if (await isUsableButton(page.getByTestId('unlock-account'))) {
        return;
      }

      await page.waitForTimeout(500);
    }

    throw new Error('Timed out waiting for workspace or unlock state.');
  }

  async function isUsableButton(locator) {
    if (await locator.count() === 0) {
      return false;
    }

    return !(await locator.first().isDisabled({ timeout: 1000 }).catch(() => true));
  }

  function normalizeName(value) {
    const normalized = String(value)
      .trim()
      .replace(/[^a-zA-Z0-9._-]+/g, '-')
      .replace(/-+/g, '-')
      .replace(/^[-._]+|[-._]+$/g, '')
      .toLowerCase();
    return normalized || 'business-object';
  }

  function aliceBackupJson() {
    return JSON.stringify({
      format: 'candoitall-economy-accountapp-vault-backup-v1',
      exportedAtUtc: '2026-06-29T22:32:07.6130809+00:00',
      record: {
        vaultId: 'accountapp-demo-alice-source',
        displayName: 'Alice demo account',
        keyId: 'compose-demo-alice-b8082499ad8e49e588a7ade91533c821',
        address: { value: aliceAddress },
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
}
