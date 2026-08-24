targetScope = 'resourceGroup'

param botName string
param botDisplayName string
param botMicrosoftAppId string
param botTenantId string
param botMessagingEndpoint string
param tags object

resource botService 'Microsoft.BotService/botServices@2022-09-15' = {
  name: botName
  location: 'global'
  kind: 'azurebot'
  sku: {
    name: 'F0'
  }
  tags: tags
  properties: {
    displayName: botDisplayName
    endpoint: botMessagingEndpoint
    msaAppId: botMicrosoftAppId
    msaAppTenantId: botTenantId
    msaAppType: 'SingleTenant'
    iconUrl: 'https://docs.botframework.com/static/devportal/client/images/bot-framework-default.png'
    schemaTransformationVersion: '1.3'
    isCmekEnabled: false
    luisAppIds: []
  }
}

resource teamsChannel 'Microsoft.BotService/botServices/channels@2022-09-15' = {
  parent: botService
  name: 'MsTeamsChannel'
  location: 'global'
  properties: {
    channelName: 'MsTeamsChannel'
    properties: {
      isEnabled: true
      acceptedTerms: true
      enableCalling: false
      deploymentEnvironment: 'CommercialDeployment'
      incomingCallRoute: 'graphPma'
    }
  }
}

output botResourceId string = botService.id
output botName string = botService.name
