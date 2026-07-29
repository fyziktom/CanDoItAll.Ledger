[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [string[]]$ComposeFile = @('compose.yaml'),

    [string]$EnvFile = '.env.example',

    [switch]$RunBuildChecks,

    [switch]$Smoke,

    [ValidateRange(1, 3600)]
    [int]$WaitTimeoutSeconds = 300,

    [ValidateRange(1, 65535)]
    [int]$NodePort = 8080,

    [ValidateRange(1, 65535)]
    [int]$AlicePort = 5081,

    [ValidateRange(1, 65535)]
    [int]$BobPort = 5082
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$docker = Get-Command docker -ErrorAction Stop
$composePaths = @(
    foreach ($path in $ComposeFile) {
        $candidate = if ([IO.Path]::IsPathRooted($path)) {
            $path
        }
        else {
            Join-Path $repositoryRoot $path
        }
        (Resolve-Path -LiteralPath $candidate).Path
    }
)
$envPath = if ([string]::IsNullOrWhiteSpace($EnvFile)) {
    $null
}
else {
    $candidate = if ([IO.Path]::IsPathRooted($EnvFile)) {
        $EnvFile
    }
    else {
        Join-Path $repositoryRoot $EnvFile
    }
    (Resolve-Path -LiteralPath $candidate).Path
}

$composeArguments = [Collections.Generic.List[string]]::new()
$composeArguments.Add('compose')
if ($envPath) {
    $composeArguments.Add('--env-file')
    $composeArguments.Add($envPath)
}
foreach ($composePath in $composePaths) {
    $composeArguments.Add('-f')
    $composeArguments.Add($composePath)
}

& $docker.Source @composeArguments config --quiet
if ($LASTEXITCODE -ne 0) {
    throw "Compose validation failed with exit code $LASTEXITCODE."
}

$services = @(& $docker.Source @composeArguments config --services)
if ($LASTEXITCODE -ne 0) {
    throw "Compose service discovery failed with exit code $LASTEXITCODE."
}
$requiredServices = @('postgres', 'node', 'accountapp-alice', 'accountapp-bob', 'accountapp-lab')
$missingServices = @($requiredServices | Where-Object { $services -notcontains $_ })
if ($missingServices.Count -gt 0) {
    throw "Compose is missing required services: $($missingServices -join ', ')."
}

$sharedInfoCandidates = @(
    $env:CANDOITALL_SHAREDINFO_ROOT
    (Join-Path (Split-Path -Parent $repositoryRoot) 'CanDoItAll.SharedInfo')
) | Where-Object { $_ }
$sharedValidator = $null
foreach ($candidate in $sharedInfoCandidates) {
    $validator = Join-Path $candidate 'tools\validation\Test-DockerConventions.ps1'
    if (Test-Path -LiteralPath $validator -PathType Leaf) {
        $sharedValidator = (Resolve-Path -LiteralPath $validator).Path
        break
    }
}
if (-not $sharedValidator) {
    throw 'CanDoItAll.SharedInfo Docker validation was not found.'
}

$sharedArguments = @{
    RepositoryPath = $repositoryRoot
    ComposeFile = $composePaths
    RequireDocker = $true
    WarningsAsErrors = $true
}
if ($envPath) {
    $sharedArguments.EnvFile = $envPath
}
& $sharedValidator @sharedArguments

if ($RunBuildChecks) {
    # Keep the repository-owned check Compose-aware so it validates every
    # service/profile against the same resolved build model used by developers.
    & $docker.Source @composeArguments --profile '*' build --check
    if ($LASTEXITCODE -ne 0) {
        throw "Compose-aware Dockerfile checks failed with exit code $LASTEXITCODE."
    }
}

if (-not $Smoke) {
    [pscustomobject]@{
        Repository = $repositoryRoot
        ComposeFiles = $composePaths
        Services = $services
        BuildChecks = [bool]$RunBuildChecks
        Smoke = $false
        Result = 'PASS'
    }
    return
}

$projectName = "candoitall-ledger-validation-$PID"
if (-not $PSCmdlet.ShouldProcess(
        $projectName,
        'Build and start a disposable Ledger stack, run HTTP smoke checks, then remove its containers and volumes'
    )) {
    return
}

$started = $false
try {
    $started = $true
    & $docker.Source @composeArguments -p $projectName up -d --build --wait --wait-timeout $WaitTimeoutSeconds
    if ($LASTEXITCODE -ne 0) {
        throw "Compose smoke startup failed with exit code $LASTEXITCODE."
    }

    foreach ($uri in @(
            "http://127.0.0.1:$NodePort/api/v1/health",
            "http://127.0.0.1:$AlicePort/",
            "http://127.0.0.1:$BobPort/"
        )) {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $uri -TimeoutSec 30
        if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 400) {
            throw "HTTP smoke failed for '$uri' with status $($response.StatusCode)."
        }
    }

    & $docker.Source @composeArguments -p $projectName ps --all
    if ($LASTEXITCODE -ne 0) {
        throw "Compose status failed with exit code $LASTEXITCODE."
    }
}
finally {
    if ($started) {
        & $docker.Source @composeArguments -p $projectName down --volumes --remove-orphans
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Compose teardown failed for disposable project '$projectName'."
        }
    }
}

[pscustomobject]@{
    Repository = $repositoryRoot
    ComposeFiles = $composePaths
    Services = $services
    BuildChecks = [bool]$RunBuildChecks
    Smoke = $true
    ProjectName = $projectName
    Result = 'PASS'
}
