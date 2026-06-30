async page => {
  const dialog = page.getByRole('dialog', { name: 'Send Transaction' });
  await dialog.getByRole('button', { name: 'Submit transaction' }).click();

  const accepted = page.getByText(/accepted at ledger sequence/i).first();
  await accepted.waitFor({ timeout: 60000 });
  await page.screenshot({
    path: 'bundles/2026-06-30-accountapp-refresh-cache-ui-polish/proof/SB03/accountapp-send-notification-popup.png',
    fullPage: true
  });

  const immediateMatches = await page.getByText(/accepted at ledger sequence/i).count();
  const inlineRefreshAlerts = await page.locator('[data-testid="venft-refresh-alert"]').count();
  const inlineReceiptLike = await page.locator('main').getByText(/accepted at ledger sequence/i).count();

  await page.waitForTimeout(9000);
  const afterDismissMatches = await page.getByText(/accepted at ledger sequence/i).count();

  return {
    immediateMatches,
    inlineRefreshAlerts,
    inlineReceiptLike,
    afterDismissMatches
  };
}
