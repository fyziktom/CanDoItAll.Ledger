async page => {
  const objectId = 'bo:post:new-ledger-post';
  const screenshotRoot = 'C:/repositories/CanDoItAll.Ledger/bundles/bo-commerce-flow/proof/SB03/browser';

  await page.setViewportSize({ width: 1600, height: 950 });
  page.setDefaultTimeout(300000);

  await page.getByTestId('adapter-workspace-host').waitFor();
  await page.getByRole('button', { name: /^Home$/i }).first().click();
  await page.getByTestId('refresh-workspace').click();
  await page.getByTestId('cda-commerce-dashboard').waitFor();

  const dashboard = page.getByTestId('cda-commerce-dashboard');
  await dashboard.scrollIntoViewIfNeeded();
  const row = dashboard.getByTestId('cda-commerce-record')
    .filter({ hasText: objectId })
    .first();
  await row.waitFor();
  await page.screenshot({
    path: `${screenshotRoot}/alice-commerce-dashboard-payment.png`,
    fullPage: true
  });

  const rowText = await row.textContent();
  await row.click();
  await page.getByTestId('cda-commerce-details-dialog').waitFor();
  await page.screenshot({
    path: `${screenshotRoot}/alice-commerce-payment-detail.png`,
    fullPage: true
  });

  return {
    objectId,
    rowText,
    screenshots: ['alice-commerce-dashboard-payment.png', 'alice-commerce-payment-detail.png']
  };
}
