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
    tags: tags
  }
}

output functionAppResourceId string = platform.outputs.functionAppResourceId
output functionAppPrincipalId string = platform.outputs.functionAppPrincipalId
output storageAccountResourceId string = platform.outputs.storageAccountResourceId
