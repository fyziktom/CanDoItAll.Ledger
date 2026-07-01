async page => {
  const screenshotRoot = 'C:/repositories/CanDoItAll.Ledger/bundles/2026-07-01-ticket-event-picker/proof/screenshots';
  const aliceUrl = 'http://localhost:5081/';
  const backupPassword = 'accountapp-demo-password';
  const aliceVaultPassword = 'ticket-picker-proof-alice';
  const runId = Date.now();
  const eventName = `Picker Event ${runId}`;
  const ticketName = `Picker Ticket ${runId}`;
  const aliceAddress = 'cdia1Ngb3miLzYorvFSfKzLFwYPvZH6Kw6LPsSu';

  page.setDefaultTimeout(120000);
  await page.setViewportSize({ width: 1700, height: 1000 });

  await page.goto(aliceUrl);
  await page.getByTestId('account-app-root').waitFor();
  await importIfLocked(aliceBackupJson(), backupPassword, aliceVaultPassword);
  await page.getByTestId('adapter-workspace-host').waitFor();

  await createEvent();
  const eventHolding = await latestHolding('event', eventName, aliceAddress);
  if (!eventHolding?.objectId || !eventHolding?.txId) {
    throw new Error(`Created event ${eventName} was not found with object id and tx id.`);
  }

  await createTicketWithPicker(eventHolding);
  const ticketHolding = await latestHolding('ticket', ticketName, aliceAddress);
  if (!ticketHolding?.objectId) {
    throw new Error(`Created ticket ${ticketName} was not found through the node API.`);
  }

  const fields = ticketHolding.state?.fields ?? {};
  assertEqual(fields.eventId, eventHolding.objectId, 'ticket eventId');
  assertEqual(fields.eventAddress, aliceAddress, 'ticket eventAddress');
  assertEqual(fields.eventDate, '2026-09-15T19:15:00Z', 'ticket eventDate');
  assertEqual(fields.location, 'Brno Expo Hall', 'ticket location');
  assertEqual(fields.locationCoordinates, '49.195,16.608', 'ticket locationCoordinates');
  assertEqual(fields.status, 'FreeToUse', 'ticket status');
  assertEqual(String(fields.used).toLowerCase(), 'false', 'ticket used');

  await page.screenshot({ path: `${screenshotRoot}/ticket-prefilled-and-created.png`, fullPage: true });

  return {
    eventObjectId: eventHolding.objectId,
    eventTxId: eventHolding.txId,
    ticketObjectId: ticketHolding.objectId,
    prefilledFieldsValidated: true,
    screenshot: 'ticket-prefilled-and-created.png'
  };

  async function createEvent() {
    await page.goto(`${aliceUrl}?view=event`);
    await importIfLocked(aliceBackupJson(), backupPassword, aliceVaultPassword);
    await page.getByTestId('bo-create-wizard').waitFor();
    await page.getByRole('button', { name: /^Next$/i }).click();
    await page.getByLabel('BusinessObject name').fill(eventName);
    await page.getByLabel('BusinessObject tags').fill('#event #picker #ticket');
    await page.getByLabel('BusinessObject description').fill('Playwright-created event for picker validation.');
    await page.getByLabel('BusinessObject text').fill('This event should be loaded by the ticket minting picker.');
    await page.getByLabel('Event id').fill(`picker-event-${runId}`);
    await page.getByLabel('Event date').fill('2026-09-15T19:15:00Z');
    await page.getByLabel('Event class').fill('Expo');
    await page.getByLabel('Location', { exact: true }).fill('Brno Expo Hall');
    await page.getByLabel('Location coordinates', { exact: true }).fill('49.195,16.608');
    await page.getByLabel('Organizer').fill('CanDoItAll');
    await page.getByLabel('Author website').fill('https://events.example/picker');
    await page.getByLabel('Mint author address').fill(aliceAddress);
    await page.getByLabel('Status', { exact: true }).fill('Scheduled');
    await page.getByRole('button', { name: /^Next$/i }).click();
    await page.getByRole('button', { name: /^Stage BO Transaction$/i }).click();
    await page.getByTestId('ssr-bo-gallery').waitFor();
    await page.getByPlaceholder('Search BOs...').fill(eventName);
    await page.locator('.cda-vef-gallery-card').filter({ hasText: eventName }).first().waitFor();
  }

  async function createTicketWithPicker(eventHolding) {
    await page.goto(`${aliceUrl}?view=ticket`);
    await importIfLocked(aliceBackupJson(), backupPassword, aliceVaultPassword);
    await page.getByTestId('bo-create-wizard').waitFor();
    await page.getByRole('button', { name: /^Next$/i }).click();
    await page.getByLabel('BusinessObject name').fill(ticketName);
    await page.getByLabel('BusinessObject tags').fill('#ticket #picker #FreeToUse');
    await page.getByLabel('BusinessObject description').fill('Ticket created with event picker prefill.');
    await page.getByLabel('BusinessObject text').fill('The event fields must be loaded from the selected event BO.');

    await page.getByTestId('cda-ticket-select-event').click();
    await page.getByTestId('cda-event-picker-dialog').waitFor();
    await page.getByTestId('cda-event-picker-content').waitFor();
    await page.getByLabel('Search available events').fill(eventName);
    await page.locator('[data-testid="cda-event-picker-event"]').filter({ hasText: eventName }).first().waitFor();
    await page.getByLabel('Event BusinessObject id or transaction hash').fill(eventHolding.txId);
    await page.getByTestId('cda-event-lookup-button').click();
    await page.locator('[data-testid="cda-event-picker-selected"]').filter({ hasText: eventName }).waitFor();
    await page.screenshot({ path: `${screenshotRoot}/ticket-event-picker-loaded-by-tx.png`, fullPage: true });
    await page.getByTestId('cda-event-picker-use').click();
    await page.getByTestId('cda-event-picker-dialog').waitFor({ state: 'detached' });

    assertEqual(await page.getByLabel('Event BO outpoint').inputValue(), eventHolding.objectId, 'prefilled event id field');
    assertEqual(await page.getByLabel('Event owner address').inputValue(), aliceAddress, 'prefilled event owner field');
    assertEqual(await page.getByLabel('Event date').inputValue(), '2026-09-15T19:15:00Z', 'prefilled event date field');
    assertEqual(await page.getByLabel('Location', { exact: true }).inputValue(), 'Brno Expo Hall', 'prefilled location field');
    assertEqual(await page.getByLabel('Location coordinates', { exact: true }).inputValue(), '49.195,16.608', 'prefilled coordinates field');
    assertEqual(await page.getByLabel('Status', { exact: true }).inputValue(), 'FreeToUse', 'prefilled status field');

    await page.getByLabel('Ticket number').fill(`PICK-${runId}`);
    await page.getByLabel('Ticket class').fill('VIP');
    await page.getByLabel('Ticket duration').fill('Evening');
    await page.getByLabel('Holder').fill('Alice');
    await page.getByLabel('Seat or scope').fill('B-12');
    await page.getByRole('button', { name: /^Next$/i }).click();
    await page.getByRole('button', { name: /^Stage BO Transaction$/i }).click();
    await page.getByTestId('ssr-bo-gallery').waitFor();
    await page.getByPlaceholder('Search BOs...').fill(ticketName);
    await page.locator('.cda-vef-gallery-card').filter({ hasText: ticketName }).first().waitFor();
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
          const currentOutPoint = holding.currentOutPoint ?? {};
          const txId = currentOutPoint.txId?.value ?? currentOutPoint.txId ?? null;
          return {
            objectId: holding.objectId?.value ?? holding.objectId,
            txId,
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

  function assertEqual(actual, expected, label) {
    if (actual !== expected) {
      throw new Error(`${label}: expected '${expected}', got '${actual}'.`);
    }
  }

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
}
