<#
.SYNOPSIS
  Deploy the weather agent to Azure Container Apps and generate traffic.

.DESCRIPTION
  Mirrors deploy.sh for Windows / PowerShell users. Requires the
  Azure CLI (`az`) and Python on PATH.

.NOTES
  Required env vars:
    AZURE_SUBSCRIPTION_ID,
    RESOURCE_GROUP, LOCATION, ACA_ENV, ACR_NAME,
    APPLICATIONINSIGHTS_CONNECTION_STRING,
    AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_DEPLOYMENT
  Optional:
    AZURE_OPENAI_API_KEY, AGENT_NAME (default weather-agent), IMAGE_TAG (default current timestamp),
    TRACE_INGEST_WAIT_SECS (default 90)
#>

$ErrorActionPreference = "Stop"

function Require-Env($name) {
  if (-not (Get-Item "Env:$name" -ErrorAction SilentlyContinue)) {
    throw "Missing required env var: $name"
  }
}

foreach ($v in @(
  "AZURE_SUBSCRIPTION_ID",
  "RESOURCE_GROUP","LOCATION","ACA_ENV","ACR_NAME",
  "APPLICATIONINSIGHTS_CONNECTION_STRING",
  "AZURE_OPENAI_ENDPOINT","AZURE_OPENAI_DEPLOYMENT"
)) { Require-Env $v }

Write-Host "==> Selecting Azure subscription"
az account set --subscription $env:AZURE_SUBSCRIPTION_ID --only-show-errors

$AgentName  = if ($env:AGENT_NAME)    { $env:AGENT_NAME }    else { "weather-agent" }
$OtelAgentId = if ($env:OTEL_AGENT_ID) { $env:OTEL_AGENT_ID } else { $AgentName }
$ImageTag   = if ($env:IMAGE_TAG)     { $env:IMAGE_TAG }     else { Get-Date -Format "yyyyMMddHHmmss" }
$TraceIngestWaitSecs = if ($env:TRACE_INGEST_WAIT_SECS) { [int]$env:TRACE_INGEST_WAIT_SECS } else { 90 }
$Image     = "$($env:ACR_NAME).azurecr.io/${AgentName}:${ImageTag}"

Write-Host "==> Building and pushing image to ACR"
az acr build --registry $env:ACR_NAME --image "${AgentName}:${ImageTag}" --no-logs --only-show-errors . | Out-Null

Write-Host "==> Deploying to Azure Container Apps"
$envVars = @(
  "AGENT_NAME=$AgentName",
  "OTEL_AGENT_ID=$OtelAgentId",
  "APPLICATIONINSIGHTS_CONNECTION_STRING=$($env:APPLICATIONINSIGHTS_CONNECTION_STRING)",
  "AZURE_OPENAI_ENDPOINT=$($env:AZURE_OPENAI_ENDPOINT)",
  "AZURE_OPENAI_DEPLOYMENT=$($env:AZURE_OPENAI_DEPLOYMENT)",
  "AZURE_OPENAI_API_VERSION=$(if ($env:AZURE_OPENAI_API_VERSION) { $env:AZURE_OPENAI_API_VERSION } else { '2024-10-21' })",
  "AZURE_OPENAI_API_KEY=$($env:AZURE_OPENAI_API_KEY)"
)

$exists = az containerapp show -n $AgentName -g $env:RESOURCE_GROUP --only-show-errors 2>$null
if ($LASTEXITCODE -eq 0 -and $exists) {
  az containerapp update `
    -n $AgentName `
    -g $env:RESOURCE_GROUP `
    --image $Image `
    --set-env-vars $envVars `
    --only-show-errors | Out-Null
} else {
  az containerapp create `
    --name $AgentName `
    --resource-group $env:RESOURCE_GROUP `
    --environment $env:ACA_ENV `
    --image $Image `
    --target-port 8000 `
    --ingress external `
    --min-replicas 1 --max-replicas 2 `
    --registry-server "$($env:ACR_NAME).azurecr.io" `
    --system-assigned `
    --registry-identity system `
    --env-vars $envVars `
    --only-show-errors | Out-Null
}

$Fqdn = az containerapp show -n $AgentName -g $env:RESOURCE_GROUP `
        --query properties.configuration.ingress.fqdn -o tsv --only-show-errors
$AgentUrl = "https://$Fqdn"
Write-Host "==> Agent URL: $AgentUrl"

Write-Host "==> Waiting for /healthz"
for ($i = 0; $i -lt 30; $i++) {
  try { Invoke-WebRequest -UseBasicParsing -Uri "$AgentUrl/healthz" -TimeoutSec 10 | Out-Null; break }
  catch { Start-Sleep -Seconds 5 }
}

Write-Host "==> Generating traffic"
$env:AGENT_URL = $AgentUrl
$env:AGENT_NAME = $AgentName
$env:OTEL_AGENT_ID = $OtelAgentId
python generate_traffic.py

if ($TraceIngestWaitSecs -gt 0) {
  Write-Host "==> Waiting ${TraceIngestWaitSecs}s for OTel export and ingestion"
  Start-Sleep -Seconds $TraceIngestWaitSecs
}

Write-Host "==> Done. Agent URL: $AgentUrl"
Write-Host "==> Then run: python register_external_agent.py; python run_trace_eval.py"
