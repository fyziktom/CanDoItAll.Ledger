param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$forbiddenProjectOrNamespacePatterns = @(
    "CanDoItAll\.Economy\.Simulation\.SimpleAccounts",
    "CanDoItAll\.Economy\.Simulation\.ExperimentalEconomics",
    "CanDoItAll\.Economy\.Simulation\.MemoryEconomy",
    "CanDoItAll\.Economy\.SimulationSandbox",
    "CanDoItAll\.Economy\.Simulator",
    "SimulationSandbox\\ScenarioPacks",
    "ScenarioPacks",
    "SmithExperimentalEconomics",
    "SimpleAccounts"
)

$scanRoots = @("src", "tests", "examples", "tools")
$excludedSegments = @("\bin\", "\obj\", "\.git\", "\docs\migration\")
$violations = New-Object System.Collections.Generic.List[string]

function Test-IsExcludedPath {
    param([string]$Path)

    $normalized = $Path.Replace("/", "\")
    foreach ($segment in $excludedSegments) {
        if ($normalized.IndexOf($segment, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }

    return $false
}

function Get-RepoRelativePath {
    param([string]$Path)

    $root = (Resolve-Path -LiteralPath $RepositoryRoot).Path.TrimEnd("\") + "\"
    $fullPath = (Resolve-Path -LiteralPath $Path).Path
    if ($fullPath.Length -ge $root.Length -and $fullPath.Substring(0, $root.Length).ToLowerInvariant() -eq $root.ToLowerInvariant()) {
        return $fullPath.Substring($root.Length)
    }

    return $fullPath
}

foreach ($directory in Get-ChildItem -LiteralPath $RepositoryRoot -Recurse -Directory -ErrorAction SilentlyContinue) {
    if (Test-IsExcludedPath $directory.FullName) {
        continue
    }

    if ($directory.FullName -match "SimulationSandbox\\ScenarioPacks|ScenarioPacks") {
        $relative = Get-RepoRelativePath $directory.FullName
        $violations.Add("basic scenario pack directory copied into Ledger: $relative")
    }
}

$candidateFiles = foreach ($root in $scanRoots) {
    $fullRoot = Join-Path $RepositoryRoot $root
    if (Test-Path -LiteralPath $fullRoot) {
        Get-ChildItem -LiteralPath $fullRoot -Recurse -File |
            Where-Object { -not (Test-IsExcludedPath $_.FullName) }
    }
}

foreach ($file in $candidateFiles) {
    $extension = [System.IO.Path]::GetExtension($file.Name)
    if ($extension -in @(".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".dll", ".pdb", ".zip", ".gz", ".bin")) {
        continue
    }

    $matches = Select-String -LiteralPath $file.FullName -Pattern $forbiddenProjectOrNamespacePatterns -AllMatches -ErrorAction SilentlyContinue
    foreach ($match in $matches) {
        $relative = Get-RepoRelativePath $file.FullName
        $violations.Add("${relative}:$($match.LineNumber): $($match.Line.Trim())")
    }
}

if ($violations.Count -gt 0) {
    Write-Host "Ledger repository basic simulator ownership audit failed."
    Write-Host "Forbidden Economy basic simulator scenario packs/drivers:"
    $violations | Sort-Object -Unique | ForEach-Object { Write-Host " - $_" }
    exit 1
}

Write-Host "Ledger repository basic simulator ownership audit passed."
