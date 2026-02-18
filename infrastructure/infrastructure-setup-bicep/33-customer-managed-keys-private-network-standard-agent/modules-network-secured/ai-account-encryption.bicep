param aiFoundryName string
param aiFoundryPrincipal string
param location string
param keyVaultName string
param keyVaultUri string
param keyName string
param keyVersion string

// Reference account post creation, since we must wait for managed identity to be created to give access to CMK key vault
resource existingAccount 'Microsoft.CognitiveServices/accounts@2025-09-01' existing = {
  name: aiFoundryName
}
// Reference the existing Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
}


resource KeyVaultCryptoServiceEncryptionUser 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  name:'e147488a-f6f5-4113-8e2d-b22465e65bf6' // Built-in role for Key Vault Crypto Service Encryption User
  scope: resourceGroup()
}

resource KeyVaultCryptoServiceEncryptionUserassignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(aiFoundryPrincipal, KeyVaultCryptoServiceEncryptionUser.id, keyVault.id)
  properties: {
    principalId: aiFoundryPrincipal
    roleDefinitionId: KeyVaultCryptoServiceEncryptionUser.id
    principalType: 'ServicePrincipal'
  }
}

resource KeyVaultCryptoServiceEncryptioncosmosassignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid('a232010e-820c-4083-83bb-3ace5fc29d0b', KeyVaultCryptoServiceEncryptionUser.id, keyVault.id)
  properties: {
    principalId: 'a232010e-820c-4083-83bb-3ace5fc29d0b'
    roleDefinitionId: KeyVaultCryptoServiceEncryptionUser.id
    principalType: 'ServicePrincipal'
  }
}



// Set customer-managed key encryption on account
resource accountUpdate 'Microsoft.CognitiveServices/accounts@2025-09-01' = {
  name: existingAccount.name
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  properties: {
    // new
    encryption: {
      keySource: 'Microsoft.KeyVault'
      keyVaultProperties: {
        keyVaultUri: keyVaultUri
        keyName: keyName
        keyVersion: keyVersion
      }
    }
    networkAcls: {
      defaultAction: 'Deny'
      virtualNetworkRules: []
      ipRules: []
      bypass:'AzureServices'
    }

    publicNetworkAccess: 'Disabled'
    allowProjectManagement: true
    customSubDomainName: aiFoundryName
    disableLocalAuth: false
  }
  dependsOn: [
    KeyVaultCryptoServiceEncryptionUserassignment
    KeyVaultCryptoServiceEncryptioncosmosassignment
  ]
}
