// ---- GSA Proxy Deployment Module ----
// Deploys a GSA AI Connector proxy VM from the Azure Marketplace offering
// with managed identity, IP forwarding, and NSG rules.
// Also creates a UDR on the agent subnet to route 0/0 traffic through the proxy,
// with exceptions for required Azure service tags.

@description('Azure region for all resources')
param location string

@description('Name prefix for resources')
param name string

@description('Name of the existing virtual network')
param vnetName string

@description('Resource ID of the GSA proxy subnet')
param gsaProxySubnetId string

@description('Name of the agent subnet to apply the UDR to')
param agentSubnetName string

@description('VM size for the GSA proxy')
param vmSize string = 'Standard_D2s_v3'

@description('Admin username for the proxy VM')
param adminUsername string = 'azureuser'

@description('SSH public key for the proxy VM')
@secure()
param sshPublicKey string

@description('Tags to apply to all resources')
param tags object = {}

// ---- Variables ----
var gsaProxyNsgName = '${name}-gsa-proxy-nsg'
var gsaProxyNicName = '${name}-gsa-proxy-nic'
var gsaProxyVmName = '${name}-gsa-proxy-vm'
var gsaProxyIpConfigName = '${name}-gsa-proxy-ipconfig'
var agentSubnetUdrName = '${name}-agent-udr'

// ---- Reference existing VNet ----
resource vnet 'Microsoft.Network/virtualNetworks@2024-01-01' existing = {
  name: vnetName
}

// ---- NSG for GSA Proxy ----
// Inbound: Allow all VNet traffic (agent subnet sends traffic here via UDR)
// Outbound: Allow VNet and Internet traffic (core proxy function)
resource gsaProxyNsg 'Microsoft.Network/networkSecurityGroups@2024-01-01' = {
  name: gsaProxyNsgName
  location: location
  tags: tags
  properties: {
    securityRules: [
      {
        name: 'AllowVNetInbound'
        properties: {
          priority: 100
          direction: 'Inbound'
          access: 'Allow'
          protocol: '*'
          sourceAddressPrefix: 'VirtualNetwork'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '*'
        }
      }
      {
        name: 'AllowAzureLoadBalancerInbound'
        properties: {
          priority: 110
          direction: 'Inbound'
          access: 'Allow'
          protocol: '*'
          sourceAddressPrefix: 'AzureLoadBalancer'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '*'
        }
      }
      {
        name: 'DenyAllOtherInbound'
        properties: {
          priority: 4096
          direction: 'Inbound'
          access: 'Deny'
          protocol: '*'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '*'
        }
      }
      {
        name: 'AllowVNetOutbound'
        properties: {
          priority: 100
          direction: 'Outbound'
          access: 'Allow'
          protocol: '*'
          sourceAddressPrefix: 'VirtualNetwork'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '*'
        }
      }
      {
        name: 'AllowInternetOutbound'
        properties: {
          priority: 110
          direction: 'Outbound'
          access: 'Allow'
          protocol: '*'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: 'Internet'
          destinationPortRange: '*'
        }
      }
    ]
  }
}

// ---- NIC for GSA Proxy VM (IP Forwarding enabled) ----
resource gsaProxyNic 'Microsoft.Network/networkInterfaces@2024-01-01' = {
  name: gsaProxyNicName
  location: location
  tags: tags
  properties: {
    enableIPForwarding: true
    ipConfigurations: [
      {
        name: gsaProxyIpConfigName
        properties: {
          subnet: {
            id: gsaProxySubnetId
          }
          privateIPAllocationMethod: 'Dynamic'
        }
      }
    ]
    networkSecurityGroup: {
      id: gsaProxyNsg.id
    }
  }
}

// ---- GSA Proxy VM (Marketplace Image with Managed Identity) ----
resource gsaProxyVm 'Microsoft.Compute/virtualMachines@2024-07-01' = {
  name: gsaProxyVmName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  plan: {
    name: 'gsaaiconnectorplan1'
    publisher: 'microsoftcorporation1687208452115'
    product: 'gsaaiconnector1-preview'
  }
  properties: {
    hardwareProfile: {
      vmSize: vmSize
    }
    osProfile: {
      computerName: take(gsaProxyVmName, 15)
      adminUsername: adminUsername
      linuxConfiguration: {
        disablePasswordAuthentication: true
        ssh: {
          publicKeys: [
            {
              path: '/home/${adminUsername}/.ssh/authorized_keys'
              keyData: sshPublicKey
            }
          ]
        }
      }
    }
    storageProfile: {
      imageReference: {
        publisher: 'microsoftcorporation1687208452115'
        offer: 'gsaaiconnector1-preview'
        sku: 'gsaaiconnectorplan1'
        version: 'latest'
      }
      osDisk: {
        createOption: 'FromImage'
        managedDisk: {
          storageAccountType: 'Premium_LRS'
        }
      }
    }
    networkProfile: {
      networkInterfaces: [
        {
          id: gsaProxyNic.id
          properties: {
            primary: true
          }
        }
      ]
    }
  }
}

// ---- UDR for Agent Subnet ----
// Routes all internet-bound traffic (0.0.0.0/0) through the GSA proxy,
// with explicit exceptions for required Azure service tags.
resource agentSubnetUdr 'Microsoft.Network/routeTables@2024-01-01' = {
  name: agentSubnetUdrName
  location: location
  tags: tags
  properties: {
    disableBgpRoutePropagation: false
    routes: [
      {
        name: 'DefaultToProxy'
        properties: {
          addressPrefix: '0.0.0.0/0'
          nextHopType: 'VirtualAppliance'
          nextHopIpAddress: gsaProxyNic.properties.ipConfigurations[0].properties.privateIPAddress
        }
      }
      {
        name: 'AllowAzureActiveDirectory'
        properties: {
          addressPrefix: 'AzureActiveDirectory'
          nextHopType: 'Internet'
        }
      }
      {
        name: 'AllowAzureResourceManager'
        properties: {
          addressPrefix: 'AzureResourceManager'
          nextHopType: 'Internet'
        }
      }
      {
        name: 'AllowAzureMonitor'
        properties: {
          addressPrefix: 'AzureMonitor'
          nextHopType: 'Internet'
        }
      }
      {
        name: 'AllowGuestAndHybridManagement'
        properties: {
          addressPrefix: 'GuestAndHybridManagement'
          nextHopType: 'Internet'
        }
      }
      {
        name: 'AllowAzureContainerRegistry'
        properties: {
          addressPrefix: 'AzureContainerRegistry'
          nextHopType: 'Internet'
        }
      }
      {
        name: 'AllowAzureKeyVault'
        properties: {
          addressPrefix: 'AzureKeyVault'
          nextHopType: 'Internet'
        }
      }
      {
        name: 'AllowStorage'
        properties: {
          addressPrefix: 'Storage'
          nextHopType: 'Internet'
        }
      }
      {
        name: 'AllowAzureFrontDoorFirstParty'
        properties: {
          addressPrefix: 'AzureFrontDoor.FirstParty'
          nextHopType: 'Internet'
        }
      }
      {
        name: 'AllowContainerAppsManagement'
        properties: {
          addressPrefix: 'ContainerAppsManagement'
          nextHopType: 'Internet'
        }
      }
    ]
  }
}

// ---- Apply UDR to Agent Subnet ----
resource agentSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-01-01' existing = {
  parent: vnet
  name: agentSubnetName
}

module agentSubnetUdrAssociation 'agent-subnet-udr-association.bicep' = {
  name: 'agent-subnet-udr-association'
  params: {
    vnetName: vnetName
    agentSubnetName: agentSubnetName
    agentSubnetAddressPrefix: agentSubnet.properties.addressPrefix
    existingNsgId: contains(agentSubnet.properties, 'networkSecurityGroup') && agentSubnet.properties.networkSecurityGroup != null ? agentSubnet.properties.networkSecurityGroup.id : ''
    existingDelegations: contains(agentSubnet.properties, 'delegations') ? agentSubnet.properties.delegations : []
    routeTableId: agentSubnetUdr.id
  }
}

// ---- Outputs ----
output gsaProxyPrivateIp string = gsaProxyNic.properties.ipConfigurations[0].properties.privateIPAddress
output gsaProxyVmId string = gsaProxyVm.id
output gsaProxyVmPrincipalId string = gsaProxyVm.identity.principalId
output routeTableId string = agentSubnetUdr.id
