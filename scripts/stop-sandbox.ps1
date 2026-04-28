$ProgressPreference = 'SilentlyContinue'

Get-Process -Name 'AgentHub.AppHost' -ErrorAction SilentlyContinue | Stop-Process -Force

$patterns = '^(trading-dashboard|commodities-api|commodities-worker|rates-api|rates-worker|correlation-worker|servicebus-emulator|servicebus-sql|commodities-sql|rates-sql|ollama)(-|$)'
$names = docker ps -a --format '{{.Names}}' | Select-String -Pattern $patterns | ForEach-Object { $_.Line.Trim() }
if ($names) {
    docker rm -f $names | Out-Null
}

Write-Host 'Sandbox stopped and matching containers removed.'
