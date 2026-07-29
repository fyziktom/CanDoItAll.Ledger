[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [string]$EnvFile = '.env'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$docker = Get-Command docker -ErrorAction Stop
$envPath = if ([IO.Path]::IsPathRooted($EnvFile)) {
    (Resolve-Path -LiteralPath $EnvFile).Path
}
else {
    (Resolve-Path -LiteralPath (Join-Path $repositoryRoot $EnvFile)).Path
}

$arguments = [Collections.Generic.List[string]]::new()
$arguments.Add('compose')
$arguments.Add('--env-file')
$arguments.Add($envPath)
$arguments.Add('--profile')
$arguments.Add('seed')
$resolvedConfigJson =
    (& $docker.Source @arguments config --format json) -join [Environment]::NewLine
if ($LASTEXITCODE -ne 0) {
    throw "Unable to resolve the Seeder Compose configuration (exit code $LASTEXITCODE)."
}
$resolvedConfig = $resolvedConfigJson | ConvertFrom-Json
$backupPassword =
    $resolvedConfig.services.'demo-seeder'.environment.CANDOITALL_LEDGER_ACCOUNT_BACKUP_PASSWORD
if ([string]::IsNullOrWhiteSpace($backupPassword)) {
    throw 'LEDGER_ACCOUNT_BACKUP_PASSWORD must be set to a non-empty value before running the Seeder.'
}

$arguments.Add('run')
$arguments.Add('--build')
$arguments.Add('--rm')

$isWindowsPlatform = if (Get-Variable -Name IsWindows -ErrorAction SilentlyContinue) {
    [bool]$IsWindows
}
else {
    $env:OS -eq 'Windows_NT'
}

if (-not $isWindowsPlatform) {
    $id = Get-Command id -ErrorAction Stop
    $hostUid = (& $id.Source -u).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($hostUid)) {
        throw 'Unable to resolve the host user ID for the Seeder bind mount.'
    }

    $hostGid = (& $id.Source -g).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($hostGid)) {
        throw 'Unable to resolve the host group ID for the Seeder bind mount.'
    }

    $arguments.Add('--user')
    $arguments.Add("${hostUid}:${hostGid}")
}

$arguments.Add('demo-seeder')

if (-not $PSCmdlet.ShouldProcess(
        (Join-Path $repositoryRoot 'docker\demo-keys'),
        'Build and run the disposable Ledger Seeder against the configured Node'
    )) {
    [pscustomobject]@{
        KeyRoot = Join-Path $repositoryRoot 'docker\demo-keys'
        Result = 'Preview'
    }
    return
}

Push-Location $repositoryRoot
try {
    & $docker.Source @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Ledger demo seeding failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

[pscustomobject]@{
    KeyRoot = Join-Path $repositoryRoot 'docker\demo-keys'
    Result = 'PASS'
}
