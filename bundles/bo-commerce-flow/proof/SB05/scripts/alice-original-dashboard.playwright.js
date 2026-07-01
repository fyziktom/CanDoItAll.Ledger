async page => {
  const objectId = 'bo:post:purchase-order-post';
  const screenshotRoot = 'C:/repositories/CanDoItAll.Ledger/bundles/bo-commerce-flow/proof/SB05/browser';

  await page.setViewportSize({ width: 1600, height: 950 });
  page.setDefaultTimeout(120000);
  await page.goto('http://localhost:5081/');
  await page.getByTestId('account-app-root').waitFor();
  await unlockOrImportAlice();
  await page.getByTestId('adapter-workspace-host').waitFor();

  const dashboard = page.getByTestId('cda-commerce-dashboard');
  await dashboard.waitFor();
  await dashboard.scrollIntoViewIfNeeded();
  const paymentRow = dashboard.getByTestId('cda-commerce-record')
    .filter({ hasText: `Payment for ${objectId}` })
    .filter({ hasText: 'Dispatched' })
    .first();
  await paymentRow.waitFor();
  const receiptRow = dashboard.getByTestId('cda-commerce-record')
    .filter({ hasText: `Receipt for ${objectId}` })
    .filter({ hasText: 'Dispatched' })
    .first();
  await receiptRow.waitFor();
  await page.screenshot({
    path: `${screenshotRoot}/alice-original-dispatch-dashboard.png`,
    fullPage: true
  });

  return {
    objectId,
    paymentRow: (await paymentRow.textContent())?.trim(),
    receiptRow: (await receiptRow.textContent())?.trim(),
    screenshots: ['alice-original-dispatch-dashboard.png']
  };

  async function unlockOrImportAlice() {
    if (await page.getByTestId('adapter-workspace-host').count() > 0) {
      return;
    }

    const unlockButton = page.getByTestId('unlock-account');
    if (await unlockButton.count() > 0 && !(await unlockButton.isDisabled())) {
      await page.getByTestId('unlock-password').fill('commerce-proof-alice');
      await unlockButton.click();
      await page.getByTestId('adapter-workspace-host').waitFor();
      return;
    }

    await page.getByTestId('backup-import-json').fill(aliceBackupJson());
    await page.getByTestId('backup-import-password').fill('accountapp-demo-password');
    await page.getByTestId('backup-new-password').fill('commerce-proof-alice');
    await page.getByTestId('backup-confirm-password').fill('commerce-proof-alice');
    await page.getByTestId('import-backup').click();
    await page.getByTestId('adapter-workspace-host').waitFor();
  }

  function aliceBackupJson() {
    return JSON.stringify({
      format: 'candoitall-economy-accountapp-vault-backup-v1',
      exportedAtUtc: '2026-06-29T22:32:07.6130809+00:00',
      record: {
        vaultId: 'accountapp-demo-alice-source',
        displayName: 'Alice demo account',
        keyId: 'compose-demo-alice-b8082499ad8e49e588a7ade91533c821',
        address: { value: 'cdia1Ngb3miLzYorvFSfKzLFwYPvZH6Kw6LPsSu' },
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
