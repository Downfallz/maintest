// The hosted table (ADR 0080, ADR 0082, ADR 0162): the API app scaled between zero and one replica, the worker
// app that takes the bots' turns, scaled on its queue, the hourly job that tends the tables, the storage account
// all three record into and read, and the platform's sign-in in front of the lobby.
//
// Deployed into an existing resource group by .github/workflows/deploy.yml; infra/README.md says how the
// group, the deploying identity and the sign-in registration are made once.

@description('Where everything lives. The resource group\'s own region by default.')
param location string = resourceGroup().location

@description('The image to run, as the workflow pushed it: ghcr.io/<owner>/downfall-table:<commit>.')
param image string

@description('The application (client) id of the app registration the platform signs the operator in with.')
param signInClientId string

@secure()
@description('The table\'s VAPID key pair, the JSON `push-keys` prints (ADR 0118). Empty, the table sends no notices.')
param pushKeys string = ''

@description('The object ids of the people who may sign in to the admin panel, comma-separated.')
param operators string

@description('The Google OAuth client id players sign in with (ADR 0122). Public, not a secret. Empty, players do not sign in.')
param googleClientId string = ''

@description('The relay the pages and processes hear what moved through (ADR 0165): Free_F1 (20 connections, 20 000 messages a day), Standard_S1 (1 000 connections a unit), or empty for none, and the pages read the API\'s own streams. Opted into once the bootstrap has granted its role.')
@allowed([
  ''
  'Free_F1'
  'Standard_S1'
])
param relaySku string = ''

@description('A short word every resource name starts with.')
@minLength(3)
@maxLength(11)
param namePrefix string = 'downfall'

var tenantId = tenant().tenantId
var operatorIds = filter(map(split(operators, ','), id => trim(id)), id => !empty(id))

// The players' sign-in (ADR 0122): the host checks Google's ID token itself, so the id is all it takes.
var accountArgs = empty(googleClientId) ? [] : [
  '--accounts'
  'google'
  '--google-client-id'
  googleClientId
]
var suffix = uniqueString(resourceGroup().id)
var containerName = 'playtests'
var rulesFile = 'docs/tabletop/playtest.rules.json'
var recordings = '${storage.properties.primaryEndpoints.blob}${containerName}'

// The queues the bots' turns go through (ADR 0159, phase 5): made by the processes as they open them.
var turnsQueue = 'agent-turns'
var poisonQueue = 'agent-turns-poison'

// The push keys as a secret of each app that sends notices, read by the host as a variable (ADR 0118).
var pushSecrets = empty(pushKeys) ? [] : [
  {
    name: 'push-keys'
    value: pushKeys
  }
]
var pushEnv = empty(pushKeys) ? [] : [
  {
    name: 'DOWNFALL_PUSH_KEYS'
    secretRef: 'push-keys'
  }
]

// The roles the table's processes need: write and read the recordings, the index that finds them (ADR 0161),
// and the queues the bots' turns go through (ADR 0162). Built-in roles, named by their ids.
var storageBlobDataContributor = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var storageTableDataContributor = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
var storageQueueDataContributor = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var recordsAndIndexes = [
  storageBlobDataContributor
  storageTableDataContributor
]
var recordsIndexesAndQueues = concat(recordsAndIndexes, [storageQueueDataContributor])

// What each process says through the relay and signs a page's address with (ADR 0165).
var webPubSubServiceOwner = '12cf5a90-567b-43ae-8102-96cf46c7d9b4'
var relayOn = !empty(relaySku)
var relayName = '${namePrefix}-relay-${suffix}'
// The service's own host name, made from its name: nothing is read off a resource that may not be deployed.
var relayHost = '${relayName}.webpubsub.azure.com'
var relayEnv = relayOn ? [
  {
    name: 'DOWNFALL_RELAY'
    value: 'https://${relayHost}'
  }
] : []

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

// Blob only (ADR 0080). One fence, the one that names who may read (ADR 0160): shared-key access is off, so
// no key exists to leak, public blob access is off, and the only credential that works is an identity
// holding a role on the account, which is the app's. The network is open: closing it to all but the app's
// subnet put the environment in a virtual network, and with it a load balancer billed every hour.
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
      defaultAction: 'Allow'
      bypass: 'None'
      virtualNetworkRules: []
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

// The relay (ADR 0165): it holds the pages' connections, so a page is told what moved whichever process moved it
// and whether or not the API is awake. Reached by identity alone, as the storage account is (ADR 0160): local auth
// is off, so no access key exists to leak, and a page connects with an address the API signs for its own group.
resource relay 'Microsoft.SignalRService/webPubSub@2024-03-01' = if (relayOn) {
  name: relayName
  location: location
  sku: {
    name: relayOn ? relaySku : 'Free_F1'
    tier: relaySku == 'Standard_S1' ? 'Standard' : 'Free'
    capacity: 1
  }
  properties: {
    disableLocalAuth: true
    disableAadAuth: false
    publicNetworkAccess: 'Enabled'
    tls: {
      clientCertEnabled: false
    }
  }
}

// On the platform's own network, not one of ours (ADR 0160): an environment in a virtual network brings a
// load balancer and a public address into the subscription, billed whether or not a replica runs.
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

// The API (ADR 0162): one replica at most, zero when nobody has a page open. Its bots' turns go on the queue for
// the worker, and the hourly job tends its tables, so it answers requests and streams, and nothing else.
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
      // The push keys are a secret of the app, read by the table as a variable (ADR 0118); none when empty.
      secrets: pushSecrets
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
          args: concat([
            'table'
            '--lobby'
            '--platform-auth'
            '--bind'
            '0.0.0.0'
            '--port'
            '8080'
            '--rules'
            rulesFile
            '--record'
            recordings
            '--turns'
            'worker'
            '--tended'
          ], accountArgs)
          env: concat(pushEnv, relayEnv)
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

// The worker (ADR 0162): the turn worker and the dead-letter worker, over the same store, picking tables up as
// the API does. No ingress. Zero replicas while neither queue holds anything, one when either does: a second
// would only take turns the first is already taking. It closes a match its turn ended, so it keeps the result
// and tells the accounts as the API would, with the same sign-in and the same keys.
resource worker 'Microsoft.App/containerApps@2025-01-01' = {
  name: '${namePrefix}-worker'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    environmentId: appEnvironment.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      secrets: pushSecrets
    }
    template: {
      containers: [
        {
          name: 'worker'
          image: image
          args: concat([
            'table-worker'
            '--rules'
            rulesFile
            '--record'
            recordings
          ], accountArgs)
          env: concat(pushEnv, relayEnv)
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
        // Read with the app's own identity: the account takes no key (ADR 0160).
        rules: [
          {
            name: 'turns-owed'
            custom: {
              type: 'azure-queue'
              metadata: {
                accountName: storage.name
                queueName: turnsQueue
                queueLength: '1'
              }
              identity: 'system'
            }
          }
          {
            name: 'turns-given-up-on'
            custom: {
              type: 'azure-queue'
              metadata: {
                accountName: storage.name
                queueName: poisonQueue
                queueLength: '1'
              }
              identity: 'system'
            }
          }
        ]
      }
    }
  }
}

// The tending job (ADR 0162): one pass of the sweep and of the invitations' expiry, every hour, and it exits.
// The tables it picks up take no bot turn, and it tells nobody anything, so it holds no keys.
resource tend 'Microsoft.App/jobs@2024-03-01' = {
  name: '${namePrefix}-tend'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    environmentId: appEnvironment.id
    workloadProfileName: 'Consumption'
    configuration: {
      triggerType: 'Schedule'
      replicaTimeout: 900
      replicaRetryLimit: 1
      scheduleTriggerConfig: {
        cronExpression: '17 * * * *'
        parallelism: 1
        replicaCompletionCount: 1
      }
    }
    template: {
      containers: [
        {
          name: 'tend'
          image: image
          args: concat([
            'table-tend'
            '--rules'
            rulesFile
            '--record'
            recordings
          ], accountArgs)
          env: relayEnv
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
    }
  }
}

// Scoped to the one account, for each process's own identity and nothing else: the API and the worker read and
// write the recordings, the index and the queues; the job, the recordings and the index.
resource apiRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for role in recordsIndexesAndQueues: {
  scope: storage
  name: guid(storage.id, app.id, role)
  properties: {
    principalId: app.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', role)
  }
}]

resource workerRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for role in recordsIndexesAndQueues: {
  scope: storage
  name: guid(storage.id, worker.id, role)
  properties: {
    principalId: worker.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', role)
  }
}]

resource tendRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for role in recordsAndIndexes: {
  scope: storage
  name: guid(storage.id, tend.id, role)
  properties: {
    principalId: tend.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', role)
  }
}]

// Each process says through the relay what it moved, and the API signs the pages' addresses (ADR 0165).
var relayId = resourceId('Microsoft.SignalRService/webPubSub', relayName)

resource apiRelayRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (relayOn) {
  scope: relay
  name: guid(relayId, app.id, webPubSubServiceOwner)
  properties: {
    principalId: app.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', webPubSubServiceOwner)
  }
}

resource workerRelayRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (relayOn) {
  scope: relay
  name: guid(relayId, worker.id, webPubSubServiceOwner)
  properties: {
    principalId: worker.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', webPubSubServiceOwner)
  }
}

resource tendRelayRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (relayOn) {
  scope: relay
  name: guid(relayId, tend.id, webPubSubServiceOwner)
  properties: {
    principalId: tend.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', webPubSubServiceOwner)
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
output recordings string = recordings

@description('The app that takes the bots\' turns.')
output worker string = worker.name

@description('The job that tends the tables every hour.')
output tendJob string = tend.name

@description('The relay the pages connect to, or nothing when there is none (ADR 0165).')
output relayHostName string = relayOn ? relayHost : ''
