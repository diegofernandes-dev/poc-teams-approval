using './main.bicep'

param location = 'eastus2'
param resourceGroupName = 'rg-ado-teams-poc'
param functionAppName = 'func-ado-teams-poc-diegolab'
param planName = 'ASP-rgadoteamspoc-9431'
param storageAccountName = 'rgadoteamspoc9a37'
param deploymentContainerName = 'app-package-func-ado-teams-poc-diegolab-1f4bef4'
param applicationInsightsName = 'func-ado-teams-poc-diegolab'
param runtimeVersion = '10.0'
param instanceMemoryMB = 512
param maximumInstanceCount = 100
param tags = {
  project: 'poc-teams-approval'
  environment: 'poc'
}
