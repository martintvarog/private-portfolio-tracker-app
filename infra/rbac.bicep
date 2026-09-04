// Who may do what. Deployed BY A HUMAN (your own account), never by the pipeline:
//   az deployment group create -g rg-portfoliotracker --template-file infra/rbac.bicep
// The pipeline's Contributor role cannot write role assignments — by Azure design — so
// permissions can only change here, by hand, with a git history.

// `existing` = look these up, don't create or manage them. main.bicep owns them.
resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: 'acrportfoliotrackerapp'
}
resource app 'Microsoft.App/containerApps@2024-03-01' existing = {
  name: 'ca-portfoliotracker'
}
resource deployIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: 'id-github-deploy'
}

// Built-in role IDs — identical in every Azure tenant.
var roleAcrPull = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
var roleAcrPush = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8311e382-0749-4cb8-b61a-304f252e45ec')
var roleContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c')

// The app's own identity may pull images.
resource appAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, app.id, roleAcrPull)
  scope: acr
  properties: {
    principalId: app.identity.principalId
    roleDefinitionId: roleAcrPull
    principalType: 'ServicePrincipal'
  }
}

// The pipeline may push images.
resource deployAcrPush 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, deployIdentity.id, roleAcrPush)
  scope: acr
  properties: {
    principalId: deployIdentity.properties.principalId
    roleDefinitionId: roleAcrPush
    principalType: 'ServicePrincipal'
  }
}

// The pipeline may deploy main.bicep: Contributor on the whole resource group.
// (Was: Contributor on the app only. Widened so `az deployment group create` works.)
resource deployRgContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, deployIdentity.id, roleContributor)
  scope: resourceGroup()
  properties: {
    principalId: deployIdentity.properties.principalId
    roleDefinitionId: roleContributor
    principalType: 'ServicePrincipal'
  }
}
