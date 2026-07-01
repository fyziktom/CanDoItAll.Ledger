async page => {
  const expectedText = 'testing of the right click menu in candoitall web app';

  await page.setViewportSize({ width: 1600, height: 900 });
  await page.goto('http://localhost:5081/', { waitUntil: 'networkidle' });

  if (await page.locator('[data-testid="adapter-workspace-host"]').count() === 0) {
    await page.getByTestId('unlock-password').fill('refresh-proof-password');
    await page.getByTestId('unlock-account').click();
  }

  await page.getByTestId('adapter-workspace-host').waitFor({ timeout: 90000 });
  await page.getByRole('button', { name: /^Gallery$/ }).click();
  await page.getByLabel('Search gallery').fill('new-ledger-post');
  await page.waitForFunction(() => {
    const gallery = document.querySelector('[data-testid="ssr-bo-gallery"]');
    return gallery && gallery.textContent && gallery.textContent.includes('New ledger post');
  }, undefined, { timeout: 30000 });

  await page.evaluate(() => {
    const cards = Array.from(document.querySelectorAll('.cda-vef-gallery-card'));
    const card = cards.find(candidate => candidate.textContent && candidate.textContent.includes('New ledger post'));
    const button = card ? card.querySelector('button[aria-label="Open BO details"]') : null;
    if (!(button instanceof HTMLElement)) {
      throw new Error('Could not locate New ledger post details button.');
    }

    button.click();
  });

  await page.getByTestId('cda-bo-details-layout').waitFor({ timeout: 30000 });
  const overviewText = await page.getByTestId('cda-bo-long-description').innerText();
  if (!overviewText.includes(expectedText)) {
    throw new Error('Overview long description missing expected body text.');
  }

  await page.screenshot({
    path: 'bundles/bo-commerce-flow/proof/SB01/browser/long-description-details.png',
    fullPage: true
  });

  await page.getByText('Ledger details', { exact: true }).click();
  await page.locator('.cda-vef-bo-state-json').waitFor({ timeout: 30000 });
  const rawState = await page.locator('.cda-vef-bo-state-json').innerText();
  if (!rawState.includes('"text"') || !rawState.includes(expectedText)) {
    throw new Error('Raw state missing serialized text property or expected body text.');
  }

  await page.screenshot({
    path: 'bundles/bo-commerce-flow/proof/SB01/browser/long-description-raw.png',
    fullPage: true
  });

  return {
    route: page.url(),
    viewport: await page.viewportSize(),
    object: 'bo:post:new-ledger-post',
    overviewContainsLongText: overviewText.includes(expectedText),
    rawStateContainsTextProperty: rawState.includes('"text"'),
    rawStateContainsLongText: rawState.includes(expectedText),
    screenshots: [
      'bundles/bo-commerce-flow/proof/SB01/browser/long-description-details.png',
      'bundles/bo-commerce-flow/proof/SB01/browser/long-description-raw.png'
    ]
  };
}
