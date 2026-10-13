targetScope = 'resourceGroup'

@description('Team members: get Foundry access for local development and are Entra admins on Postgres.')
param developers { name: string, objectId: string }[]

@description('Public IPs allowed through the Postgres firewall, in addition to Azure services.')
param allowedIps { name: string, address: string }[] = []

@description('Sweden Central had no B1ms capacity in any zone on 2026-10-12.')
param postgresLocation string = 'norwayeast'

@description('Availability zone for Postgres. Change it if the region reports RegionalAllocationFailed.')
param postgresZone '1' | '2' | '3' = '1'

@description('GitHub OIDC subject prefix in the immutable format: owner@id/repo@id.')
param githubRepo string = 'paragrafen@340029239/paragrafen@1410162442'
param location string = resourceGroup().location

var models = [
  { name: 'gpt-6-luna', format: 'OpenAI', version: '2026-09-22', capacity: 100 }
  { name: 'mistral-small-2503', format: 'Mistral AI', version: '1', capacity: 100 }
]

var roles = {
  acrPull: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
  acrPush: '8311e382-0749-4cb8-b61a-304f252e45ec'
  cognitiveServicesUser: 'a97b65f3-24c7-4388-baec-2e87135dc908'
  contributor: 'b24988ac-6180-42a0-ab88-20f7382dd24c'
}

func roleId(id string) string => subscriptionResourceId('Microsoft.Authorization/roleDefinitions', id)

resource webId 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-paragrafen-web'
  location: location
}

resource apiId 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-paragrafen-api'
  location: location
}

resource ingestId 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-paragrafen-ingest'
  location: location
}

resource githubId 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-paragrafen-github'
  location: location
}

resource githubMain 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2024-11-30' = {
  parent: githubId
  name: 'github-main'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${githubRepo}:ref:refs/heads/main'
    audiences: ['api://AzureADTokenExchange']
  }
}

resource logs 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
  name: 'log-paragrafen'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
    workspaceCapping: { dailyQuotaGb: 1 }
  }
}

resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' = {
  name: 'crparagrafen'
  location: location
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false
    anonymousPullEnabled: false
  }
}

resource foundry 'Microsoft.CognitiveServices/accounts@2026-09-01' = {
  name: 'paragrafen-foundry-dev'
  location: location
  kind: 'AIServices'
  sku: { name: 'S0' }
  identity: { type: 'SystemAssigned' }
  properties: {
    customSubDomainName: 'paragrafen-foundry-dev'
    publicNetworkAccess: 'Enabled'
    allowProjectManagement: true
  }
}

@batchSize(1)
resource deployments 'Microsoft.CognitiveServices/accounts/deployments@2026-09-01' = [
  for model in models: {
    parent: foundry
    name: model.name
    sku: { name: 'GlobalStandard', capacity: model.capacity }
    properties: {
      model: { format: model.format, name: model.name, version: model.version }
      versionUpgradeOption: 'NoAutoUpgrade'
      raiPolicyName: 'Microsoft.DefaultV2'
    }
  }
]

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2025-08-01' = {
  name: 'psql-paragrafen-noe'
  location: postgresLocation
  sku: { name: 'Standard_B1ms', tier: 'Burstable' }
  properties: {
    version: '18'
    availabilityZone: postgresZone
    storage: { storageSizeGB: 32, autoGrow: 'Disabled' }
    backup: { backupRetentionDays: 7, geoRedundantBackup: 'Disabled' }
    highAvailability: { mode: 'Disabled' }
    network: { publicNetworkAccess: 'Enabled' }
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Disabled'
      tenantId: tenant().tenantId
    }
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2025-08-01' = {
  parent: postgres
  name: 'paragrafen'
}

resource extensions 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2025-08-01' = {
  parent: postgres
  name: 'azure.extensions'
  properties: { value: 'VECTOR', source: 'user-override' }
  dependsOn: [database]
}

resource allowAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2025-08-01' = {
  parent: postgres
  name: 'AllowAllAzureServicesAndResourcesWithinAzureIps'
  properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
  dependsOn: [extensions]
}

@batchSize(1)
resource allowIps 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2025-08-01' = [
  for ip in allowedIps: {
    parent: postgres
    name: ip.name
    properties: { startIpAddress: ip.address, endIpAddress: ip.address }
    dependsOn: [allowAzure]
  }
]

@batchSize(1)
resource developerAdmins 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2025-08-01' = [
  for dev in developers: {
    parent: postgres
    name: dev.objectId
    properties: {
      principalType: 'User'
      principalName: dev.name
      tenantId: tenant().tenantId
    }
    dependsOn: [allowIps]
  }
]

module githubAdmin 'postgres-admin.bicep' = {
  name: 'postgres-admin-github'
  params: {
    serverName: postgres.name
    principalId: githubId.properties.principalId
    principalName: githubId.name
    principalType: 'ServicePrincipal'
  }
  dependsOn: [developerAdmins]
}

resource webAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, webId.id, roles.acrPull)
  properties: {
    roleDefinitionId: roleId(roles.acrPull)
    principalId: webId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource apiAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, apiId.id, roles.acrPull)
  properties: {
    roleDefinitionId: roleId(roles.acrPull)
    principalId: apiId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource ingestAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, ingestId.id, roles.acrPull)
  properties: {
    roleDefinitionId: roleId(roles.acrPull)
    principalId: ingestId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource githubAcrPush 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, githubId.id, roles.acrPush)
  properties: {
    roleDefinitionId: roleId(roles.acrPush)
    principalId: githubId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource githubContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, githubId.id, roles.contributor)
  properties: {
    roleDefinitionId: roleId(roles.contributor)
    principalId: githubId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource apiFoundryUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: foundry
  name: guid(foundry.id, apiId.id, roles.cognitiveServicesUser)
  properties: {
    roleDefinitionId: roleId(roles.cognitiveServicesUser)
    principalId: apiId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource developerFoundryUsers 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for dev in developers: {
    scope: foundry
    name: guid(foundry.id, dev.objectId, roles.cognitiveServicesUser)
    properties: {
      roleDefinitionId: roleId(roles.cognitiveServicesUser)
      principalId: dev.objectId
      principalType: 'User'
    }
  }
]

output registryLoginServer string = registry.properties.loginServer
output postgresHost string = postgres.properties.fullyQualifiedDomainName
output foundryEndpoint string = foundry.properties.endpoint
output logAnalyticsId string = logs.id
output identities object = {
  web: { id: webId.id, clientId: webId.properties.clientId }
  api: { id: apiId.id, clientId: apiId.properties.clientId }
  ingest: { id: ingestId.id, clientId: ingestId.properties.clientId }
  github: { clientId: githubId.properties.clientId }
}
