async page => {
  const screenshotRoot = 'C:/repositories/CanDoItAll.Ledger/bundles/2026-07-01-ticket-profile-menu/proof/screenshots';
  const aliceUrl = 'http://localhost:5081/';
  const bobUrl = 'http://localhost:5082/';
  const backupPassword = 'accountapp-demo-password';
  const aliceVaultPassword = 'ticket-proof-alice';
  const bobVaultPassword = 'ticket-proof-bob';
  const runId = Date.now();
  const eventName = `Playwright Ledger Event ${runId}`;
  const ticketName = `Playwright Ledger Ticket ${runId}`;
  const aliceAddress = 'cdia1Ngb3miLzYorvFSfKzLFwYPvZH6Kw6LPsSu';

  page.setDefaultTimeout(120000);
  await page.setViewportSize({ width: 1700, height: 1000 });

  await page.goto(aliceUrl);
  await page.getByTestId('account-app-root').waitFor();
  await importIfLocked(aliceBackupJson(), backupPassword, aliceVaultPassword);
  await page.getByTestId('adapter-workspace-host').waitFor();
  await page.getByTestId('refresh-workspace').click();
  await page.getByTestId('adapter-workspace-host').waitFor();
  await assertMintMenu();
  await page.screenshot({ path: `${screenshotRoot}/alice-menu-without-background-bo-types.png`, fullPage: true });

  await openProfileAndSave(runId);

  await createEvent();
  const eventHolding = await latestHolding('event', eventName, aliceAddress);
  if (!eventHolding?.objectId) {
    throw new Error(`Created event ${eventName} was not found through the node API.`);
  }

  await createTicket(eventHolding.objectId);
  const ticketHolding = await latestHolding('ticket', ticketName, aliceAddress);
  if (!ticketHolding?.objectId) {
    throw new Error(`Created ticket ${ticketName} was not found through the node API.`);
  }

  await openTicketDetails(ticketName);
  await page.getByRole('tab', { name: /Ticket/i }).click();
  await page.getByRole('button', { name: /Sign Ownership/i }).click();
  const message = page.getByTestId('cda-ticket-verification-message');
  await message.waitFor();
  const messageText = await message.textContent();
  if (!messageText?.includes('CanDoItAll Ledger ticket ownership verification') || !messageText.includes('Hash:')) {
    throw new Error(`Ticket verification message was not generated correctly: ${messageText}`);
  }
  await page.getByTestId('cda-ticket-verification-signature').waitFor();
  await page.getByTestId('cda-ticket-qr-mock').waitFor();
  await page.screenshot({ path: `${screenshotRoot}/ticket-verification-signed-hash.png`, fullPage: true });

  await page.getByRole('button', { name: /Use Ticket/i }).click();
  await waitForTicketUsed(ticketHolding.objectId);
  await page.screenshot({ path: `${screenshotRoot}/ticket-used-after-ledger-update.png`, fullPage: true });

  await page.goto(bobUrl);
  await page.getByTestId('account-app-root').waitFor();
  await importIfLocked(bobBackupJson(), backupPassword, bobVaultPassword);
  await page.getByTestId('adapter-workspace-host').waitFor();
  await assertMintMenu();

  return {
    aliceMenuChecked: true,
    bobMenuChecked: true,
    eventObjectId: eventHolding.objectId,
    ticketObjectId: ticketHolding.objectId,
    ticketVerificationHashShown: true,
    ticketUsed: true,
    screenshots: [
      'alice-menu-without-background-bo-types.png',
      'ticket-verification-signed-hash.png',
      'ticket-used-after-ledger-update.png'
    ]
  };

  async function importIfLocked(backupJson, oldPassword, newPassword) {
    if (await page.getByTestId('adapter-workspace-host').count() > 0) {
      return;
    }

    const unlockButton = page.getByTestId('unlock-account');
    if (await unlockButton.count() > 0 && !(await unlockButton.isDisabled())) {
      await page.getByTestId('unlock-password').fill(newPassword);
      await unlockButton.click();
      await page.getByTestId('adapter-workspace-host').waitFor();
      return;
    }

    await page.getByTestId('backup-import-json').fill(backupJson);
    await page.getByTestId('backup-import-password').fill(oldPassword);
    await page.getByTestId('backup-new-password').fill(newPassword);
    await page.getByTestId('backup-confirm-password').fill(newPassword);
    await page.getByTestId('import-backup').click();
    await page.getByTestId('adapter-workspace-host').waitFor();
  }

  async function assertMintMenu() {
    const nav = page.locator('.cda-vef-nav');
    await nav.waitFor();
    const text = await nav.textContent();
    for (const forbidden of ['New Payment', 'New Purchase', 'New Receipt', 'New Profile']) {
      if (text?.includes(forbidden)) {
        throw new Error(`${forbidden} is still visible in the minting menu.`);
      }
    }
    for (const expected of ['New Event', 'New Ticket']) {
      if (!text?.includes(expected)) {
        throw new Error(`${expected} is missing from the minting menu.`);
      }
    }
  }

  async function openProfileAndSave(id) {
    await page.getByRole('button', { name: /^Profile$/i }).click();
    await page.getByTestId('cda-profile-edit-dialog').waitFor();
    await page.getByLabel('Profile display name').fill(`Alice profile ${id}`);
    await page.getByLabel('Profile nickname').fill(`alice-${id}`);
    await page.getByLabel('Profile bio').fill('Profile BO created or updated from the home dashboard.');
    await page.getByRole('button', { name: /Create Profile|Save Profile/i }).click();
    await page.getByTestId('cda-profile-edit-dialog').waitFor({ state: 'detached' });
  }

  async function createEvent() {
    await page.goto(`${aliceUrl}?view=event`);
    await importIfLocked(aliceBackupJson(), backupPassword, aliceVaultPassword);
    await page.getByTestId('bo-create-wizard').waitFor();
    await page.getByRole('button', { name: /^Next$/i }).click();
    await page.getByLabel('BusinessObject name').fill(eventName);
    await page.getByLabel('BusinessObject tags').fill('#event #playwright #ticket');
    await page.getByLabel('BusinessObject description').fill('Playwright-created event for ticket validation.');
    await page.getByLabel('BusinessObject text').fill('This event carries the VEFramework-style fields required by Ledger tickets.');
    await page.getByLabel('Event id').fill(`event-${runId}`);
    await page.getByLabel('Event date').fill('2026-07-01T18:00:00Z');
    await page.getByLabel('Event class').fill('Conference');
    await page.getByLabel('Location', { exact: true }).fill('Prague');
    await page.getByLabel('Location coordinates', { exact: true }).fill('50.087,14.421');
    await page.getByLabel('Organizer').fill('CanDoItAll');
    await page.getByLabel('Mint author address').fill(aliceAddress);
    await page.getByLabel('Status', { exact: true }).fill('Scheduled');
    await page.getByRole('button', { name: /^Next$/i }).click();
    await page.getByRole('button', { name: /^Stage BO Transaction$/i }).click();
    await page.getByTestId('ssr-bo-gallery').waitFor();
    await page.getByPlaceholder('Search BOs...').fill(eventName);
    await page.locator('.cda-vef-gallery-card').filter({ hasText: eventName }).first().waitFor();
  }

  async function createTicket(eventObjectId) {
    await page.goto(`${aliceUrl}?view=ticket`);
    await importIfLocked(aliceBackupJson(), backupPassword, aliceVaultPassword);
    await page.getByTestId('bo-create-wizard').waitFor();
    await page.getByRole('button', { name: /^Next$/i }).click();
    await page.getByLabel('BusinessObject name').fill(ticketName);
    await page.getByLabel('BusinessObject tags').fill('#ticket #playwright #FreeToUse');
    await page.getByLabel('BusinessObject description').fill('Playwright-created ticket linked to an event.');
    await page.getByLabel('BusinessObject text').fill('This ticket is used to validate signed ownership and used-history checks.');
    await page.getByLabel('Ticket number').fill(`T-${runId}`);
    await page.getByLabel('Event BO outpoint').fill(eventObjectId);
    await page.getByLabel('Event owner address').fill(aliceAddress);
    await page.getByLabel('Event date').fill('2026-07-01T18:00:00Z');
    await page.getByLabel('Location', { exact: true }).fill('Prague');
    await page.getByLabel('Location coordinates', { exact: true }).fill('50.087,14.421');
    await page.getByLabel('Ticket class').fill('VIP');
    await page.getByLabel('Ticket duration').fill('Day');
    await page.getByLabel('Holder').fill('Alice');
    await page.getByLabel('Seat or scope').fill('A1');
    await page.getByLabel('Used', { exact: true }).fill('false');
    await page.getByLabel('Status', { exact: true }).fill('FreeToUse');
    await page.getByRole('button', { name: /^Next$/i }).click();
    await page.getByRole('button', { name: /^Stage BO Transaction$/i }).click();
    await page.getByTestId('ssr-bo-gallery').waitFor();
    await page.getByPlaceholder('Search BOs...').fill(ticketName);
    await page.locator('.cda-vef-gallery-card').filter({ hasText: ticketName }).first().waitFor();
  }

  async function openTicketDetails(name) {
    await page.goto(`${aliceUrl}?view=gallery`);
    await importIfLocked(aliceBackupJson(), backupPassword, aliceVaultPassword);
    await page.getByTestId('ssr-bo-gallery').waitFor();
    await page.getByPlaceholder('Search BOs...').fill(name);
    const card = page.locator('.cda-vef-gallery-card').filter({ hasText: name }).first();
    await card.waitFor();
    await card.locator('button[aria-label="Open BO details"]').click();
    await page.getByTestId('cda-bo-details-dialog').waitFor();
  }

  async function latestHolding(objectType, name, ownerAddress) {
    return await page.evaluate(async args => {
      const response = await fetch('/api/v1/ledger/default/business-objects/query', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ownerAddress: args.ownerAddress,
          objectType: args.objectType,
          skip: 0,
          take: 100
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
            ownerAddress: holding.ownerAddress?.value ?? holding.ownerAddress,
            version: holding.version,
            name: payload.name,
            state: payload,
            updatedAtUtc: holding.updatedAtUtc ?? holding.createdAtUtc ?? ''
          };
        })
        .filter(holding => holding.name === args.name)
        .sort((a, b) => String(b.updatedAtUtc).localeCompare(String(a.updatedAtUtc)))[0] ?? null;
    }, { objectType, name, ownerAddress });
  }

  async function waitForTicketUsed(ticketObjectId) {
    await page.waitForFunction(async id => {
      const response = await fetch('/api/v1/ledger/default/business-objects/query', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ objectId: id, skip: 0, take: 1 })
      });
      if (!response.ok) {
        return false;
      }

      const holding = (await response.json())[0];
      if (!holding?.stateJson) {
        return false;
      }

      const payload = JSON.parse(holding.stateJson);
      return String(payload.fields?.used).toLowerCase() === 'true';
    }, ticketObjectId, { timeout: 120000 });
  }

  function aliceBackupJson() {
    return String.raw`{
  "format": "candoitall-economy-accountapp-vault-backup-v1",
  "exportedAtUtc": "2026-06-29T22:32:07.6130809+00:00",
  "record": {
    "vaultId": "accountapp-demo-alice-source",
    "displayName": "Alice demo account",
    "keyId": "compose-demo-alice-b8082499ad8e49e588a7ade91533c821",
    "address": { "value": "cdia1Ngb3miLzYorvFSfKzLFwYPvZH6Kw6LPsSu" },
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
  }

  function bobBackupJson() {
    return String.raw`{
  "format": "candoitall-economy-accountapp-vault-backup-v1",
  "exportedAtUtc": "2026-06-29T22:32:08.0610606+00:00",
  "record": {
    "vaultId": "accountapp-demo-bob-source",
    "displayName": "Bob demo account",
    "keyId": "compose-demo-bob-918c8c21f3624fd08eb1d8db14ed6ec0",
    "address": { "value": "cdia1NTXHdDX3kHtpHwtEFyCFuHPNkqBiV8k4uU" },
    "algorithm": 1,
    "publicKeyHash": "5376d4cba4967d0325bc702414a19a9ca82c6ee30fdf75314415487eabe23333",
    "publicKeyMaterialBase64": "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEwZUkVAgWsEBeZsjIcjwKqDMiet\u002BJYRL6DYbEdMallslXESVkbDql8Jia0e4WyRLM/RHYdT6Wk1uPpGNvvftJMg==",
    "encryptedPrivateKeyBase64": "PIv6tMTT1Q5hYu9aZhp9HpJ\u002BGn2zsYQZ8mSUJbwx/96wTrSQTKx74SI3O4j\u002BYcTKriJjys324UlXizt71384kPXZRdgd5TcC\u002BtVhpOwuoat\u002B069o7c/fipRUjEv6pfCL7U8ord8rI04BFcaLCYSxwillm9sVl3UK4c7rZBlFs9awfu38TbHNZRlzXemHl4k9oZF7DPAAxOjJfg==",
    "kdfSaltBase64": "brwKX4rq\u002BwmQ35CqL8v5kQ==",
    "nonceBase64": "vN/4M/qzYzqxyl/o",
    "kdfIterations": 150000,
    "kdfAlgorithm": "PBKDF2-SHA256",
    "encryptionAlgorithm": "AES-256-GCM",
    "createdAtUtc": "2026-06-29T22:32:08.061012+00:00",
    "updatedAtUtc": "2026-06-29T22:32:08.061012+00:00"
  }
}`;
  }
}
