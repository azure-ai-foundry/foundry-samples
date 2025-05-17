param(
[string] $subscriptionId,
[string] $resourcegroup,
[string] $accountName)
while ($true) {

$token = (az account get-access-token --subscription $subscriptionId --query accessToken -o tsv)

$uri = "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$resourcegroup/providers/Microsoft.CognitiveServices/accounts/$accountName/capabilityHosts/?api-version=2025-04-01-preview"
$content = (az rest --method get --uri $uri --headers "Authorization=Bearer $token")
$jsonObject = $content | ConvertFrom-Json
$provisioningState = $jsonObject.value[0].properties.provisioningState

Write-Output "Provisioning State: $provisioningState"
if ($provisioningState -eq "Succeeded") {
  Write-Output "Provisioning State: $provisioningState, Please proceed with project creation template."
  break;
 }

if ($provisioningState -eq "Failed" -or $provisioningState -eq "Canceled") {
 Write-Output "Provisioning State: $provisioningState, project provisionig will not work."
 break;
 }

 Start-Sleep -Seconds 30
}