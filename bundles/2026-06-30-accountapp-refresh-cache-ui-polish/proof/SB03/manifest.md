# SB03 Proof Manifest

## Scope

End-to-end docker and Playwright validation.

## Command Evidence

- Command transcript path: bundle://proof/SB03/transcripts/00-manifest-summary.txt
- Passing transcript: bundle://proof/SB03/transcripts/00-manifest-summary.txt
- Anti-stub audit: bundle://proof/SB03/transcripts/00-manifest-summary.txt
- Failing-first: N/A process/non-production exemption; this was an enhancement validation bundle, with user-reported browser behavior and source analysis as baseline evidence.
- Semantic invariant contract: bundle://proof/SB03/semantic-invariants.md

## Changed File Hashes

| File | SHA-256 |
|---|---|
| repo://bundles/2026-06-30-accountapp-refresh-cache-ui-polish/proof/SB03/scripts/import-alice.playwright.js | 86A4B33E5CFFC0676E75B5910988692BD7A0D325704B84E9584C3CFC84D0F4CF |
| repo://bundles/2026-06-30-accountapp-refresh-cache-ui-polish/proof/SB03/scripts/auto-refresh-external-mint.playwright.js | 811EFED18EA5CC0718502CB2218F5C66E6715BC7176FD2880199773EDBC39FDD |
| repo://bundles/2026-06-30-accountapp-refresh-cache-ui-polish/proof/SB03/scripts/submit-send-notification.playwright.js | 8B8314105608F694DE9527CC4964D76A7DBE27EF4D7369950DE3A5E7DCA84ADC |
| repo://bundles/2026-06-30-accountapp-refresh-cache-ui-polish/proof/SB03/scripts/wait-original-auto-refresh.playwright.js | 6A7A2BD22ACB163B0C8AF49E8B00282904108BCE01C41A1BDA7EF1015C06C061 |
| repo://bundles/2026-06-30-accountapp-refresh-cache-ui-polish/proof/SB03/scripts/check-localstorage-cache.playwright.js | CD2A4A3AB6F12DE79E3C1FDE8F513DD0E361CF502D1CEE6D5BF9C2266CC8B04F |

## Browser Evidence

- Full-width visual proof: bundle://proof/SB03/accountapp-full-width.png
- Notification visual proof: bundle://proof/SB03/accountapp-send-notification-popup.png
- Auto-refresh visual proof: bundle://proof/SB03/accountapp-auto-refresh-after-external-mint.png

## Notes

The API helper mint/treasury transfer paths were not used for final proof because the long-lived demo database had consumed authority/spendable helper inputs. The successful external event was a real signed browser transaction from a second nginx-hosted AccountApp instance.
