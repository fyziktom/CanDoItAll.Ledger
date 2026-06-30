async page => {
  const readStats = async () => page.evaluate(() => {
    const findStat = label => {
      const nodes = Array.from(document.querySelectorAll('*'));
      const labelNode = nodes.find(node => node.textContent && node.textContent.trim() === label);
      const valueNode = labelNode?.parentElement?.querySelector('strong');
      const value = valueNode?.textContent?.trim();
      return value ? Number(value.replace(/[^0-9.-]/g, '')) : null;
    };

    return {
      main: findStat('Main'),
      txs: findStat('Txs'),
      bos: findStat('BOs'),
      utxos: findStat('UTXOs')
    };
  });

  const beforeWait = await readStats();
  if ((beforeWait.txs ?? 0) < 9) {
    await page.waitForFunction(() => {
      const nodes = Array.from(document.querySelectorAll('*'));
      const labelNode = nodes.find(node => node.textContent && node.textContent.trim() === 'Txs');
      const valueNode = labelNode?.parentElement?.querySelector('strong');
      const txs = valueNode?.textContent ? Number(valueNode.textContent.replace(/[^0-9.-]/g, '')) : null;
      return txs !== null && txs >= 9;
    }, undefined, { timeout: 70000, polling: 1000 });
  }

  const afterWait = await readStats();
  await page.screenshot({
    path: 'bundles/2026-06-30-accountapp-refresh-cache-ui-polish/proof/SB03/accountapp-auto-refresh-after-external-mint.png',
    fullPage: true
  });

  return {
    beforeWait,
    afterWait,
    refreshed: (afterWait.txs ?? 0) >= 9
  };
}
