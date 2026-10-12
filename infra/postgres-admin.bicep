param serverName string
param principalId string
param principalName string
param principalType 'User' | 'Group' | 'ServicePrincipal'

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2025-08-01' existing = {
  name: serverName
}

resource admin 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2025-08-01' = {
  parent: postgres
  name: principalId
  properties: {
    principalType: principalType
    principalName: principalName
    tenantId: tenant().tenantId
  }
}
