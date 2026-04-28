param(
    [switch]$NoBuild,
    [switch]$RunSmokeTest
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

Push-Location $repoRoot
try {
    & "$scriptRoot\stop-sandbox.ps1"

    $runArgs = @('run', '--project', '.\AgentHub.AppHost\AgentHub.AppHost.csproj')
    if ($NoBuild) {
        $runArgs += '--no-build'
    }

    Write-Host 'Starting AgentHub sandbox...'
    $process = Start-Process -FilePath 'dotnet' -ArgumentList $runArgs -WorkingDirectory $repoRoot -PassThru
    Write-Host "AppHost PID: $($process.Id)"

    if ($RunSmokeTest) {
        & "$scriptRoot\run-scenario-1-smoke-test.ps1"
    }
}
finally {
    Pop-Location
}
