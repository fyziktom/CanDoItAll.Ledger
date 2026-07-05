# SB01 Proof Manifest

## Summary

- Subbundle: `SB01`
- Status: `Completed`
- Owned requirements: R01, R02, R03
- Owned raw notes: N001, N002
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed Files

| File | Before behavior proof | After SHA-256 |
| --- | --- | --- |
| `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor` | `bundle://proof/SB01/transcripts/failing-first-token-source-filter.txt` | `B8B9F9F49584FC238C113D8A2310B4225AC1A87C5ACE8198430E7550366C8CFA` |
| `repo://src/CanDoItAll.Economy.Components/wwwroot/css/economy-components.css` | `bundle://proof/SB01/transcripts/failing-first-token-source-filter.txt` | `A84827DEC9BF0052FFD917B2FF7E58171F534DBC2771E5832562D1376E313191` |
| `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectOwnershipPlacementComponentTests.cs` | `bundle://proof/SB01/transcripts/failing-first-token-source-filter.txt` | `F6290EF81523510800C3F12C57D0A2038D572709B0FF77D933538D541ADBCCA2` |

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-token-source-filter.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/passing-component-tests.txt`
- Source assertions transcript: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Changed-file hashes transcript: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`

## Semantic Adequacy Evidence

- Raw note owned: N001 and N002.
- Shipped behavior: the Balances panel owns real token-source rows, and `IsTokenSourceOutput` requires no BO, a token, and token amount greater than `1`.
- Source proof: `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor` and `bundle://proof/SB01/transcripts/source-assertions.txt`.
- Test proof: `bundle://proof/SB01/transcripts/passing-component-tests.txt`.
- Test name: `CanDoItAll.Economy.Tests.BusinessObjectOwnershipPlacementComponentTests.HomeBalances_ShowOnlyRealTokenSourcesInsideBalancesCard`
- Failing-first proof: `bundle://proof/SB01/transcripts/failing-first-token-source-filter.txt`.
- Adversarial negative proof: `bundle://proof/SB01/transcripts/failing-first-token-source-filter.txt` captured the old UI exposing the forbidden Token Sources surface before the predicate/card fix.
- Negative closure assertion: `bundle://proof/SB01/transcripts/passing-component-tests.txt` asserts unique one-amount and BO-backed source outpoints are absent.
- Semantic positive proof: `bundle://proof/SB01/transcripts/passing-component-tests.txt` asserts the real `tx-token-source` source remains present.
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`.
- Downstream smoke proof: `bundle://proof/SB02/transcripts/playwright-large-home.txt` and `bundle://proof/SB02/browser/accountapp-home-large.png`.

## Closure Decision

- SB01 progression gate: `Passed`.
- Downstream dependency: SB02 completed the compact-layout browser validation.
