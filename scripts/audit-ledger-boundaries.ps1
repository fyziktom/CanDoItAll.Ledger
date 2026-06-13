param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$violations = New-Object System.Collections.Generic.List[string]

& (Join-Path $PSScriptRoot "audit-ledger-repo-does-not-contain-basic-simulator.ps1") `
    -RepositoryRoot $repoRoot `
    -RunNegativeFixture `
    -ReportPath (Join-Path $repoRoot "artifacts/ledger/boundary-audits/ledger-no-basic-simulator-boundary.md")
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$solution = Join-Path $repoRoot "CanDoItAll.Ledger.slnx"
if (-not (Test-Path -LiteralPath $solution -PathType Leaf)) {
    $violations.Add("missing Ledger solution: $solution")
}
else {
    $projects = @(dotnet sln $solution list | Select-Object -Skip 2 | Where-Object { $_.Trim() })
    foreach ($project in $projects) {
        $projectPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $project.Trim()))
        if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
            $violations.Add("solution project does not exist: $project")
            continue
        }

        $normalizedProjectPath = $projectPath.Replace("/", "\")
        $normalizedRepoRoot = $repoRoot.TrimEnd("\", "/") + "\"
        $isInLedgerRepo = $normalizedProjectPath.StartsWith($normalizedRepoRoot, [StringComparison]::OrdinalIgnoreCase)

        [xml]$xml = Get-Content -LiteralPath $projectPath -Raw
        $references = @($xml.Project.ItemGroup.ProjectReference.Include) | Where-Object { $_ }
        foreach ($reference in $references) {
            $normalizedReference = $reference.Replace("/", "\")
            if ($normalizedReference -match "\.\.\\CanDoItAll\.Economy\\") {
                $violations.Add("cross-repo Economy project reference: $project -> $reference")
            }
            if ($normalizedReference -match "Simulation\.SimpleAccounts|Simulation\.ExperimentalEconomics|Simulation\.MemoryEconomy|SimulationSandbox") {
                $violations.Add("basic simulator project reference: $project -> $reference")
            }
            if ($normalizedReference -match "Simulator" -and $normalizedReference -notmatch "CanDoItAll\.Ledger\.Simulator") {
                $violations.Add("non-ledger simulator project reference: $project -> $reference")
            }
        }

        if ($isInLedgerRepo) {
            $readmePath = Join-Path (Split-Path -Parent $projectPath) "README.md"
            if (-not (Test-Path -LiteralPath $readmePath -PathType Leaf)) {
                $relative = $readmePath.Substring($normalizedRepoRoot.Length).Replace("\", "/")
                $violations.Add("missing project README: $relative")
            }
        }
    }
}

$docsIndex = Join-Path $repoRoot "docs/README.md"
if (-not (Test-Path -LiteralPath $docsIndex -PathType Leaf)) {
    $violations.Add("missing active docs index: docs/README.md")
}

$namespacePlan = Join-Path $repoRoot "docs/architecture/namespace-compatibility.md"
if (-not (Test-Path -LiteralPath $namespacePlan -PathType Leaf)) {
    $violations.Add("missing namespace compatibility plan: docs/architecture/namespace-compatibility.md")
}
else {
    $namespacePlanText = Get-Content -LiteralPath $namespacePlan -Raw
    foreach ($requiredText in @("Current namespace/project", "Target namespace/project", "Owner", "Sunset milestone", "New-Code Guidance")) {
        if ($namespacePlanText.IndexOf($requiredText, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            $violations.Add("namespace compatibility plan missing required text: $requiredText")
        }
    }
}

$legacyMarker = Join-Path $repoRoot "docs/legacy-economy-ledger-docs/README.md"
if (-not (Test-Path -LiteralPath $legacyMarker -PathType Leaf)) {
    $violations.Add("missing legacy docs quarantine marker: docs/legacy-economy-ledger-docs/README.md")
}

if ($violations.Count -gt 0) {
    Write-Host "Ledger repository boundary audit failed."
    $violations | Sort-Object -Unique | ForEach-Object { Write-Host " - $_" }
    exit 1
}

Write-Host "Ledger repository boundary audit passed."
