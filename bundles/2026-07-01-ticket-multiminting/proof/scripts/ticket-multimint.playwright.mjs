import fs from 'node:fs/promises';
import path from 'node:path';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');

const repoRoot = 'C:/repositories/CanDoItAll.Ledger';
const proofRoot = path.join(repoRoot, 'bundles/2026-07-01-ticket-multiminting/proof');
const screenshotRoot = path.join(proofRoot, 'screenshots');
const transcriptPath = path.join(proofRoot, 'results/ticket-multimint-result.json');
const aliceUrl = 'http://localhost:5081/';
const backupPath = path.join(repoRoot, 'docker/demo-keys/accountapp-demo/imports/alice-accountapp-backup.json');
const backupPassword = 'accountapp-demo-password';
const aliceVaultPassword = `ticket-multimint-proof-alice-${Date.now()}`;
const aliceAddress = 'cdia1Ngb3miLzYorvFSfKzLFwYPvZH6Kw6LPsSu';
const bobAddress = 'cdia1NTXHdDX3kHtpHwtEFyCFuHPNkqBiV8k4uU';
const runId = Date.now();
const eventName = `Multimint Event ${runId}`;
const ticketName = `Multimint Ticket ${runId}`;
const ticketCount = 5;

await fs.mkdir(screenshotRoot, { recursive: true });
await fs.mkdir(path.dirname(transcriptPath), { recursive: true });

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ viewport: { width: 1700, height: 1000 } });
const page = await context.newPage();
page.setDefaultTimeout(180000);

try {
  const backupJson = await fs.readFile(backupPath, 'utf8');

  await page.goto(aliceUrl);
  await page.getByTestId('account-app-root').waitFor();
  await importIfLocked(backupJson, backupPassword, aliceVaultPassword);
  await page.getByTestId('adapter-workspace-host').waitFor();

  await createEvent(backupJson);
  const eventHolding = await waitForHolding('event', eventName, aliceAddress, holding => holding.objectId && holding.txId);
  if (!eventHolding?.objectId || !eventHolding?.txId) {
    throw new Error(`Created event ${eventName} was not found with object id and tx id.`);
  }

  await createTicketBatch(backupJson, eventHolding);
  const ticketHoldings = await waitForTicketBatch();
  const ticketNumbers = ticketHoldings
    .map(holding => String(holding.state?.fields?.ticketNumber ?? ''))
    .sort();
  const expectedTicketNumbers = Array.from({ length: ticketCount }, (_, index) => `MM-${runId}-${String(index + 1).padStart(3, '0')}`);
  assertArrayEqual(ticketNumbers, expectedTicketNumbers, 'ticket numbers');

  const objectIds = ticketHoldings.map(holding => holding.objectId);
  if (new Set(objectIds).size !== ticketCount) {
    throw new Error(`Expected ${ticketCount} distinct ticket object ids, got ${objectIds.join(', ')}.`);
  }

  for (const holding of ticketHoldings) {
    assertEqual(holding.ownerAddress, bobAddress, 'ticket owner');
    assertEqual(holding.state?.fields?.eventId, eventHolding.objectId, 'ticket eventId');
    assertEqual(holding.state?.fields?.eventAddress, aliceAddress, 'ticket eventAddress');
    assertEqual(holding.state?.fields?.eventDate, '2026-10-15T20:00:00Z', 'ticket eventDate');
    assertEqual(holding.state?.fields?.location, 'Ledger Arena', 'ticket location');
    assertEqual(holding.state?.fields?.locationCoordinates, '49.195,16.608', 'ticket locationCoordinates');
    assertEqual(String(holding.state?.fields?.used).toLowerCase(), 'false', 'ticket used flag');
    assertEqual(String(holding.state?.fields?.batchSize), String(ticketCount), 'ticket batchSize');
  }

  await page.goto(`${aliceUrl}?view=gallery`);
  await importIfLocked(backupJson, backupPassword, aliceVaultPassword);
  await page.getByTestId('ssr-bo-gallery').waitFor();
  await page.screenshot({ path: path.join(screenshotRoot, 'ticket-multimint-gallery-after-submit.png'), fullPage: true });

  const result = {
    status: 'passed',
    runId,
    event: {
      name: eventName,
      objectId: eventHolding.objectId,
      txId: eventHolding.txId,
      ownerAddress: eventHolding.ownerAddress
    },
    ticketBatch: {
      namePrefix: ticketName,
      count: ticketHoldings.length,
      ownerAddress: bobAddress,
      objectIds,
      ticketNumbers
    },
    screenshots: [
      'ticket-multimint-event-picker-loaded.png',
      'ticket-multimint-setup.png',
      'ticket-multimint-gallery-after-submit.png'
    ]
  };

  await fs.writeFile(transcriptPath, JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result, null, 2));
} finally {
  await browser.close();
}

async function createEvent(backupJson) {
  await page.goto(`${aliceUrl}?view=event`);
  await importIfLocked(backupJson, backupPassword, aliceVaultPassword);
  await page.getByTestId('bo-create-wizard').waitFor();
  await page.getByRole('button', { name: /^Next$/i }).click();
  await page.getByLabel('BusinessObject name').fill(eventName);
  await page.getByLabel('BusinessObject tags').fill('#event #multimint #ticket');
  await page.getByLabel('BusinessObject description').fill('Playwright-created event used for ticket multimint validation.');
  await page.getByLabel('BusinessObject text').fill('This event is selected by transaction hash before a batch of tickets is minted.');
  await page.getByLabel('Event id').fill(`multimint-event-${runId}`);
  await page.getByLabel('Event date').fill('2026-10-15T20:00:00Z');
  await page.getByLabel('Event class').fill('Conference');
  await page.getByLabel('Location', { exact: true }).fill('Ledger Arena');
  await page.getByLabel('Location coordinates', { exact: true }).fill('49.195,16.608');
  await page.getByLabel('Organizer').fill('CanDoItAll');
  await page.getByLabel('Author website').fill('https://events.example/multimint');
  await page.getByLabel('Mint author address').fill(aliceAddress);
  await page.getByLabel('Status', { exact: true }).fill('Scheduled');
  await page.getByRole('button', { name: /^Next$/i }).click();
  await page.getByRole('button', { name: /^Stage BO Transaction$/i }).click();
  await page.getByTestId('ssr-bo-gallery').waitFor();
  await page.getByPlaceholder('Search BOs...').fill(eventName);
  await page.locator('.cda-vef-gallery-card').filter({ hasText: eventName }).first().waitFor();
}

async function createTicketBatch(backupJson, eventHolding) {
  await page.goto(`${aliceUrl}?view=ticket`);
  await importIfLocked(backupJson, backupPassword, aliceVaultPassword);
  await page.getByTestId('bo-create-wizard').waitFor();
  await page.getByRole('button', { name: /^Next$/i }).click();
  await page.getByLabel('BusinessObject name').fill(ticketName);
  await page.getByLabel('BusinessObject tags').fill('#ticket #multimint #FreeToUse');
  await page.getByLabel('BusinessObject description').fill('Ticket batch created through AccountApp multimint.');
  await page.getByLabel('BusinessObject text').fill('Each generated ticket must keep the event fields and a unique object id.');

  await page.getByTestId('cda-ticket-select-event').click();
  await page.getByTestId('cda-event-picker-dialog').waitFor();
  await page.getByTestId('cda-event-picker-content').waitFor();
  await page.getByLabel('Search available events').fill(eventName);
  await page.locator('[data-testid="cda-event-picker-event"]').filter({ hasText: eventName }).first().waitFor();
  await page.getByLabel('Event BusinessObject id or transaction hash').fill(eventHolding.txId);
  await page.getByTestId('cda-event-lookup-button').click();
  await page.locator('[data-testid="cda-event-picker-selected"]').filter({ hasText: eventName }).waitFor();
  await page.screenshot({ path: path.join(screenshotRoot, 'ticket-multimint-event-picker-loaded.png'), fullPage: true });
  await page.getByTestId('cda-event-picker-use').click();
  await page.getByTestId('cda-event-picker-dialog').waitFor({ state: 'detached' });

  assertEqual(await page.getByLabel('Event BO outpoint').inputValue(), eventHolding.objectId, 'prefilled event id field');
  assertEqual(await page.getByLabel('Event owner address').inputValue(), aliceAddress, 'prefilled event owner field');
  assertEqual(await page.getByLabel('Event date').inputValue(), '2026-10-15T20:00:00Z', 'prefilled event date field');
  assertEqual(await page.getByLabel('Location', { exact: true }).inputValue(), 'Ledger Arena', 'prefilled location field');
  assertEqual(await page.getByLabel('Location coordinates', { exact: true }).inputValue(), '49.195,16.608', 'prefilled coordinates field');

  await page.getByLabel('Ticket number').fill(`MM-${runId}`);
  await page.getByLabel('Ticket class').fill('VIP');
  await page.getByLabel('Ticket duration').fill('Evening');
  await page.getByLabel('Holder').fill('Bob batch recipient');
  await page.getByLabel('Seat or scope').fill('Batch row');
  await page.getByTestId('cda-ticket-multimint-enabled').check();
  await page.getByTestId('cda-ticket-multimint-count').fill(String(ticketCount));
  await page.getByTestId('cda-ticket-multimint-recipient-mode').selectOption('manual');
  await page.getByTestId('cda-ticket-multimint-recipient-list').fill(bobAddress);
  await page.locator('[data-testid="cda-ticket-multimint-summary"]').filter({ hasText: `${ticketCount} ticket` }).waitFor();
  await page.screenshot({ path: path.join(screenshotRoot, 'ticket-multimint-setup.png'), fullPage: true });
  await page.getByRole('button', { name: /^Next$/i }).click();
  await page.getByRole('button', { name: /^Stage BO Transaction$/i }).click();
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

async function waitForHolding(objectType, name, ownerAddress, predicate) {
  const deadline = Date.now() + 180000;
  let latest = null;
  while (Date.now() < deadline) {
    latest = await latestHolding(objectType, name, ownerAddress);
    if (latest && (!predicate || predicate(latest))) {
      return latest;
    }

    await page.waitForTimeout(2000);
  }

  return latest;
}

async function waitForTicketBatch() {
  const deadline = Date.now() + 240000;
  let latest = [];
  while (Date.now() < deadline) {
    latest = await holdingsByNamePrefix('ticket', `${ticketName} #`, bobAddress);
    if (latest.length >= ticketCount) {
      return latest.slice(0, ticketCount);
    }

    await page.waitForTimeout(2500);
  }

  throw new Error(`Expected ${ticketCount} Bob-owned tickets for ${ticketName}, got ${latest.length}.`);
}

async function latestHolding(objectType, name, ownerAddress) {
  const holdings = await holdingsByQuery({ objectType, ownerAddress, take: 100 });
  return holdings
    .filter(holding => holding.name === name)
    .sort((a, b) => String(b.updatedAtUtc).localeCompare(String(a.updatedAtUtc)))[0] ?? null;
}

async function holdingsByNamePrefix(objectType, namePrefix, ownerAddress) {
  const holdings = await holdingsByQuery({ objectType, ownerAddress, take: 100 });
  return holdings
    .filter(holding => holding.name?.startsWith(namePrefix))
    .sort((a, b) => String(a.name).localeCompare(String(b.name)));
}

async function holdingsByQuery(query) {
  return await page.evaluate(async args => {
    const response = await fetch('/api/v1/ledger/default/business-objects/query', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        ownerAddress: args.ownerAddress,
        objectType: args.objectType,
        skip: 0,
        take: args.take ?? 100
      })
    });
    if (!response.ok) {
      throw new Error(`${response.status} ${response.statusText}`);
    }

    return (await response.json()).map(holding => {
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
    });
  }, query);
}

function assertEqual(actual, expected, label) {
  if (actual !== expected) {
    throw new Error(`${label}: expected '${expected}', got '${actual}'.`);
  }
}

function assertArrayEqual(actual, expected, label) {
  if (actual.length !== expected.length || actual.some((value, index) => value !== expected[index])) {
    throw new Error(`${label}: expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}.`);
  }
}
