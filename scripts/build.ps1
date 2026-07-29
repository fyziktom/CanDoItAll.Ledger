[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$solution = Join-Path $repoRoot 'CanDoItAll.Ledger.slnx'
$dotnet = Get-Command dotnet -ErrorAction Stop

& $dotnet.Source build $solution -c $Configuration -m:1
if ($LASTEXITCODE -ne 0) {
    throw "Ledger solution build failed with exit code $LASTEXITCODE."
}

[pscustomobject]@{
    Solution = $solution
    Configuration = $Configuration
    Result = 'PASS'
}
