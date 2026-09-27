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
var subnetName = 'apps'

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

// The network the app runs in, so the storage account can refuse everything else. A virtual network and a
// service endpoint cost nothing; the subnet is the smallest a Container Apps environment on workload
// profiles takes, delegated to it.
resource network 'Microsoft.Network/virtualNetworks@2024-01-01' = {
  name: '${namePrefix}-net-${suffix}'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.80.0.0/23'
      ]
    }
    subnets: [
      {
        name: subnetName
        properties: {
          addressPrefix: '10.80.0.0/27'
          delegations: [
            {
              name: 'container-apps'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
          serviceEndpoints: [
            {
              service: 'Microsoft.Storage'
              locations: [
                location
              ]
            }
          ]
        }
      }
    ]
  }
}

// The subnet by its id, spelled from the network's own id rather than read back from its properties: a
// deployment is validated before anything in it exists, and the storage provider refuses a network rule whose
// subnet id is still an unevaluated read of a network that has not been made yet.
var appsSubnetId = '${network.id}/subnets/${subnetName}'

// Blob only (ADR 0080). Two fences, either of which would hold alone (ADR 0082): the network refuses every
// request that does not come from the app's subnet, and shared-key access is off, so no key exists to leak
// and the only credential that works is the app's identity. An owner who wants to read the recordings from
// elsewhere adds their own address for the occasion (infra/README.md).
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: take('${namePrefix}${suffix}', 24)
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'None'
      virtualNetworkRules: [
        {
          id: appsSubnetId
          action: 'Allow'
        }
      ]
      ipRules: []
    }
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
    vnetConfiguration: {
      infrastructureSubnetId: appsSubnetId
      internal: false
    }
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
  scope: storage
  name: guid(storage.id, app.id, storageBlobDataContributor)
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
