param location string = resourceGroup().location

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: 'acrportfoliotrackerapp'
  location: location
  sku: {
    name: 'Basic'
  }
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'workspace-rgportfoliotrackerk171' // name Azure auto-generated; renaming would replace the workspace and its logs
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource env 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-portfoliotracker'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

@description('Full image reference to run. CD will pass the SHA-tagged image; this default is the current one.')
param image string = 'acrportfoliotrackerapp.azurecr.io/portfoliotrackerapp:daef300edd21c8f2fecc663b2b5b804c12d72fa4'

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-portfoliotracker'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: env.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'Auto'
      }
      registries: [
        {
          server: acr.properties.loginServer
          identity: 'system'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'ca-portfoliotracker'
          image: image
          resources: {
            cpu: json('0.5') // Bicep has no decimal literals
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}

@description('OIDC subject GitHub presents for pushes to main. Includes the numeric owner/repo IDs.')
param githubSubject string = 'repo:martintvarog@63610399/private-portfolio-tracker-app@1329731821:ref:refs/heads/main'

resource deployIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-github-deploy'
  location: location
}

resource githubFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: deployIdentity
  name: 'github-main'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: githubSubject
    audiences: [ 'api://AzureADTokenExchange' ]
  }
}

@description('Fire when more than this many syncs end Unavailable within the 15-minute window. 5 = more than casual retrying by one user; revisit when it fires falsely or stays silent during a real Fio outage.')
param unavailableAlertThreshold int = 5

resource syncUnavailableAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'alert-sync-unavailable'
  location: location
  properties: {
    displayName: 'Sync outcomes: Unavailable spike (bank outage?)'
    description: 'More than ${unavailableAlertThreshold} syncs ended Unavailable in 15 min. InvalidCredential is excluded on purpose: one expired token is not an incident.'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    scopes: [ logAnalytics.id ]
    criteria: {
      allOf: [
        {
          query: '''
ContainerAppConsoleLogs_CL
| extend j = parse_json(Log_s)
| where tostring(j.Category) == "PortfolioTrackerApp.Connectors.Logging.LoggingConnector"
| where tostring(j.State.Status) == "Unavailable"
'''
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: unavailableAlertThreshold
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    autoMitigate: true
  }
}

output appUrl string = 'https://${app.properties.configuration.ingress.fqdn}'
output deployClientId string = deployIdentity.properties.clientId
