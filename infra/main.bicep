// Infrastructure for the portfolio tracker — everything in rg-portfoliotracker.
// Dry run:  az deployment group what-if -g rg-portfoliotracker --template-file infra/main.bicep
// Deploy:   az deployment group create  -g rg-portfoliotracker --template-file infra/main.bicep

param location string = resourceGroup().location

// ---------- Container registry: where the pipeline pushes images, where the app pulls them ----------
resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: 'acrportfoliotrackerapp'
  location: location
  sku: {
    name: 'Basic'
  }
}

// ---------- Log Analytics workspace: where container logs end up ----------
// Azure created this silently on Monday when the Container Apps environment was made.
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'workspace-rgportfoliotrackerk171'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018' // pay per GB ingested; first 5 GB/month free
    }
    retentionInDays: 30
  }
}

// ---------- Container Apps environment: the shared boundary (network + logs) apps live in ----------
resource env 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-portfoliotracker'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId        // read from the workspace above
        sharedKey: logAnalytics.listKeys().primarySharedKey   // fetched at deploy time, never stored in the file
      }
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'   // serverless: pay per second of use, scales to zero
      }
    ]
  }
}

// ---------- The app itself ----------
@description('Full image reference to run. CD will pass the SHA-tagged image; this default is the current one.')
param image string = 'acrportfoliotrackerapp.azurecr.io/portfoliotrackerapp:daef300edd21c8f2fecc663b2b5b804c12d72fa4'

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-portfoliotracker'
  location: location
  identity: {
    type: 'SystemAssigned' // the app gets its own Azure identity, used to pull from the registry
  }
  properties: {
    managedEnvironmentId: env.id       // lives inside the environment above
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'    // new version replaces old at 100%, once healthy
      ingress: {
        external: true                 // public URL
        targetPort: 8080               // where the container listens (ASPNETCORE_HTTP_PORTS)
        transport: 'Auto'
      }
      registries: [
        {
          server: acr.properties.loginServer   // acrportfoliotrackerapp.azurecr.io
          identity: 'system'                   // pull with the app's own identity — no password
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'ca-portfoliotracker'
          image: image
          resources: {
            cpu: json('0.5')   // Bicep has no decimal literals; json() yields a number
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: 0   // scale to zero when idle
        maxReplicas: 1
      }
    }
  }
}


// ---------- CI/CD identity: who GitHub Actions is when it talks to Azure ----------
@description('OIDC subject GitHub presents for pushes to main. Includes the numeric owner/repo IDs.')
param githubSubject string = 'repo:martintvarog@63610399/private-portfolio-tracker-app@1329731821:ref:refs/heads/main'

resource deployIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-github-deploy'
  location: location
}

// Trust rule (authentication): a GitHub-signed token for exactly this repo+branch IS this identity.
resource githubFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: deployIdentity
  name: 'github-main'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: githubSubject
    audiences: [ 'api://AzureADTokenExchange' ]
  }
}

// Role assignments live in rbac.bicep — deployed by a human, never by the pipeline.

// ---------- Outputs: values the outside world needs after a deploy ----------
output appUrl string = 'https://${app.properties.configuration.ingress.fqdn}'
output deployClientId string = deployIdentity.properties.clientId
