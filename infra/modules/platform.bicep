targetScope = 'resourceGroup'

param location string
param functionAppName string
param planName string
param storageAccountName string
param deploymentContainerName string
param applicationInsightsName string
param logAnalyticsWorkspaceResourceId string
param hostStorageRoleAssignmentName string
param deploymentStorageRoleAssignmentName string
param appInsightsRoleAssignmentName string
param runtimeVersion string
param instanceMemoryMB int
param maximumInstanceCount int
param botMicrosoftAppId string
param botTenantId string
@secure()
param microsoftAppPassword string
param tags object

var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var monitoringMetricsPublisherRoleId = '3913510d-42f4-4e42-8a64-420c390055eb'
var appInsightsHiddenLink = '/subscriptions/${subscription().subscriptionId}/resourceGroups/${resourceGroup().name}/providers/microsoft.insights/components/${applicationInsightsName}'

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    publicNetworkAccess: 'Enabled'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: deploymentContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  tags: tags
  kind: 'functionapp'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
    zoneRedundant: false
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    IngestionMode: 'LogAnalytics'
    WorkspaceResourceId: logAnalyticsWorkspaceResourceId
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  tags: union(tags, {
    'hidden-link: /app-insights-resource-id': appInsightsHiddenLink
  })
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storage.name
        }
        {
          name: 'AzureWebJobsStorage__credential'
          value: 'managedidentity'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'APPLICATIONINSIGHTS_AUTHENTICATION_STRING'
          value: 'Authorization=AAD'
        }
        {
          name: 'MicrosoftAppId'
          value: botMicrosoftAppId
        }
        {
          name: 'MicrosoftAppTenantId'
          value: botTenantId
        }
        {
          name: 'MicrosoftAppPassword'
          value: microsoftAppPassword
        }
      ]
    }
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storage.properties.primaryEndpoints.blob}${deploymentContainer.name}'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: maximumInstanceCount
        instanceMemoryMB: instanceMemoryMB
      }
      runtime: {
        name: 'dotnet-isolated'
        version: runtimeVersion
      }
    }
  }
}

resource hostStorageBlobOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: hostStorageRoleAssignmentName
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwnerRoleId)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource deploymentStorageBlobContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: deploymentStorageRoleAssignmentName
  scope: deploymentContainer
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource appInsightsMetricsPublisher 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: appInsightsRoleAssignmentName
  scope: appInsights
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', monitoringMetricsPublisherRoleId)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output functionAppResourceId string = functionApp.id
output functionAppPrincipalId string = functionApp.identity.principalId
output functionAppDefaultHostName string = functionApp.properties.defaultHostName
output storageAccountResourceId string = storage.id
