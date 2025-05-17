#!/bin/bash

subscriptionId="$1"
resourcegroup="$2"
accountName="$3"

if [[ -z "$subscriptionId" || -z "$resourcegroup" || -z "$accountName" ]]; then
    echo "Usage: $0 <subscriptionId> <resourcegroup> <accountName>"
    exit 1
fi

while true; do
    token=$(az account get-access-token --subscription "$subscriptionId" --query accessToken -o tsv)
    uri="https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$resourcegroup/providers/Microsoft.CognitiveServices/accounts/$accountName/capabilityHosts/?api-version=2025-04-01-preview"
    content=$(az rest --method get --uri "$uri" --headers "Authorization=Bearer $token")
    provisioningState=$(echo "$content" | jq -r '.value[0].properties.provisioningState')

    echo "Provisioning State: $provisioningState"

    if [[ "$provisioningState" == "Succeeded" ]]; then
        echo "Provisioning State: $provisioningState, Please proceed with project creation template."
        break
    fi

    if [[ "$provisioningState" == "Failed" || "$provisioningState" == "Canceled" ]]; then
        echo "Provisioning State: $provisioningState, project provisioning will not work."
        break
    fi

    sleep 30
done