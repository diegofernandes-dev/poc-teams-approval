targetScope = 'subscription'

@description('Azure region for the POC resources.')
param location string = 'eastus2'

@description('Resource group that hosts the POC resources.')
param resourceGroupName string = 'rg-ado-teams-poc'

@description('Function App name.')
param functionAppName string = 'func-ado-teams-poc-diegolab'

@description('Flex Consumption plan name.')
param planName string = 'ASP-rgadoteamspoc-9431'

@description('Storage Account name. Must be globally unique.')
param storageAccountName string = 'rgadoteamspoc9a37'

@description('Deployment package container name used by Flex Consumption.')
param deploymentContainerName string = 'app-package-func-ado-teams-poc-diegolab-1f4bef4'

@description('Application Insights resource name.')
param applicationInsightsName string = 'func-ado-teams-poc-diegolab'

@description('Existing Log Analytics Workspace resource ID used by workspace-based Application Insights.')
param logAnalyticsWorkspaceResourceId string

@description('Existing Storage Blob Data Owner role assignment resource name for the Function managed identity.')
param hostStorageRoleAssignmentName string

@description('Existing Storage Blob Data Contributor role assignment resource name for the Function deployment container.')
param deploymentStorageRoleAssignmentName string

@description('Existing Monitoring Metrics Publisher role assignment resource name for Application Insights.')
param appInsightsRoleAssignmentName string

@description('Runtime version used by dotnet-isolated.')
param runtimeVersion string = '10.0'

@allowed([
  512
  2048
  4096
])
@description('Flex Consumption instance memory in MB.')
param instanceMemoryMB int = 512

@description('Maximum number of Flex Consumption instances.')
param maximumInstanceCount int = 100

@description('Azure Bot resource name.')
param botName string = 'bot-ado-teams-poc-diegolab'

@description('Azure Bot display name.')
param botDisplayName string = 'bot-ado-teams-poc-diegolab'

@description('Microsoft App (client) ID of the existing Entra App Registration referenced by the Azure Bot.')
param botMicrosoftAppId string = '5936429a-7889-45c1-983e-d9064aa7ee84'

@description('Microsoft Entra tenant ID for the SingleTenant Azure Bot.')
param botTenantId string = 'e9dbba09-e7a3-42be-9a2c-f82470024e00'

@description('Validated Bot Framework messaging endpoint for the Function App.')
param botMessagingEndpoint string

@description('Bot App Registration client secret. Supply at deploy time only; never commit.')
@secure()
param microsoftAppPassword string

param tags object = {
  project: 'poc-teams-approval'
  environment: 'poc'
}

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

module platform './modules/platform.bicep' = {
  name: 'poc-teams-approval-platform'
  scope: rg
  params: {
    location: location
    functionAppName: functionAppName
    planName: planName
    storageAccountName: storageAccountName
    deploymentContainerName: deploymentContainerName
    applicationInsightsName: applicationInsightsName
    logAnalyticsWorkspaceResourceId: logAnalyticsWorkspaceResourceId
    hostStorageRoleAssignmentName: hostStorageRoleAssignmentName
    deploymentStorageRoleAssignmentName: deploymentStorageRoleAssignmentName
    appInsightsRoleAssignmentName: appInsightsRoleAssignmentName
    runtimeVersion: runtimeVersion
    instanceMemoryMB: instanceMemoryMB
    maximumInstanceCount: maximumInstanceCount
    botMicrosoftAppId: botMicrosoftAppId
    botTenantId: botTenantId
    microsoftAppPassword: microsoftAppPassword
    tags: tags
  }
}

module bot './modules/bot.bicep' = {
  name: 'poc-teams-approval-bot'
  scope: rg
  params: {
    botName: botName
    botDisplayName: botDisplayName
    botMicrosoftAppId: botMicrosoftAppId
    botTenantId: botTenantId
    botMessagingEndpoint: botMessagingEndpoint
    tags: tags
  }
}

output functionAppResourceId string = platform.outputs.functionAppResourceId
output functionAppPrincipalId string = platform.outputs.functionAppPrincipalId
output storageAccountResourceId string = platform.outputs.storageAccountResourceId
output botResourceId string = bot.outputs.botResourceId
