async page => {
  const backup = String.raw`{
  "format": "candoitall-economy-accountapp-vault-backup-v1",
  "exportedAtUtc": "2026-06-29T22:32:07.6130809+00:00",
  "record": {
    "vaultId": "accountapp-demo-alice-source",
    "displayName": "Alice demo account",
    "keyId": "compose-demo-alice-b8082499ad8e49e588a7ade91533c821",
    "address": {
      "value": "cdia1Ngb3miLzYorvFSfKzLFwYPvZH6Kw6LPsSu"
    },
    "algorithm": 1,
    "publicKeyHash": "e2c690e20e607774bc888fae8af35dd9a91ae55a3f33c87372f9e707907220fa",
    "publicKeyMaterialBase64": "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE3t0q1H39Qe\u002BF9AtCLw4aMITrQPi8hPWXA0AWcGSvpZRQmfUHbPubMLjIJIwoz/a1mwAa4BkXk9ArbAQC7xY/8g==",
    "encryptedPrivateKeyBase64": "0Da3E1mGIPbnEHRXLsgJFnBOSeBvAxqfQ4k28kPdqgz1zb4L3lbLAfjVfHi1l0J2EXvxkPiSkttS26g2Byk6OT5tFYo41bMl8Qvv3xdzQyFLsYDQD8LLXPPlMI2gvbQme/6GZCOaBXMiulARGxUKECxLDEjvZtdH5/UWZchxZ6qur9Qj6D7FaM9F/RzgieqtVDDkQNOA5QT5mg==",
    "kdfSaltBase64": "eStErXC1j/\u002BWdpkJ4t36tw==",
    "nonceBase64": "sRkGalmuZww82BEV",
    "kdfIterations": 150000,
    "kdfAlgorithm": "PBKDF2-SHA256",
    "encryptionAlgorithm": "AES-256-GCM",
    "createdAtUtc": "2026-06-29T22:32:07.6119885+00:00",
    "updatedAtUtc": "2026-06-29T22:32:07.6119885+00:00"
  }
}`;

  await page.getByTestId('backup-import-json').fill(backup);
  await page.getByTestId('backup-import-password').fill('accountapp-demo-password');
  await page.getByTestId('backup-new-password').fill('refresh-proof-password');
  await page.getByTestId('backup-confirm-password').fill('refresh-proof-password');
  await page.getByTestId('import-backup').click();
  await page.getByTestId('adapter-workspace-host').waitFor({ timeout: 60000 });

  return {
    title: await page.title(),
    unlocked: await page.locator('[data-testid="adapter-workspace-host"]').count(),
    rootClass: await page.locator('[data-testid="account-app-root"]').getAttribute('class')
  };
}
