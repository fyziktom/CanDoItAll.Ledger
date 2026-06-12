param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "audit-ledger-repo-does-not-contain-basic-simulator.ps1") -RepositoryRoot $RepositoryRoot
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$solution = Join-Path $RepositoryRoot "CanDoItAll.Ledger.slnx"
$violations = New-Object System.Collections.Generic.List[string]

if (Test-Path -LiteralPath $solution) {
    $projects = dotnet sln $solution list | Select-Object -Skip 2 | Where-Object { $_.Trim() }
    foreach ($project in $projects) {
        $projectPath = Join-Path $RepositoryRoot $project
        if (-not (Test-Path -LiteralPath $projectPath)) {
            $violations.Add("solution project does not exist: $project")
            continue
        }

        [xml]$xml = Get-Content -LiteralPath $projectPath
        $references = @($xml.Project.ItemGroup.ProjectReference.Include) | Where-Object { $_ }
        foreach ($reference in $references) {
            $normalized = $reference.Replace("/", "\")
            if ($normalized -match "\.\.\\CanDoItAll\.Economy\\") {
                $violations.Add("cross-repo Economy project reference: $project -> $reference")
            }
            if ($normalized -match "Simulation\.SimpleAccounts|Simulation\.ExperimentalEconomics|SimulationSandbox|Simulator") {
                $violations.Add("basic simulator project reference: $project -> $reference")
            }
        }
    }
}
else {
    $violations.Add("missing Ledger solution: $solution")
}

if ($violations.Count -gt 0) {
    Write-Host "Ledger repository boundary audit failed."
    $violations | Sort-Object -Unique | ForEach-Object { Write-Host " - $_" }
    exit 1
}

Write-Host "Ledger repository boundary audit passed."
