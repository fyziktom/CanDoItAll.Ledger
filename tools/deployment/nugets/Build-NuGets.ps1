<#
.SYNOPSIS
Builds, tests, and packs the public Ledger NuGet packages.

.PARAMETER Configuration
Build configuration. The default is Release.

.PARAMETER OutputDirectory
Absolute or repository-relative package destination.

.PARAMETER NoRestore
Skips restore when the caller guarantees it has already completed.

.PARAMETER Version
Overrides the package version without editing committed project files.

.EXAMPLE
.\tools\deployment\nugets\Build-NuGets.ps1 -Version '1.2.3'
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$OutputDirectory,

    [switch]$NoRestore,

    [string]$Version = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-DotNetCommand {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$Description
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$solutionPath = Join-Path $repositoryRoot 'CanDoItAll.Ledger.slnx'
$testProject = Join-Path $repositoryRoot 'tests\CanDoItAll.Ledger.Tests\CanDoItAll.Ledger.Tests.csproj'
$packageProjects = @(
    'src\CanDoItAll.Ledger.BusinessObjects\CanDoItAll.Ledger.BusinessObjects.csproj'
    'src\CanDoItAll.Ledger.Components\CanDoItAll.Ledger.Components.csproj'
    'src\CanDoItAll.Ledger.Core\CanDoItAll.Ledger.Core.csproj'
    'src\CanDoItAll.Ledger.Cryptography\CanDoItAll.Ledger.Cryptography.csproj'
    'src\CanDoItAll.Ledger.Cryptography.Browser\CanDoItAll.Ledger.Cryptography.Browser.csproj'
    'src\CanDoItAll.Ledger.Engine\CanDoItAll.Ledger.Engine.csproj'
    'src\CanDoItAll.Ledger.Persistence.EFCore\CanDoItAll.Ledger.Persistence.EFCore.csproj'
    'src\CanDoItAll.Ledger.Persistence.PostgreSql\CanDoItAll.Ledger.Persistence.PostgreSql.csproj'
    'src\CanDoItAll.Ledger.Rules.NCalc\CanDoItAll.Ledger.Rules.NCalc.csproj'
    'src\CanDoItAll.Ledger.Sdk\CanDoItAll.Ledger.Sdk.csproj'
    'src\CanDoItAll.Ledger.Storage.InMemory\CanDoItAll.Ledger.Storage.InMemory.csproj'
) | ForEach-Object { Join-Path $repositoryRoot $_ }

$requiredPaths = @($solutionPath, $testProject) + $packageProjects
foreach ($requiredPath in $requiredPaths) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required packaging input was not found: '$requiredPath'."
    }
}

$simulatorPackages = @(
    $packageProjects |
        Where-Object {
            [System.IO.Path]::GetFileNameWithoutExtension($_) -match '\.(?:Simulation|Simulator)(?:\.|$)'
        }
)
if ($simulatorPackages.Count -gt 0) {
    throw "Simulator projects must not be included in the public package allowlist: $($simulatorPackages -join ', ')"
}

$effectiveVersion = $Version.Trim()
$msbuildProperties = @()
if (-not [string]::IsNullOrWhiteSpace($effectiveVersion)) {
    $msbuildProperties += "-p:Version=$effectiveVersion"
}

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts\packages'
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot $OutputDirectory
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$operation = if ($NoRestore) {
    'Build, test, and pack the public Ledger NuGet package allowlist'
}
else {
    'Restore, build, test, and pack the public Ledger NuGet package allowlist'
}
if (-not [string]::IsNullOrWhiteSpace($effectiveVersion)) {
    $operation += " at version '$effectiveVersion'"
}

if (-not $PSCmdlet.ShouldProcess($OutputDirectory, $operation)) {
    [pscustomobject]@{
        Repository = Split-Path $repositoryRoot -Leaf
        Configuration = $Configuration
        PackageVersion = $effectiveVersion
        OutputDirectory = $OutputDirectory
        Packages = @($packageProjects | ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_) })
        Status = 'Preview'
    }
    return
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

if (-not $NoRestore) {
    Invoke-DotNetCommand `
        -Arguments (
            @(
                'restore',
                $solutionPath,
                '--configfile',
                (Join-Path $repositoryRoot 'NuGet.config')
            ) + $msbuildProperties
        ) `
        -Description 'dotnet restore'
}

# Some solution roots build BusinessObjects with different
# EnableBusinessObjectIpfsStorage values. Those graph nodes currently share output
# directories, so parallel solution builds can race while writing bin/obj artifacts.
Invoke-DotNetCommand `
    -Arguments (
        @(
            'build',
            $solutionPath,
            '--configuration', $Configuration,
            '--no-restore',
            '-maxcpucount:1',
            '-p:ContinuousIntegrationBuild=true',
            '-p:GeneratePackageOnBuild=false'
        ) + $msbuildProperties
    ) `
    -Description 'dotnet build'

Invoke-DotNetCommand `
    -Arguments (
        @(
            'test',
            $testProject,
            '--configuration', $Configuration,
            '--no-build',
            '--no-restore'
        ) + $msbuildProperties
    ) `
    -Description 'dotnet test'

foreach ($packageProject in $packageProjects) {
    Invoke-DotNetCommand `
        -Arguments (
            @(
                'pack',
                $packageProject,
                '--configuration', $Configuration,
                '--no-build',
                '--no-restore',
                '--output', $OutputDirectory,
                '-p:ContinuousIntegrationBuild=true',
                '-p:GeneratePackageOnBuild=false'
            ) + $msbuildProperties
        ) `
        -Description "dotnet pack for '$([System.IO.Path]::GetFileNameWithoutExtension($packageProject))'"
}

[pscustomobject]@{
    Repository = Split-Path $repositoryRoot -Leaf
    Configuration = $Configuration
    PackageVersion = $effectiveVersion
    OutputDirectory = $OutputDirectory
    Packages = @($packageProjects | ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_) })
    Status = 'Succeeded'
}
