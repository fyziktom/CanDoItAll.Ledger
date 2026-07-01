async page => {
  const pages = page.context().pages();
  const info = [];
  for (const p of pages) {
    info.push({
      url: p.url(),
      title: await p.title().catch(() => ''),
      buyPage: await p.getByTestId('buy-bo-page').count().catch(() => -1),
      buySubmit: await p.getByTestId('buy-bo-submit').count().catch(() => -1),
      buyUnlock: await p.getByTestId('buy-unlock-account').count().catch(() => -1),
      buyUnlockDisabled: await p.getByTestId('buy-unlock-account').isDisabled().catch(() => null),
      buyImportJson: await p.getByTestId('buy-backup-import-json').count().catch(() => -1),
      accountRoot: await p.getByTestId('account-app-root').count().catch(() => -1),
      workspace: await p.getByTestId('adapter-workspace-host').count().catch(() => -1),
      errorText: await p.locator('[data-testid="account-error"], [data-testid="buy-bo-error"]').textContent().catch(() => null)
    });
  }
  return info;
}
