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

  const before = await readStats();
  const marker = `playwright-auto-refresh-transfer-${Date.now()}`;
  const response = await page.context().request.post('http://localhost:8080/api/v1/transactions/transfer', {
    data: {
      fromAddress: 'cdia1NTXHdDX3kHtpHwtEFyCFuHPNkqBiV8k4uU',
      toAddress: 'cdia1Ngb3miLzYorvFSfKzLFwYPvZH6Kw6LPsSu',
      mainAmount: 1,
      tokenId: null,
      tokenAmount: null,
      dataItemType: 'proof',
      dataJson: '{"source":"playwright-auto-refresh"}',
      externalRef: marker,
      businessObjectId: null,
      scenarioId: null
    }
  });

  if (!response.ok()) {
    throw new Error(`External transfer failed: ${response.status()} ${await response.text()}`);
  }

  const receipt = await response.json();

  await page.waitForFunction(
    expected => {
      const findStat = label => {
        const nodes = Array.from(document.querySelectorAll('*'));
        const labelNode = nodes.find(node => node.textContent && node.textContent.trim() === label);
        const valueNode = labelNode?.parentElement?.querySelector('strong');
        const value = valueNode?.textContent?.trim();
        return value ? Number(value.replace(/[^0-9.-]/g, '')) : null;
      };

      const main = findStat('Main');
      const txs = findStat('Txs');
      return main !== null
        && txs !== null
        && main >= expected.main + 1
        && txs >= expected.txs + 1;
    },
    before,
    { timeout: 70000, polling: 1000 });

  const after = await readStats();
  await page.screenshot({
    path: 'bundles/2026-06-30-accountapp-refresh-cache-ui-polish/proof/SB03/accountapp-auto-refresh-after-external-mint.png',
    fullPage: true
  });

  return {
    before,
    receipt,
    after,
    marker
  };
}
