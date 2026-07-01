async page => {
  const screenshotRoot = 'C:/repositories/CanDoItAll.Ledger/bundles/2026-07-01-bo-create-identity-uniqueness/proof/SB03/screenshots';
  const demoUrl = 'http://localhost:56426/';
  const nodeQueryUrl = 'http://localhost:8080/api/v1/ledger/default/business-objects/query';
  const aliceAddress = 'cdia1Ngb3miLzYorvFSfKzLFwYPvZH6Kw6LPsSu';
  const createdIds = [];

  page.setDefaultTimeout(120000);
  await page.setViewportSize({ width: 1600, height: 950 });
  await page.goto(demoUrl);
  await page.getByTestId('ssr-vef-home').waitFor();
  await page.getByText('Real node http://localhost:8080/').waitFor();

  for (let i = 0; i < 2; i += 1) {
    createdIds.push(await submitDefaultMessageDraft());
    await page.waitForTimeout(1500);
  }

  if (new Set(createdIds).size !== createdIds.length) {
    throw new Error(`SSR default drafts reused object ids: ${createdIds.join(', ')}`);
  }

  for (const id of createdIds) {
    if (!/^bo:ui-message:message-from-alice:[0-9a-f]+-[0-9a-f]{4}-[0-9a-f]{12}$/.test(id)) {
      throw new Error(`SSR default draft id has unexpected shape: ${id}`);
    }
  }

  const projected = await waitForProjectedObjects(createdIds);
  await page.screenshot({
    path: `${screenshotRoot}/ssr-default-draft-unique-messages.png`,
    fullPage: true
  });

  const bodyText = await page.locator('body').innerText();
  if (/already exists|Create must not overwrite/i.test(bodyText)) {
    throw new Error(`SSR create surfaced a duplicate transition error: ${bodyText.slice(0, 1000)}`);
  }

  return {
    createdIds,
    projectedCount: projected.length,
    screenshot: 'ssr-default-draft-unique-messages.png'
  };

  async function submitDefaultMessageDraft() {
    await page.locator('button').filter({ hasText: 'Send' }).first().click();
    const dialog = page.getByTestId('cda-send-transaction-dialog');
    await dialog.waitFor();
    const objectId = await page.getByLabel('Create object id').inputValue();
    const externalRef = await page.getByLabel('External reference').inputValue();
    if (!externalRef.endsWith(objectId)) {
      throw new Error(`External reference ${externalRef} does not include full object id ${objectId}`);
    }

    await dialog.getByRole('button', { name: /^Submit transaction$/i }).click();
    await dialog.waitFor({ state: 'detached' });
    await page.getByText(/Transaction submitted/i).first().waitFor();
    return objectId;
  }

  async function waitForProjectedObjects(ids) {
    for (let attempt = 0; attempt < 90; attempt += 1) {
      const items = await queryMessages();
      const matches = items.filter(item => ids.includes(item.objectId));
      if (matches.length === ids.length) {
        return matches;
      }

      await page.getByRole('button', { name: /^Refresh$/i }).click();
      await page.waitForTimeout(1000);
    }

    const items = await queryMessages();
    throw new Error(`Timed out waiting for SSR message BO projections. Expected ${ids.join(', ')}; saw ${items.map(item => item.objectId).join(', ')}`);
  }

  async function queryMessages() {
    const response = await page.request.post(nodeQueryUrl, {
      data: {
        ownerAddress: aliceAddress,
        objectType: 'message',
        skip: 0,
        take: 200
      }
    });
    if (!response.ok) {
      throw new Error(`Node query failed: ${response.status()} ${response.statusText()}`);
    }

    return (await response.json()).map(holding => ({
      objectId: holding.objectId?.value ?? holding.objectId,
      txId: holding.currentOutPoint?.txId?.value ?? holding.currentOutPoint?.txId
    }));
  }
}
