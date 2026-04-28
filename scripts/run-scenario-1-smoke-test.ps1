param(
    [string]$DashboardBaseUrl = "http://localhost:17020",
    [string]$Question = "Is there a coherent macro narrative that explains current positions across both trading books?",
    [int]$ReadyTimeoutSeconds = 180
)

$ProgressPreference = 'SilentlyContinue'
$deadline = (Get-Date).AddSeconds($ReadyTimeoutSeconds)
$ready = $false

while ((Get-Date) -lt $deadline) {
    try {
        $probe = Invoke-WebRequest -Method Get -Uri "$DashboardBaseUrl/" -TimeoutSec 3
        if ($probe.StatusCode -ge 200 -and $probe.StatusCode -lt 500) {
            $ready = $true
            break
        }
    }
    catch {
    }
}

if (-not $ready) {
    throw "Dashboard did not become ready within $ReadyTimeoutSeconds seconds at $DashboardBaseUrl."
}

$bootstrapResponse = Invoke-RestMethod -Method Post -Uri "$DashboardBaseUrl/api/scenario-1/bootstrap"
Write-Host "Scenario bootstrap completed for correlation key: $($bootstrapResponse.correlationKey)"

$queryBody = @{
    question = $Question
    correlationKey = $bootstrapResponse.correlationKey
} | ConvertTo-Json

$queryResponse = Invoke-RestMethod -Method Post -Uri "$DashboardBaseUrl/api/macro-query" -ContentType "application/json" -Body $queryBody

Write-Host "Model: $($queryResponse.model)"
Write-Host "Generated At: $($queryResponse.generatedAt)"
Write-Host "Narrative:"
Write-Host $queryResponse.narrative
