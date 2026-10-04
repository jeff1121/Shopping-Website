// 新站 App Service：Linux B1 方案與 net10.0 Web App。
// 與既有 Windows 方案 asp-shopping 分開。掛上預建的 id-shopping-web，Bicep 不指派角色（ADR-0004）。
@description('部署區域')
param location string

@description('共用資源標籤')
param tags object

@description('Web App 名稱（全域唯一）')
param appName string

@description('id-shopping-web 的資源 ID')
param webIdentityId string

@description('id-shopping-web 的 client ID')
param webIdentityClientId string

@description('Key Vault 名稱')
param keyVaultName string

@description('SQL 伺服器完整網域名稱')
param sqlServerFqdn string

@description('資料庫名稱')
param databaseName string

@description('Blob 主要端點')
param storageBlobEndpoint string

@description('商品圖片容器名稱')
param storageContainer string

@description('圖片對外網址根（Front Door）')
param imageBaseUrl string

@description('寄件者地址')
param smtpFrom string

@description('ACS SMTP 使用者名稱；空字串表示尚未設定 SMTP 帳號')
param smtpUserName string

@description('Application Insights 連線字串')
param appInsightsConnectionString string

@description('Log Analytics 工作區資源 ID')
param logAnalyticsWorkspaceId string

var kvRef = 'VaultName=${keyVaultName};SecretName='

var baseSettings = {
  SQL_SERVER: 'tcp:${sqlServerFqdn},1433'
  SQL_DATABASE: databaseName
  SQL_ENCRYPT: 'True'
  SQL_USER: 'shopping_app'
  SQL_PASSWORD: '@Microsoft.KeyVault(${kvRef}sql-app-password)'
  SQL_MIGRATOR_USER: 'shopping_migrator'
  SQL_MIGRATOR_PASSWORD: '@Microsoft.KeyVault(${kvRef}sql-migrator-password)'
  SMTP_HOST: 'smtp.azurecomm.net'
  SMTP_PORT: '587'
  SMTP_FROM: smtpFrom
  STORAGE_BLOB_ENDPOINT: storageBlobEndpoint
  STORAGE_CONTAINER: storageContainer
  IMAGE_BASE_URL: imageBaseUrl
  AZURE_CLIENT_ID: webIdentityClientId
  APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
  ASPNETCORE_ENVIRONMENT: 'Production'
  WEBSITE_RUN_FROM_PACKAGE: '1'
}

var smtpCredentialSettings = empty(smtpUserName) ? {} : {
  SMTP_USER: smtpUserName
  SMTP_PASSWORD: '@Microsoft.KeyVault(${kvRef}smtp-password)'
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-shopping-mvc'
  location: location
  tags: tags
  sku: {
    name: 'B1'
    tier: 'Basic'
    capacity: 1
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource site 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${webIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: true
    keyVaultReferenceIdentity: webIdentityId
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      appCommandLine: 'dotnet Shopping.Web.dll'
      alwaysOn: true
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
    }
  }
}

resource appSettings 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: site
  name: 'appsettings'
  properties: union(baseSettings, smtpCredentialSettings)
}

resource ftpBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2023-12-01' = {
  parent: site
  name: 'ftp'
  properties: {
    allow: false
  }
}

resource scmBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2023-12-01' = {
  parent: site
  name: 'scm'
  properties: {
    allow: false
  }
}

resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: site
  name: 'to-log-analytics'
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AppServiceAppLogs'
        enabled: true
      }
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
      }
    ]
  }
}

@description('Web App 名稱')
output name string = site.name

@description('Web App 預設主機名稱')
output hostName string = site.properties.defaultHostName
