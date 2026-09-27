// The hosted table (ADR 0080, ADR 0082): one container app scaled between zero and one replica, the storage
// account its sessions are recorded into, and the platform's sign-in in front of the lobby.
//
// Deployed into an existing resource group by .github/workflows/deploy.yml; infra/README.md says how the
// group, the deploying identity and the sign-in registration are made once.

@description('Where everything lives. The resource group\'s own region by default.')
param location string = resourceGroup().location

@description('The image to run, as the workflow pushed it: ghcr.io/<owner>/downfall-table:<commit>.')
param image string

@description('The application (client) id of the app registration the platform signs the operator in with.')
param signInClientId string

@description('The object ids of the people who may sign in to the lobby, comma-separated. Players never sign in.')
param operators string

@description('A short word every resource name starts with.')
@minLength(3)
@maxLength(11)
param namePrefix string = 'downfall'

var tenantId = tenant().tenantId
var operatorIds = filter(map(split(operators, ','), id => trim(id)), id => !empty(id))
var suffix = uniqueString(resourceGroup().id)
var containerName = 'playtests'

// The one role the table needs: write and read its own recordings. A built-in role, named by its id.
var storageBlobDataContributor = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

// Where the console lines go. Capped per day so a host that logs in a loop costs cents, not a surprise.
resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${namePrefix}-logs-${suffix}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      dailyQuotaGb: json('0.5')
    }
  }
}

// Blob only (ADR 0080). Reached with the app's identity, never a key: shared-key access is off, so no key
// exists that could leak. Public network access stays on because the app runs on the consumption plan,
// outside any network of ours; what fences the account is that nothing can authenticate to it but the app.
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: take('${namePrefix}${suffix}', 24)
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    publicNetworkAccess: 'Enabled'
    encryption: {
      keySource: 'Microsoft.Storage'
      requireInfrastructureEncryption: true
      services: {
        blob: {
          enabled: true
          keyType: 'Account'
        }
      }
    }
  }
}

// A week to take back a session deleted by mistake: recordings are small, and one is a playtest evening.
resource blobs 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 7
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 7
    }
  }
}

resource playtests 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobs
  name: containerName
  properties: {
    publicAccess: 'None'
  }
}

resource appEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${namePrefix}-env-${suffix}'
  location: location
  properties: {
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

// One replica at most: a seat blocks a thread while a person thinks, and a second replica would hold other
// tables than the ones its players were told about (ADR 0080). Zero when nobody has a page open.
resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-table'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    environmentId: appEnvironment.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
      }
    }
    template: {
      containers: [
        {
          name: 'table'
          image: image
          args: [
            'table'
            '--lobby'
            '--platform-auth'
            '--bind'
            '0.0.0.0'
            '--port'
            '8080'
            '--rules'
            'docs/tabletop/playtest.rules.json'
            '--record'
            '${storage.properties.primaryEndpoints.blob}${containerName}'
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          probes: [
            {
              type: 'Startup'
              tcpSocket: {
                port: 8080
              }
              periodSeconds: 3
              failureThreshold: 20
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
        rules: [
          {
            name: 'http'
            http: {
              metadata: {
                concurrentRequests: '100'
              }
            }
          }
        ]
      }
    }
  }
}

// Scoped to the one account, for the app's own identity and nothing else.
resource recordsSessions 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, app.id, storageBlobDataContributor)
  scope: storage
  properties: {
    principalId: app.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributor)
  }
}

// The operator's door (ADR 0081): the platform signs them in and stamps who they are on every request it
// forwards. Anonymous requests still pass, because a player joins by a code and has no account; only the
// lobby asks for the stamp. The registration has no client secret, so none exists anywhere (ADR 0082), and
// only the principals named may hold a session at all.
resource signIn 'Microsoft.App/containerApps/authConfigs@2024-03-01' = {
  parent: app
  name: 'current'
  properties: {
    platform: {
      enabled: true
    }
    globalValidation: {
      unauthenticatedClientAction: 'AllowAnonymous'
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          clientId: signInClientId
          openIdIssuer: '${environment().authentication.loginEndpoint}${tenantId}/v2.0'
        }
        validation: {
          allowedAudiences: [
            signInClientId
            'api://${signInClientId}'
          ]
          defaultAuthorizationPolicy: {
            allowedPrincipals: {
              identities: operatorIds
            }
          }
        }
      }
    }
  }
}

@description('Where the table answers.')
output url string = 'https://${app.properties.configuration.ingress.fqdn}'

@description('What the sign-in registration must list as its redirect URI, once (infra/README.md).')
output redirectUri string = 'https://${app.properties.configuration.ingress.fqdn}/.auth/login/aad/callback'

@description('Where sessions are recorded.')
output recordings string = '${storage.properties.primaryEndpoints.blob}${containerName}'
