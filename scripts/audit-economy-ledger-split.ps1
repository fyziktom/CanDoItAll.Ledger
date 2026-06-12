param(
    [string]$EconomyRoot,
    [string]$LedgerRoot,
    [string]$ReportDirectory
)

$ErrorActionPreference = "Stop"

$thisRepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$repoParent = Split-Path -Parent $thisRepoRoot

if ([string]::IsNullOrWhiteSpace($EconomyRoot)) {
    $EconomyRoot = Join-Path $repoParent "CanDoItAll.Economy"
}

if ([string]::IsNullOrWhiteSpace($LedgerRoot)) {
    $LedgerRoot = Join-Path $repoParent "CanDoItAll.Ledger"
}

$EconomyRoot = (Resolve-Path -LiteralPath $EconomyRoot).Path
$LedgerRoot = (Resolve-Path -LiteralPath $LedgerRoot).Path

if ([string]::IsNullOrWhiteSpace($ReportDirectory)) {
    $ReportDirectory = Join-Path $LedgerRoot "artifacts/ledger/boundary-audits"
}

if (-not [System.IO.Path]::IsPathRooted($ReportDirectory)) {
    $ReportDirectory = Join-Path $LedgerRoot $ReportDirectory
}

New-Item -ItemType Directory -Path $ReportDirectory -Force | Out-Null

$economyAudit = Join-Path $EconomyRoot "scripts/audit-basic-simulator-no-node-ledger.ps1"
$ledgerAudit = Join-Path $LedgerRoot "scripts/audit-ledger-repo-does-not-contain-basic-simulator.ps1"

if (-not (Test-Path -LiteralPath $economyAudit -PathType Leaf)) {
    throw "Missing Economy audit script: $economyAudit"
}

if (-not (Test-Path -LiteralPath $ledgerAudit -PathType Leaf)) {
    throw "Missing Ledger audit script: $ledgerAudit"
}

& $economyAudit `
    -RepositoryRoot $EconomyRoot `
    -RunNegativeFixture `
    -ReportPath (Join-Path $ReportDirectory "economy-basic-simulator-boundary.md")
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $ledgerAudit `
    -RepositoryRoot $LedgerRoot `
    -RunNegativeFixture `
    -ReportPath (Join-Path $ReportDirectory "ledger-no-basic-simulator-boundary.md")
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$summaryPath = Join-Path $ReportDirectory "two-repo-boundary-summary.md"
$summary = @(
    "# Two-Repo Economy/Ledger Boundary Audit",
    "",
    "- Result: PASS",
    "- Economy root: $EconomyRoot",
    "- Ledger root: $LedgerRoot",
    "- Economy report: economy-basic-simulator-boundary.md",
    "- Ledger report: ledger-no-basic-simulator-boundary.md",
    "",
    "The Economy audit proves the basic simulator repo has no moved Node/Ledger/BusinessObjects/SDK ownership references in active product surface.",
    "The Ledger audit proves the ledger/node repo has no Economy basic simulator, scenario-pack, SimpleAccounts, ExperimentalEconomics, MemoryEconomy, SimulationSandbox, or simulator-host dependencies outside documented quarantine and boundary docs."
)
Set-Content -LiteralPath $summaryPath -Value $summary -Encoding UTF8

Write-Host "Two-repo Economy/Ledger boundary audit passed."
Write-Host "Report directory: $ReportDirectory"
