using './main.bicep'

param location = 'eastus2'
param resourceGroupName = 'rg-ado-teams-poc'
param functionAppName = 'func-ado-teams-poc-diegolab'
param planName = 'ASP-rgadoteamspoc-9431'
param storageAccountName = 'rgadoteamspoc9a37'
param deploymentContainerName = 'app-package-func-ado-teams-poc-diegolab-1f4bef4'
param applicationInsightsName = 'func-ado-teams-poc-diegolab'
param logAnalyticsWorkspaceResourceId = '/subscriptions/e979b0ce-3200-4e2c-9741-bfb368aadf25/resourceGroups/DefaultResourceGroup-EUS2/providers/Microsoft.OperationalInsights/workspaces/DefaultWorkspace-e979b0ce-3200-4e2c-9741-bfb368aadf25-EUS2'
param hostStorageRoleAssignmentName = 'ac5dabde-0f09-58c2-b7f0-e7b5186c9961'
param deploymentStorageRoleAssignmentName = 'a83330cc-1a44-5356-bc7a-ec1ed00a8eae'
param appInsightsRoleAssignmentName = '74d3277c-6b9f-5dbd-a6b6-7d826a724d02'
param runtimeVersion = '10.0'
param instanceMemoryMB = 512
param maximumInstanceCount = 100
param tags = {
  project: 'poc-teams-approval'
  environment: 'poc'
}
