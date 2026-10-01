// Shopping Website 的 Azure 基礎設施進入點（targetScope = resourceGroup，部署到 rg-shopping）。
// 不建立任何角色指派：受控識別與資料角色由 infra/bootstrap.sh 預先建立（ADR-0004）。
targetScope = 'resourceGroup'

@description('部署區域')
param location string = resourceGroup().location

@description('SQL 管理員登入名稱（GitHub Environment 變數 SQL_ADMIN_LOGIN）')
param sqlAdminLogin string

@secure()
@description('SQL 管理員密碼（GitHub Environment Secret SQL_ADMIN_PASSWORD，部署時傳入）')
param sqlAdminPassword string

@description('ACS SMTP 使用者名稱；完成 9.4 第 5 步之前保持空字串')
param smtpUserName string = ''

@description('可用性告警收件信箱（GitHub 變數 ALERT_EMAIL）；空字串時告警只顯示在 Azure 入口網站')
param alertEmail string = ''

@description('共用資源標籤')
param tags object = {
  project: 'shopping-website'
  owner: 'jeff.hou'
  purpose: 'demo'
}

// 全域唯一名稱的後綴
var suffix = take(uniqueString(resourceGroup().id), 6)
var databaseName = 'sqldb-shopping'
var storageContainer = 'products'

resource webIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: 'id-shopping-web'
}

resource deployScriptIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: 'id-shopping-deployscript'
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    tags: tags
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    location: location
    tags: tags
    name: 'kv-shopping-${suffix}'
    sqlAdminPassword: sqlAdminPassword
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    location: location
    tags: tags
    serverName: 'sql-shopping-${suffix}'
    databaseName: databaseName
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
  }
}

module sqlUsers 'modules/sql-users.bicep' = {
  name: 'sql-users'
  params: {
    location: location
    tags: tags
    identityId: deployScriptIdentity.id
    keyVaultName: keyVault.outputs.name
    sqlServerFqdn: sql.outputs.fqdn
    databaseName: sql.outputs.databaseName
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    tags: tags
    name: 'stshopping${suffix}'
    containerName: storageContainer
  }
}

module frontDoor 'modules/frontdoor.bicep' = {
  name: 'frontdoor'
  params: {
    tags: tags
    endpointName: 'afd-shopping-${suffix}'
    blobHostName: storage.outputs.blobHostName
  }
}

module email 'modules/email.bicep' = {
  name: 'email'
  params: {
    tags: tags
    suffix: suffix
  }
}

module appService 'modules/appservice.bicep' = {
  name: 'appservice'
  dependsOn: [
    // 使用者與 Key Vault 密碼就緒後才讓 App 啟動，避免 Key Vault 參考解析失敗
    sqlUsers
  ]
  params: {
    location: location
    tags: tags
    appName: 'app-shopping-${suffix}'
    webIdentityId: webIdentity.id
    webIdentityClientId: webIdentity.properties.clientId
    keyVaultName: keyVault.outputs.name
    sqlServerFqdn: sql.outputs.fqdn
    databaseName: sql.outputs.databaseName
    storageBlobEndpoint: storage.outputs.blobEndpoint
    storageContainer: storageContainer
    imageBaseUrl: 'https://${frontDoor.outputs.endpointHostName}'
    smtpFrom: 'DoNotReply@${email.outputs.fromDomain}'
    smtpUserName: smtpUserName
    appInsightsConnectionString: monitoring.outputs.connectionString
    logAnalyticsWorkspaceId: monitoring.outputs.workspaceId
  }
}

module availability 'modules/availability.bicep' = {
  name: 'availability'
  params: {
    location: location
    tags: tags
    appInsightsId: monitoring.outputs.appInsightsId
    appHostName: appService.outputs.hostName
    alertEmail: alertEmail
  }
}

@description('Web App 名稱')
output appName string = appService.outputs.name

@description('Web App 主機名稱')
output appHostName string = appService.outputs.hostName

@description('Blob 主要端點')
output storageBlobEndpoint string = storage.outputs.blobEndpoint

@description('Storage Account 名稱（上傳示範圖片使用）')
output storageAccountName string = 'stshopping${suffix}'

@description('Front Door 端點主機名稱')
output frontDoorEndpoint string = frontDoor.outputs.endpointHostName

@description('SQL 伺服器完整網域名稱')
output sqlServerFqdn string = sql.outputs.fqdn

@description('Key Vault 名稱')
output keyVaultName string = keyVault.outputs.name

@description('寄件網域')
output emailFromDomain string = email.outputs.fromDomain

@description('Communication Service 名稱')
output communicationServiceName string = email.outputs.communicationServiceName

@description('Application Insights 資源 ID')
output appInsightsId string = monitoring.outputs.appInsightsId
