targetScope = 'resourceGroup'

param webImage string
param apiImage string
param ingestImage string

@description('0 lets web and api scale to zero when idle, with a cold start of a few seconds. Use 1 in demo week.')
param minReplicas int = 0

@description('Same region as Postgres.')
param location string = 'norwayeast'

resource logs 'Microsoft.OperationalInsights/workspaces@2025-07-01' existing = {
  name: 'log-paragrafen'
}

resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' existing = {
  name: 'crparagrafen'
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2025-08-01' existing = {
  name: 'psql-paragrafen-noe'
}

resource foundry 'Microsoft.CognitiveServices/accounts@2026-09-01' existing = {
  name: 'paragrafen-foundry-dev'
}

resource webId 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' existing = {
  name: 'id-paragrafen-web'
}

resource apiId 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' existing = {
  name: 'id-paragrafen-api'
}

resource ingestId 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' existing = {
  name: 'id-paragrafen-ingest'
}

resource environment 'Microsoft.App/managedEnvironments@2026-07-01' = {
  name: 'cae-paragrafen'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [{ name: 'Consumption', workloadProfileType: 'Consumption' }]
  }
}

resource api 'Microsoft.App/containerApps@2026-07-01' = {
  name: 'ca-paragrafen-api'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${apiId.id}': {} }
  }
  properties: {
    environmentId: environment.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
      }
      registries: [{ server: registry.properties.loginServer, identity: apiId.id }]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            {
              name: 'ConnectionStrings__Paragrafen'
              value: 'Host=${postgres.properties.fullyQualifiedDomainName};Database=paragrafen;Username=${apiId.name};SslMode=Require'
            }
            { name: 'Foundry__Endpoint', value: foundry.properties.endpoint }
            { name: 'AZURE_CLIENT_ID', value: apiId.properties.clientId }
          ]
          probes: [
            { type: 'Readiness', httpGet: { path: '/api/health', port: 8080 } }
            { type: 'Liveness', httpGet: { path: '/api/health', port: 8080 } }
          ]
        }
      ]
      scale: { minReplicas: minReplicas, maxReplicas: 2 }
    }
  }
}

resource web 'Microsoft.App/containerApps@2026-07-01' = {
  name: 'ca-paragrafen-web'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${webId.id}': {} }
  }
  properties: {
    environmentId: environment.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 3000
        transport: 'http'
      }
      registries: [{ server: registry.properties.loginServer, identity: webId.id }]
    }
    template: {
      containers: [
        {
          name: 'web'
          image: webImage
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            { name: 'API_URL', value: 'https://${api.properties.configuration.ingress.fqdn}' }
          ]
        }
      ]
      scale: { minReplicas: minReplicas, maxReplicas: 2 }
    }
  }
}

resource ingest 'Microsoft.App/jobs@2026-07-01' = {
  name: 'caj-paragrafen-ingest'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${ingestId.id}': {} }
  }
  properties: {
    environmentId: environment.id
    workloadProfileName: 'Consumption'
    configuration: {
      triggerType: 'Schedule'
      scheduleTriggerConfig: {
        cronExpression: '17 3 * * *'
        parallelism: 1
        replicaCompletionCount: 1
      }
      replicaTimeout: 1800
      replicaRetryLimit: 1
      registries: [{ server: registry.properties.loginServer, identity: ingestId.id }]
    }
    template: {
      containers: [
        {
          name: 'ingest'
          image: ingestImage
          resources: { cpu: json('1'), memory: '2Gi' }
          env: [
            { name: 'PGHOST', value: postgres.properties.fullyQualifiedDomainName }
            { name: 'PGDATABASE', value: 'paragrafen' }
            { name: 'PGUSER', value: ingestId.name }
            { name: 'PGSSLMODE', value: 'require' }
            { name: 'AZURE_CLIENT_ID', value: ingestId.properties.clientId }
          ]
        }
      ]
    }
  }
}

output webUrl string = 'https://${web.properties.configuration.ingress.fqdn}'
output apiInternalUrl string = 'https://${api.properties.configuration.ingress.fqdn}'
