[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,

    [switch]$RunNegativeFixture
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$expectedSolution = Join-Path $repoRoot 'CanDoItAll.Ledger.slnx'
if (-not (Test-Path -LiteralPath $expectedSolution -PathType Leaf)) {
    throw "The selected path is not the CanDoItAll.Ledger repository: $repoRoot"
}

$excludedSegments = @(
    '\.git\',
    '\bin\',
    '\obj\',
    '\artifacts\',
    '\bundles\',
    '\docker\data\',
    '\docker\demo-keys\',
    '\docker\devcerts\',
    '\docs\legacy-economy-ledger-docs\',
    '\output\',
    '\test-results\'
)
$textExtensions = @(
    '.cs', '.razor', '.csproj', '.json', '.xml', '.css', '.js', '.mjs',
    '.html', '.md', '.txt', '.sh', '.sql', '.yml', '.yaml', '.slnx',
    '.props', '.targets', '.conf', '.ps1', '.user', '.webmanifest'
)
$persistedFormatIds = @(
    'candoitall-economy-accountapp-vault-backup-v1',
    'candoitall-economy-address-directory-v1',
    'candoitall-economy-tags-v1',
    'candoitall-economy-node-operations-public-v1',
    'candoitall-economy-accountapp-settings-backup-v1',
    'candoitall-economy-account-backup-v1'
)
$forbiddenPatterns = [ordered]@{
    'old-namespace' = 'CanDoItAll\.Economy'
    'old-aspire-metadata' = 'CanDoItAll_Economy_'
    'old-config-prefix' = 'Economy__'
    'old-environment-prefix' = 'CANDOITALL_ECONOMY'
    'old-infrastructure-name' = 'candoitall[-_]economy|cda-economy|cdaeconomy|economy-node|economy-components\.css'
    'old-type-identity' = '\bEconomy(?:Account|Client|Db|Kpi|Ledger|Node|Scenario|Schema|Simulator|Token|Visual)[A-Za-z0-9_]*'
    'double-ledger-collision' = 'CanDoItAll\.Ledger\.Ledger|LedgerLedger'
}

function Test-IsExcludedPath {
    param([Parameter(Mandatory)][string]$Path)

    $normalized = $Path.Replace('/', '\')
    foreach ($segment in $excludedSegments) {
        if ($normalized.IndexOf($segment, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }

    return $false
}

function Get-RelativePath {
    param([Parameter(Mandatory)][string]$Path)

    $rootWithSeparator = $repoRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($rootWithSeparator, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escaped the repository: $fullPath"
    }

    return $fullPath.Substring($rootWithSeparator.Length).Replace('\', '/')
}

function Get-IdentityMatches {
    param([Parameter(Mandatory)][string]$Text)

    $sanitized = $Text
    foreach ($formatId in $persistedFormatIds) {
        $sanitized = $sanitized.Replace($formatId, '')
    }

    foreach ($entry in $forbiddenPatterns.GetEnumerator()) {
        foreach ($match in [regex]::Matches(
                $sanitized,
                $entry.Value,
                [Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
            [pscustomobject]@{
                Rule = $entry.Key
                Value = $match.Value
                Index = $match.Index
            }
        }
    }
}

function Get-LineNumber {
    param(
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][int]$Index
    )

    if ($Index -le 0) {
        return 1
    }

    return ([regex]::Matches($Text.Substring(0, $Index), "`n")).Count + 1
}

$violations = [Collections.Generic.List[object]]::new()
$productRoots = @('src', 'examples', 'tests', 'tools', 'docker')
$files = foreach ($rootName in $productRoots) {
    $rootPath = Join-Path $repoRoot $rootName
    if (-not (Test-Path -LiteralPath $rootPath -PathType Container)) {
        continue
    }

    Get-ChildItem -LiteralPath $rootPath -Recurse -File |
        Where-Object {
            -not (Test-IsExcludedPath $_.FullName) -and
            ($textExtensions.Contains($_.Extension.ToLowerInvariant()) -or $_.Name -eq 'Dockerfile') -and
            $_.FullName -ne $PSCommandPath
        }
}
$files += Get-Item -LiteralPath (Join-Path $repoRoot 'CanDoItAll.Ledger.slnx')
$files += Get-Item -LiteralPath (Join-Path $repoRoot 'compose.yaml')

foreach ($file in @($files | Sort-Object FullName -Unique)) {
    $relative = Get-RelativePath $file.FullName
    $pathMatches = @(
        [regex]::Matches(
            $relative,
            'CanDoItAll\.Economy|Economy(?:Account|Client|Ledger|Node|Scenario|Simulator)',
            [Text.RegularExpressions.RegexOptions]::CultureInvariant)
    )
    foreach ($pathMatch in $pathMatches) {
        $violations.Add([pscustomobject]@{
            Rule = 'old-path-identity'
            Path = $relative
            Line = 1
            Value = $pathMatch.Value
        })
    }

    $content = [IO.File]::ReadAllText(
        $file.FullName,
        [Text.UTF8Encoding]::new($false, $true))
    foreach ($match in @(Get-IdentityMatches -Text $content)) {
        $violations.Add([pscustomobject]@{
            Rule = $match.Rule
            Path = $relative
            Line = Get-LineNumber -Text $content -Index $match.Index
            Value = $match.Value
        })
    }
}

if ($RunNegativeFixture) {
    $negativeMatches = @(Get-IdentityMatches -Text @'
using CanDoItAll.Economy.Node;
var value = "Economy__Persistence__Provider";
'@)
    foreach ($requiredRule in @('old-namespace', 'old-config-prefix')) {
        if (-not ($negativeMatches | Where-Object Rule -eq $requiredRule)) {
            throw "Negative fixture did not trigger required rule '$requiredRule'."
        }
    }
}

if ($violations.Count -gt 0) {
    foreach ($violation in $violations | Sort-Object Path, Line, Rule -Unique) {
        Write-Error "[$($violation.Rule)] $($violation.Path):$($violation.Line) contains '$($violation.Value)'."
    }
    throw "Ledger identity validation failed with $($violations.Count) violation(s)."
}

[pscustomobject]@{
    Repository = $repoRoot
    ScannedFiles = @($files | Sort-Object FullName -Unique).Count
    PersistedFormatIdsAllowed = $persistedFormatIds.Count
    NegativeFixture = [bool]$RunNegativeFixture
    Result = 'PASS'
}
