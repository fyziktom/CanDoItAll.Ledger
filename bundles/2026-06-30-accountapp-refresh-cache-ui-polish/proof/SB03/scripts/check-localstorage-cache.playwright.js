async page => {
  await page.evaluate(() => localStorage.clear());
  const beforeKeys = await page.evaluate(() =>
    Object.keys(localStorage)
      .filter(key => key.startsWith('candoitall:economy:account-workspace-cache:v1:')));

  await page.getByTestId('load-transaction-detail').click();
  await page.getByTestId('transaction-detail').waitFor({ timeout: 60000 });

  const afterKeys = await page.evaluate(() =>
    Object.keys(localStorage)
      .filter(key => key.startsWith('candoitall:economy:account-workspace-cache:v1:')));

  return {
    beforeCacheKeys: beforeKeys.length,
    afterCacheKeys: afterKeys.length,
    sampleKey: afterKeys[0] ?? null,
    detailVisible: await page.getByTestId('transaction-detail').isVisible()
  };
}
