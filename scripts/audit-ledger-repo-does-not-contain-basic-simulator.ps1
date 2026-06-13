param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch]$RunNegativeFixture,
    [string]$ReportPath
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$violations = New-Object System.Collections.Generic.List[object]
$allowlistEntries = New-Object System.Collections.Generic.List[object]
$observations = New-Object System.Collections.Generic.List[string]

function Add-Violation {
    param(
        [string]$Rule,
        [string]$Path,
        [int]$Line,
        [string]$Message
    )

    $violations.Add([pscustomobject]@{
        Rule = $Rule
        Path = $Path
        Line = $Line
        Message = $Message
    })
}

function Add-AllowlistEntry {
    param(
        [string]$Scope,
        [string]$Owner,
        [string]$ExpectedBenefit,
        [string]$MaintenanceCost,
        [string]$EnforcementCost,
        [string]$RetirementCondition
    )

    $allowlistEntries.Add([pscustomobject]@{
        Scope = $Scope
        Owner = $Owner
        ExpectedBenefit = $ExpectedBenefit
        MaintenanceCost = $MaintenanceCost
        EnforcementCost = $EnforcementCost
        RetirementCondition = $RetirementCondition
    })
}

function Get-RepoRelativePath {
    param([string]$Path)

    $fullPath = (Resolve-Path -LiteralPath $Path).Path
    $prefix = $repoRoot.TrimEnd("\", "/") + "\"
    if ($fullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($prefix.Length).Replace("\", "/")
    }

    return $fullPath
}

function Test-IsExcludedPath {
    param([string]$Path)

    $relative = Get-RepoRelativePath $Path
    $normalized = $relative.Replace("/", "\")
    if ($normalized -match "(^|\\)(bin|obj|\.git|\.vs|TestResults)(\\|$)") {
        return $true
    }

    foreach ($prefix in @("artifacts\", "output\")) {
        if ($normalized.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Test-IsBinaryLikeFile {
    param([System.IO.FileInfo]$File)

    return $File.Extension -in @(
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico",
        ".dll", ".pdb", ".exe", ".zip", ".gz", ".bin", ".wasm"
    )
}

function Get-CandidateFiles {
    foreach ($fileName in @("README.md", "CanDoItAll.Ledger.slnx", "Directory.Build.props", "Directory.Build.targets")) {
        $path = Join-Path $repoRoot $fileName
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            Get-Item -LiteralPath $path
        }
    }

    foreach ($rootName in @("src", "tests", "examples", "tools", "docs")) {
        $path = Join-Path $repoRoot $rootName
        if (Test-Path -LiteralPath $path -PathType Container) {
            Get-ChildItem -LiteralPath $path -Recurse -File |
                Where-Object { -not (Test-IsExcludedPath $_.FullName) -and -not (Test-IsBinaryLikeFile $_) }
        }
    }
}

function Get-ForbiddenBasicSimulatorRules {
    @(
        [pscustomobject]@{ Id = "basic-simpleaccounts"; Pattern = "CanDoItAll\.Economy\.Simulation\.SimpleAccounts|Simulation\.SimpleAccounts|SimpleAccounts"; Message = "Ledger must not reference Economy SimpleAccounts." },
        [pscustomobject]@{ Id = "basic-experimental-economics"; Pattern = "CanDoItAll\.Economy\.Simulation\.ExperimentalEconomics|Simulation\.ExperimentalEconomics|SmithExperimentalEconomics"; Message = "Ledger must not reference Economy experimental-economics or Smith driver projects." },
        [pscustomobject]@{ Id = "basic-memory-economy"; Pattern = "CanDoItAll\.Economy\.Simulation\.MemoryEconomy|Simulation\.MemoryEconomy"; Message = "Ledger must not reference Economy memory-economy simulator extensions." },
        [pscustomobject]@{ Id = "basic-simulation-sandbox"; Pattern = "CanDoItAll\.Economy\.SimulationSandbox|SimulationSandbox|EconomySimulationSandbox"; Message = "Ledger must not reference the Economy SimulationSandbox." },
        [pscustomobject]@{ Id = "basic-simulator-host"; Pattern = "CanDoItAll\.Economy\.Simulator"; Message = "Ledger may reference this only inside the moved BO/project simulator compatibility landing zone." },
        [pscustomobject]@{ Id = "basic-scenario-packs"; Pattern = "ScenarioPacks"; Message = "Ledger must not contain or reference Economy scenario packs." }
    )
}

function Get-LineNumber {
    param(
        [string]$Content,
        [int]$Index
    )

    if ($Index -le 0) {
        return 1
    }

    return ($Content.Substring(0, $Index).ToCharArray() | Where-Object { $_ -eq "`n" }).Count + 1
}

function Test-IsLegacyQuarantinePath {
    param([string]$RelativePath)

    return $RelativePath.Replace("/", "\").StartsWith("docs\legacy-economy-ledger-docs\", [StringComparison]::OrdinalIgnoreCase)
}

function Test-IsBoundaryDocumentationPath {
    param([string]$RelativePath)

    $normalized = $RelativePath.Replace("/", "\")
    return $normalized -eq "README.md" -or
        $normalized -eq "docs\architecture\repository-boundaries.md" -or
        $normalized -eq "docs\migration\import-inventory.md" -or
        $normalized -eq "docs\architecture\namespace-compatibility.md" -or
        $normalized -eq "docs\migration\ledger-simulator-runtime-ownership-inventory.md"
}

function Test-IsLedgerBoSimulatorCompatibilityPath {
    param([string]$RelativePath)

    $normalized = $RelativePath.Replace("/", "\")
    return $normalized.StartsWith("src\CanDoItAll.Ledger.Simulator\", [StringComparison]::OrdinalIgnoreCase) -or
        $normalized.StartsWith("src\CanDoItAll.Ledger.Simulator.Components\", [StringComparison]::OrdinalIgnoreCase) -or
        $normalized.StartsWith("src\CanDoItAll.Ledger.Simulator.Persistence.EFCore\", [StringComparison]::OrdinalIgnoreCase) -or
        $normalized.StartsWith("examples\CanDoItAll.Ledger.Simulator.App\", [StringComparison]::OrdinalIgnoreCase) -or
        $normalized -eq "tests\CanDoItAll.Ledger.Tests\EconomySimulatorTests.cs" -or
        $normalized -eq "tests\CanDoItAll.Ledger.Tests\EconomySimulatorComponentTests.cs" -or
        $normalized -eq "tests\CanDoItAll.Ledger.Tests\EconomySimulatorRuleAutomationTests.cs" -or
        $normalized -eq "tests\CanDoItAll.Ledger.Tests\SimulationApiEndpointTests.cs" -or
        $normalized -eq "tests\CanDoItAll.Ledger.Tests\SimulationProjectManagementTests.cs"
}

function Assert-LegacyQuarantineMarker {
    $markerPath = Join-Path $repoRoot "docs/legacy-economy-ledger-docs/README.md"
    if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) {
        Add-Violation "missing-legacy-quarantine-marker" "docs/legacy-economy-ledger-docs/README.md" 1 "Legacy Economy docs in Ledger require a non-product quarantine marker."
        return
    }

    $content = Get-Content -LiteralPath $markerPath -Raw
    if ($content.IndexOf("non-product legacy quarantine", [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        Add-Violation "weak-legacy-quarantine-marker" "docs/legacy-economy-ledger-docs/README.md" 1 "Legacy docs marker must say 'non-product legacy quarantine'."
    }
}

function Find-ContentViolations {
    param(
        [string]$RelativePath,
        [string]$Content
    )

    $found = New-Object System.Collections.Generic.List[object]
    $isLegacyQuarantine = Test-IsLegacyQuarantinePath $RelativePath
    $isBoundaryDocumentation = Test-IsBoundaryDocumentationPath $RelativePath

    foreach ($rule in @(Get-ForbiddenBasicSimulatorRules)) {
        foreach ($match in [regex]::Matches($Content, $rule.Pattern, [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
            if ($isLegacyQuarantine -or $isBoundaryDocumentation -or ($rule.Id -eq "basic-simulator-host" -and (Test-IsLedgerBoSimulatorCompatibilityPath $RelativePath))) {
                continue
            }

            $found.Add([pscustomobject]@{
                Rule = $rule.Id
                Path = $RelativePath
                Line = Get-LineNumber $Content $match.Index
                Message = $rule.Message
            })
        }
    }

    return $found.ToArray()
}

function Assert-SolutionBoundary {
    $solutionPath = Join-Path $repoRoot "CanDoItAll.Ledger.slnx"
    if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
        Add-Violation "missing-solution" "CanDoItAll.Ledger.slnx" 1 "Ledger solution is missing."
        return
    }

    $solutionProjects = @(dotnet sln $solutionPath list | Select-Object -Skip 2 | Where-Object { $_.Trim() })
    foreach ($project in $solutionProjects) {
        $projectRelative = $project.Trim().Replace("\", "/")
        $projectPath = Join-Path $repoRoot $projectRelative
        if ($projectRelative.StartsWith("../", [StringComparison]::Ordinal)) {
            $projectPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $projectRelative))
        }

        if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
            Add-Violation "missing-solution-project" $projectRelative 1 "Solution project path does not exist."
            continue
        }

        foreach ($rule in @(Get-ForbiddenBasicSimulatorRules)) {
            if ($projectRelative -match $rule.Pattern) {
                Add-Violation "forbidden-solution-project" $projectRelative 1 $rule.Message
            }
        }
    }
}

function Assert-ProjectReferences {
    $projectRoots = @("src", "tests", "examples", "tools")
    $projectFiles = foreach ($rootName in $projectRoots) {
        $path = Join-Path $repoRoot $rootName
        if (Test-Path -LiteralPath $path -PathType Container) {
            Get-ChildItem -LiteralPath $path -Recurse -Filter *.csproj -File |
                Where-Object { -not (Test-IsExcludedPath $_.FullName) }
        }
    }

    foreach ($projectFile in $projectFiles) {
        $relative = Get-RepoRelativePath $projectFile.FullName
        [xml]$xml = Get-Content -LiteralPath $projectFile.FullName -Raw
        $references = @(
            $xml.Project.ItemGroup.ProjectReference | ForEach-Object { $_.Include }
            $xml.Project.ItemGroup.PackageReference | ForEach-Object { $_.Include }
        ) | Where-Object { $_ }

        foreach ($reference in $references) {
            foreach ($rule in @(Get-ForbiddenBasicSimulatorRules)) {
                if ($reference -match $rule.Pattern) {
                    if ($rule.Id -eq "basic-simulator-host" -and (Test-IsLedgerBoSimulatorCompatibilityPath $relative)) {
                        continue
                    }
                    Add-Violation "forbidden-project-reference" $relative 1 "Project reference '$reference' violates: $($rule.Message)"
                }
            }
        }
    }
}

function Assert-NoScenarioPackDirectories {
    foreach ($directory in Get-ChildItem -LiteralPath $repoRoot -Recurse -Directory -ErrorAction SilentlyContinue) {
        if (Test-IsExcludedPath $directory.FullName) {
            continue
        }

        $relative = Get-RepoRelativePath $directory.FullName
        if (Test-IsLegacyQuarantinePath $relative) {
            continue
        }

        if ($relative -match "(^|/)ScenarioPacks(/|$)|SimulationSandbox") {
            Add-Violation "forbidden-basic-simulator-directory" $relative 1 "Ledger must not contain Economy scenario pack or SimulationSandbox directories outside legacy quarantine."
        }
    }
}

function Assert-NodeProjectNoSimulatorHost {
    $nodeRoot = Join-Path $repoRoot "src/CanDoItAll.Economy.Node"
    if (-not (Test-Path -LiteralPath $nodeRoot -PathType Container)) {
        return
    }

    $nodeFiles = Get-ChildItem -LiteralPath $nodeRoot -Recurse -File |
        Where-Object { -not (Test-IsExcludedPath $_.FullName) -and -not (Test-IsBinaryLikeFile $_) }
    foreach ($file in $nodeFiles) {
        $relative = Get-RepoRelativePath $file.FullName
        $content = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($rule in @(Get-ForbiddenBasicSimulatorRules)) {
            foreach ($match in [regex]::Matches($content, $rule.Pattern, [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
                Add-Violation "node-basic-simulator-reference" $relative (Get-LineNumber $content $match.Index) "Node project must remain ledger-only. $($rule.Message)"
            }
        }
    }
}

function Assert-NegativeFixture {
    $fixture = @'
using CanDoItAll.Economy.Simulation.SimpleAccounts;
using CanDoItAll.Economy.Simulation.ExperimentalEconomics;
using CanDoItAll.Economy.Simulation.MemoryEconomy;
using CanDoItAll.Economy.SimulationSandbox;
using CanDoItAll.Economy.Simulator;

var source = "SimulationSandbox/ScenarioPacks/EconomySimulationSandbox";
var smith = "SmithExperimentalEconomics";
var simple = "SimpleAccounts";
'@

    $fixtureViolations = @(Find-ContentViolations "negative-fixture/BasicSimulatorLeak.cs" $fixture)
    $requiredRules = @(
        "basic-simpleaccounts",
        "basic-experimental-economics",
        "basic-memory-economy",
        "basic-simulation-sandbox",
        "basic-simulator-host",
        "basic-scenario-packs"
    )

    foreach ($rule in $requiredRules) {
        if (-not ($fixtureViolations | Where-Object { $_.Rule -eq $rule })) {
            throw "Negative fixture did not trigger expected Ledger rule: $rule"
        }
    }

    $observations.Add("Negative fixture triggered expected Ledger no-basic-simulator rules: $($requiredRules -join ', ').")
}

function Add-CompatibilityNamespaceObservation {
    $matches = @(Get-ChildItem -LiteralPath $repoRoot -Recurse -File -Include *.cs,*.csproj |
        Where-Object { -not (Test-IsExcludedPath $_.FullName) } |
        Select-String -Pattern "CanDoItAll\.Economy\." -AllMatches -ErrorAction SilentlyContinue)

    if ($matches.Count -gt 0) {
        $observations.Add("Compatibility namespace references remain in Ledger-owned code: $($matches.Count) match(es). They are governed by docs/architecture/namespace-compatibility.md and do not imply basic simulator ownership.")
    }
}

function Write-AuditReport {
    param([bool]$Passed)

    if ([string]::IsNullOrWhiteSpace($ReportPath)) {
        return
    }

    $resolvedReportPath = $ReportPath
    if (-not [System.IO.Path]::IsPathRooted($resolvedReportPath)) {
        $resolvedReportPath = Join-Path $repoRoot $resolvedReportPath
    }

    $reportDirectory = Split-Path -Parent $resolvedReportPath
    if (-not (Test-Path -LiteralPath $reportDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    }

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("# Ledger No-Basic-Simulator Audit")
    $lines.Add("")
    $lines.Add("- Result: " + $(if ($Passed) { "PASS" } else { "FAIL" }))
    $lines.Add("- Repository: $repoRoot")
    $lines.Add("- Scanned: solution, project references, source, tests, examples, tools, active docs, quarantined legacy docs, Node source, scenario-pack directories")
    $lines.Add("")
    $lines.Add("## Allowlist Entries")
    $lines.Add("")
    $lines.Add("| Scope | Owner | Expected benefit | Maintenance cost | Enforcement cost | Retirement condition |")
    $lines.Add("|---|---|---|---|---|---|")
    foreach ($entry in $allowlistEntries) {
        $lines.Add("| `$($entry.Scope)` | $($entry.Owner) | $($entry.ExpectedBenefit) | $($entry.MaintenanceCost) | $($entry.EnforcementCost) | $($entry.RetirementCondition) |")
    }

    $lines.Add("")
    $lines.Add("## Observations")
    if ($observations.Count -eq 0) {
        $lines.Add("")
        $lines.Add("- No observations.")
    }
    else {
        foreach ($observation in $observations) {
            $lines.Add("- $observation")
        }
    }

    $lines.Add("")
    $lines.Add("## Violations")
    if ($violations.Count -eq 0) {
        $lines.Add("")
        $lines.Add("No violations.")
    }
    else {
        foreach ($violation in $violations | Sort-Object Path, Line, Rule -Unique) {
            $lines.Add("- `$($violation.Path):$($violation.Line)` [$($violation.Rule)] $($violation.Message)")
        }
    }

    Set-Content -LiteralPath $resolvedReportPath -Value $lines -Encoding UTF8
}

Add-AllowlistEntry `
    -Scope "docs/legacy-economy-ledger-docs/**" `
    -Owner "Ledger split maintainer" `
    -ExpectedBenefit "Preserve imported historical documentation without presenting Economy simulator docs as active Ledger product docs." `
    -MaintenanceCost "Quarantine marker and audit report must be reviewed during split cleanup." `
    -EnforcementCost "No product code may cite the quarantine as active guidance; active docs remain scanned separately." `
    -RetirementCondition "Delete or move simulator-specific legacy docs back to Economy after Ledger docs are complete."

Add-AllowlistEntry `
    -Scope "README.md; docs/architecture/repository-boundaries.md; docs/migration/import-inventory.md; docs/architecture/namespace-compatibility.md" `
    -Owner "Ledger architecture maintainer" `
    -ExpectedBenefit "Allow active boundary docs to name forbidden Economy simulator surfaces so humans and audits know what to reject." `
    -MaintenanceCost "Review wording when the split boundary changes." `
    -EnforcementCost "Allowed only in documentation paths; source/tests/examples/tools stay strict." `
    -RetirementCondition "Retire only if boundary rules move to structured policy metadata with equivalent human-readable generated docs."

Add-AllowlistEntry `
    -Scope "CanDoItAll.Economy.* compatibility namespaces in Ledger-owned code" `
    -Owner "Ledger namespace migration owner" `
    -ExpectedBenefit "Avoid combining physical repository split with a broad public API rename." `
    -MaintenanceCost "Namespace map, consumer migration guidance, and new-code naming checks." `
    -EnforcementCost "Boundary audits must distinguish compatibility namespaces from basic simulator ownership." `
    -RetirementCondition "Remove after package consumers are migrated and type-forwarding/rename plan is complete."

Assert-LegacyQuarantineMarker
Assert-SolutionBoundary
Assert-ProjectReferences
Assert-NoScenarioPackDirectories
Assert-NodeProjectNoSimulatorHost
Add-CompatibilityNamespaceObservation

foreach ($file in @(Get-CandidateFiles | Sort-Object FullName -Unique)) {
    $relative = Get-RepoRelativePath $file.FullName
    $content = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($violation in @(Find-ContentViolations $relative $content)) {
        Add-Violation $violation.Rule $violation.Path $violation.Line $violation.Message
    }
}

if ($RunNegativeFixture) {
    Assert-NegativeFixture
}

$passed = $violations.Count -eq 0
Write-AuditReport -Passed $passed

if (-not $passed) {
    Write-Host "Ledger repository basic simulator ownership audit failed."
    $violations | Sort-Object Path, Line, Rule -Unique | ForEach-Object {
        Write-Host " - $($_.Path):$($_.Line) [$($_.Rule)] $($_.Message)"
    }
    exit 1
}

Write-Host "Ledger repository basic simulator ownership audit passed."
if ($RunNegativeFixture) {
    Write-Host "Ledger negative fixture passed."
}
