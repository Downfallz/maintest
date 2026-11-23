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

@description('The player accounts that are the operator once signed in (ADR 0175), by their ids (acct_...), comma-separated. Empty, only the operators above reach the admin panel.')
param adminAccounts string = ''

@description('The relay the pages and processes hear what moved through (ADR 0165): Free_F1 (20 connections, 20 000 messages a day), Standard_S1 (1 000 connections a unit), or empty for none, and the pages read the API\'s own streams. Opted into once the bootstrap has granted its role.')
@allowed([
  ''
  'Free_F1'
  'Standard_S1'
])
param relaySku string = ''

@description('The table\'s own host name behind Cloudflare, e.g. play.downfallinstinct.com (ADR 0175). Empty, the table answers at the platform\'s address only.')
param customDomain string = ''

@description('The name the Cloudflare Origin CA certificate was uploaded under to the Container Apps environment, once, by hand (infra/README.md). Read only when customDomain is set.')
param originCertificateName string = 'cloudflare-origin'

@description('Whether the API answers Cloudflare\'s addresses alone (ADR 0175), so nobody reaches it around Cloudflare at the platform\'s address. Turned on once the custom domain serves.')
param onlyCloudflare bool = false

@description('A short word every resource name starts with.')
@minLength(3)
@maxLength(11)
param namePrefix string = 'downfall'

@secure()
@description('Where the alerts go, by email and to the Azure mobile app signed in with the same address (ADR 0186). Empty, no alert and no budget are deployed.')
param alertEmail string = ''

@description('The monthly budget of the resource group, in the billing currency (ADR 0186): an alert at 80 % and 100 % spent and at 100 % forecast. Nothing is stopped.')
@minValue(1)
param budgetAmount int = 5

@description('The first day of the month the budget starts, yyyy-MM-01, set once (the TABLE_BUDGET_START variable): Azure takes a past start only within its own month, and a start that moves may not be accepted on an existing budget. Empty, no budget is deployed.')
param budgetStart string = ''

@description('The principal id of the promo identity (ADR 0206, the AZURE_PROMO_PRINCIPAL_ID variable): it alone writes the promo container. Empty, nobody writes it but its readers.')
param promoWriter string = ''

var tenantId = tenant().tenantId
var operatorIds = filter(map(split(operators, ','), id => trim(id)), id => !empty(id))

// The players' sign-in (ADR 0122): the host checks Google's ID token itself, so the id is all it takes.
var accountArgs = empty(googleClientId) ? [] : [
  '--accounts'
  'google'
  '--google-client-id'
  googleClientId
]
// The admin accounts (ADR 0175), on the API alone: it is the one process that serves the panel, and only with players
// signing in at all.
var adminArgs = empty(googleClientId) || empty(trim(adminAccounts)) ? [] : [
  '--admins'
  trim(adminAccounts)
]
var suffix = uniqueString(resourceGroup().id)
var containerName = 'playtests'
// The promotion's films and pictures (ADR 0206): not the recordings, so the processes of the table never see them.
var promoContainerName = 'promo'
var rulesFile = 'docs/tabletop/playtest.rules.json'
var recordings = '${storage.properties.primaryEndpoints.blob}${containerName}'

// The queues the bots' turns go through (ADR 0159, phase 5): made by the processes as they open them.
var turnsQueue = 'agent-turns'
var poisonQueue = 'agent-turns-poison'

// The queues the notices go through (ADR 0203): the API and the worker send them, and the worker wakes for them, so a
// notice outlives the API's replica.
var noticesQueue = 'notices'
var noticesPoisonQueue = 'notices-poison'

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
var roleDefinitions = 'Microsoft.Authorization/roleDefinitions'

// What each process says through the relay and signs a page's address with (ADR 0165).
var webPubSubServiceOwner = '12cf5a90-567b-43ae-8102-96cf46c7d9b4'
var relayOn = !empty(relaySku)
var relayName = '${namePrefix}-relay-${suffix}'
// The service's own host name, made from its name: nothing is read off a resource that may not be deployed.
var relayHost = '${relayName}.webpubsub.azure.com'
var relayId = resourceId('Microsoft.SignalRService/webPubSub', relayName)
var relayStandard = relaySku == 'Standard_S1'
var relayProperties = {
  disableLocalAuth: true
  disableAadAuth: false
  tls: {
    clientCertEnabled: false
  }
}
var relayRules = {
  defaultAction: 'Deny'
  publicNetwork: {
    allow: [
      'ClientConnection'
      'RESTAPI'
    ]
  }
}
var relayEnv = relayOn ? [
  {
    name: 'DOWNFALL_RELAY'
    value: 'https://${relayHost}'
  }
] : []

// Cloudflare's IPv4 ranges (https://www.cloudflare.com/ips-v4), the only addresses the API answers when
// onlyCloudflare is on (ADR 0175). The platform's ingress takes IPv4 only, and Cloudflare reaches it over IPv4.
// They change rarely; compare them with the published list before turning the fence on, and when Cloudflare
// announces a change.
var cloudflareRanges = [
  '173.245.48.0/20'
  '103.21.244.0/22'
  '103.22.200.0/22'
  '103.31.4.0/22'
  '141.101.64.0/18'
  '108.162.192.0/18'
  '190.93.240.0/20'
  '188.114.96.0/20'
  '197.234.240.0/22'
  '198.41.128.0/17'
  '162.158.0.0/15'
  '104.16.0.0/13'
  '104.24.0.0/14'
  '172.64.0.0/13'
  '131.0.72.0/22'
]
var ingressRestrictions = onlyCloudflare ? map(range(0, length(cloudflareRanges)), i => {
  name: 'cloudflare-${i}'
  description: 'Cloudflare (ADR 0175)'
  ipAddressRange: cloudflareRanges[i]
  action: 'Allow'
}) : []
var domainOn = !empty(customDomain)

// The alerts (ADR 0186): what the platform already measures, read where it is kept, so nothing calls the API from
// outside and keeps a replica awake. Named here, so the rules and the budget name the group without reading a
// resource that may not be deployed.
var alertsOn = !empty(alertEmail)
// The budget is a switch of its own: it needs a start set once, and some subscriptions take no budget at all.
var budgetOn = alertsOn && !empty(budgetStart)
var alertGroupName = '${namePrefix}-alerts'
var alertGroupId = resourceId('Microsoft.Insights/actionGroups', alertGroupName)
// The worker writes "deliveries, onto" as it puts a turn on the poison queue, "tries, onto" as it puts a notice
// there (ADR 0203), and the job this line when a pass fails (AgentTurnWorker, NoticeWorker, TableTend): the console
// lines are the one place each is said. Not "given up on after", which every host's banner says as it starts
// (studio/ops.test.js holds them).
var givenUpQuery = 'ContainerAppConsoleLogs_CL | where Log_s has "deliveries, onto" or Log_s has "tries, onto"'
var tendFailedQuery = 'ContainerAppConsoleLogs_CL | where Log_s has "The tables could not be tended"'
var logRules = [
  {
    name: '${namePrefix}-turn-given-up'
    description: 'A worker gave up on a bot\'s turn or a notice and put it on a poison queue (ADR 0186, 0203).'
    query: givenUpQuery
  }
  {
    name: '${namePrefix}-tend-failed'
    description: 'A tending pass failed (ADR 0186).'
    query: tendFailedQuery
  }
]
// The rules deployed: none without the alerts' address. A list, not a condition, so each rule's identity is read
// where the rule surely exists.
var activeLogRules = alertsOn ? logRules : []
// What the log alerts read the console lines with, on the workspace alone.
var logAnalyticsReader = '73c42c96-874c-492b-b04d-ab87d138a893'

// Where the console lines go. Capped per day so a host that logs in a loop costs cents, not a surprise.
resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${namePrefix}-logs-${suffix}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    // The public privacy policy promises this period (showcase/privacy.mjs); showcase/showcase.test.js holds them equal.
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
// The public privacy policy promises these days (showcase/privacy.mjs); showcase/showcase.test.js holds them equal.
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

// The promotion's media (ADR 0206, the promotion plan's question 3): the films, sheets and generated pictures a
// post shows, too large for the repository, which keeps their hashes. Private like the recordings: the promo
// identity writes it, and the operators read it in the portal, by identity, as every credential here works.
resource promo 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobs
  name: promoContainerName
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
    tier: relayStandard ? 'Standard' : 'Free'
    capacity: 1
  }
  identity: {
    type: 'SystemAssigned'
  }
  // The pages connect from anywhere a player is, so the public endpoint stays. On Standard it is narrowed to what
  // the table uses: the pages' and processes' connections, and the REST calls that send and sign. Free refuses any
  // network rule ("Free tier doesn't support setting network ACLs"), so there the property is not sent at all.
  properties: union(relayProperties, relayStandard ? { networkACLs: relayRules } : {})
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
        customDomains: domainOn ? [
          {
            name: customDomain
            // The Cloudflare Origin CA certificate, uploaded once by hand (ADR 0175), so its private key is in no
            // repository, variable or secret of the workflow.
            certificateId: resourceId('Microsoft.App/managedEnvironments/certificates', appEnvironment.name, originCertificateName)
            bindingType: 'SniEnabled'
          }
        ] : []
        ipSecurityRestrictions: ingressRestrictions
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
          ], accountArgs, adminArgs)
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
// the API does, and the notices' two (ADR 0203). No ingress. Zero replicas while no queue holds anything, one
// when one does: a second would only take turns the first is already taking. It closes a match its turn ended, so it keeps the result
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
          {
            name: 'notices-owed'
            custom: {
              type: 'azure-queue'
              metadata: {
                accountName: storage.name
                queueName: noticesQueue
                queueLength: '1'
              }
              identity: 'system'
            }
          }
          {
            name: 'notices-given-up-on'
            custom: {
              type: 'azure-queue'
              metadata: {
                accountName: storage.name
                queueName: noticesPoisonQueue
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
      // A game it times out tells its players how it ended (ADR 0169).
      secrets: pushSecrets
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
          env: concat(pushEnv, relayEnv)
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
    roleDefinitionId: subscriptionResourceId(roleDefinitions, role)
  }
}]

resource workerRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for role in recordsIndexesAndQueues: {
  scope: storage
  name: guid(storage.id, worker.id, role)
  properties: {
    principalId: worker.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(roleDefinitions, role)
  }
}]

resource tendRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for role in recordsAndIndexes: {
  scope: storage
  name: guid(storage.id, tend.id, role)
  properties: {
    principalId: tend.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(roleDefinitions, role)
  }
}]

// On the promo container alone, never the account: the promo identity, which a workflow of `main` signs in as,
// writes the media; the operators read and tidy them. Neither reaches a recording, an index or a queue.
resource promoWriterRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(promoWriter)) {
  scope: promo
  name: guid(promo.id, promoWriter, storageBlobDataContributor)
  properties: {
    principalId: promoWriter
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(roleDefinitions, storageBlobDataContributor)
  }
}

resource promoReaderRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for operator in operatorIds: {
  scope: promo
  name: guid(promo.id, operator, storageBlobDataContributor)
  properties: {
    principalId: operator
    principalType: 'User'
    roleDefinitionId: subscriptionResourceId(roleDefinitions, storageBlobDataContributor)
  }
}]

// Each process says through the relay what it moved, and the API signs the pages' addresses (ADR 0165).
resource apiRelayRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (relayOn) {
  scope: relay
  name: guid(relayId, app.id, webPubSubServiceOwner)
  properties: {
    principalId: app.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(roleDefinitions, webPubSubServiceOwner)
  }
}

resource workerRelayRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (relayOn) {
  scope: relay
  name: guid(relayId, worker.id, webPubSubServiceOwner)
  properties: {
    principalId: worker.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(roleDefinitions, webPubSubServiceOwner)
  }
}

resource tendRelayRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (relayOn) {
  scope: relay
  name: guid(relayId, tend.id, webPubSubServiceOwner)
  properties: {
    principalId: tend.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(roleDefinitions, webPubSubServiceOwner)
  }
}

// Where the alerts go: one email and one notice on the Azure mobile app, to the same address (ADR 0186).
resource alertGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (alertsOn) {
  name: alertGroupName
  location: 'global'
  properties: {
    groupShortName: take(namePrefix, 12)
    enabled: true
    emailReceivers: [
      {
        name: 'owner-email'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
    azureAppPushReceivers: [
      {
        name: 'owner-app'
        emailAddress: alertEmail
      }
    ]
  }
}

// The API answering 5xx: more than five in a quarter of an hour. An app at zero replicas reports nothing, so a
// quiet night raises nothing.
resource serverErrors 'Microsoft.Insights/metricAlerts@2018-03-01' = if (alertsOn) {
  name: '${namePrefix}-table-5xx'
  location: 'global'
  dependsOn: [
    alertGroup
  ]
  properties: {
    description: 'The table answered more than five 5xx in 15 minutes (ADR 0186).'
    severity: 2
    enabled: true
    scopes: [
      app.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'server-errors'
          metricNamespace: 'Microsoft.App/containerApps'
          metricName: 'Requests'
          dimensions: [
            {
              name: 'statusCodeCategory'
              operator: 'Include'
              values: [
                '5xx'
              ]
            }
          ]
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Total'
        }
      ]
    }
    autoMitigate: true
    actions: [
      {
        actionGroupId: alertGroupId
      }
    ]
  }
}

// A replica of the API or of the worker restarting past twice: a crash loop, not a deployment, which starts a new
// revision instead. Read as the highest count a replica reports, which holds whether the platform counts per
// interval or so far; a replica lives hours at most, since both apps scale to zero.
resource restarts 'Microsoft.Insights/metricAlerts@2018-03-01' = [for process in [
  '${namePrefix}-table'
  '${namePrefix}-worker'
]: if (alertsOn) {
  name: '${process}-restarts'
  location: 'global'
  dependsOn: [
    alertGroup
    app
    worker
  ]
  properties: {
    description: 'A replica of ${process} restarted more than twice (ADR 0186).'
    severity: 2
    enabled: true
    scopes: [
      resourceId('Microsoft.App/containerApps', process)
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'restarts'
          metricNamespace: 'Microsoft.App/containerApps'
          metricName: 'RestartCount'
          operator: 'GreaterThan'
          threshold: 2
          timeAggregation: 'Maximum'
        }
      ]
    }
    autoMitigate: true
    actions: [
      {
        actionGroupId: alertGroupId
      }
    ]
  }
}]

// A turn given up on, and a tending pass that failed: read from the console lines once an hour, which the job's
// own rhythm is. The table the query reads exists only once a line was written, so the query is not checked at
// deployment.
resource logAlerts 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = [for rule in activeLogRules: {
  name: rule.name
  location: location
  // Each rule reads the console lines as its own identity, which holds Log Analytics Reader on the workspace alone,
  // rather than as whoever deployed it.
  identity: {
    type: 'SystemAssigned'
  }
  dependsOn: [
    alertGroup
  ]
  properties: {
    displayName: rule.name
    description: rule.description
    severity: 2
    enabled: true
    scopes: [
      logs.id
    ]
    evaluationFrequency: 'PT1H'
    windowSize: 'PT1H'
    skipQueryValidation: true
    criteria: {
      allOf: [
        {
          query: rule.query
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    autoMitigate: false
    actions: {
      actionGroups: [
        alertGroupId
      ]
    }
  }
}]

resource logAlertRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for (rule, i) in activeLogRules: {
  scope: logs
  name: guid(logs.id, rule.name, logAnalyticsReader)
  properties: {
    principalId: logAlerts[i].identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(roleDefinitions, logAnalyticsReader)
  }
}]

// The month's spending on the resource group (ADR 0186): told at 80 % and 100 % spent, and when the forecast
// passes the budget. A budget stops nothing. The type requires an address of its own, so its notice arrives by email twice: from the
// budget, and from the group with the app's notice.
resource budget 'Microsoft.Consumption/budgets@2023-05-01' = if (budgetOn) {
  name: '${namePrefix}-monthly'
  dependsOn: [
    alertGroup
  ]
  properties: {
    category: 'Cost'
    amount: budgetAmount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: budgetStart
    }
    notifications: {
      actual80: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: [
          alertEmail
        ]
        contactGroups: [
          alertGroupId
        ]
      }
      actual100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Actual'
        contactEmails: [
          alertEmail
        ]
        contactGroups: [
          alertGroupId
        ]
      }
      forecast100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [
          alertEmail
        ]
        contactGroups: [
          alertGroupId
        ]
      }
    }
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

@description('Where the table answers: its own domain when it has one (ADR 0175), the platform\'s address otherwise.')
output url string = domainOn ? 'https://${customDomain}' : 'https://${app.properties.configuration.ingress.fqdn}'

@description('The platform\'s own address, which Cloudflare\'s record points at (ADR 0175).')
output platformUrl string = 'https://${app.properties.configuration.ingress.fqdn}'

@description('What the sign-in registration must list as its redirect URI, once (infra/README.md).')
output redirectUri string = domainOn ? 'https://${customDomain}/.auth/login/aad/callback' : 'https://${app.properties.configuration.ingress.fqdn}/.auth/login/aad/callback'

@description('The value of the asuid TXT record that proves the custom domain is ours (ADR 0175).')
output customDomainVerificationId string = appEnvironment.properties.customDomainConfiguration.customDomainVerificationId

@description('Where sessions are recorded.')
output recordings string = recordings

@description('The promo container (ADR 0206): where the films and pictures of the posts are written.')
output promo string = '${storage.properties.primaryEndpoints.blob}${promoContainerName}'

@description('The app that takes the bots\' turns.')
output worker string = worker.name

@description('The job that tends the tables every hour.')
output tendJob string = tend.name

@description('The relay the pages connect to, or nothing when there is none (ADR 0165).')
output relayHostName string = relayOn ? relayHost : ''
