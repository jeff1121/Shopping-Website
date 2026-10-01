// App Service：Windows B1 方案與 Web App（.NET Framework 4.8 執行階段，相容 4.7.2）。
// 掛上預建的使用者指派受控識別 id-shopping-web，用來解析 Key Vault 參考與存取 Blob（ADR-0004）。
@description('部署區域')
param location string

@description('共用資源標籤')
param tags object

@description('Web App 名稱（全域唯一）')
param appName string

@description('id-shopping-web 的資源 ID')
param webIdentityId string

@description('id-shopping-web 的 client ID，供 DefaultAzureCredential 選擇此識別')
param webIdentityClientId string

@description('Key Vault 名稱（組成 Key Vault 參考）')
param keyVaultName string

@description('SQL 伺服器完整網域名稱')
param sqlServerFqdn string

@description('資料庫名稱')
param databaseName string

@description('Blob 主要端點')
param storageBlobEndpoint string

@description('商品圖片容器名稱')
param storageContainer string

@description('圖片對外網址根（Front Door，https://主機名稱，不含結尾斜線）')
param imageBaseUrl string

@description('寄件者地址')
param smtpFrom string

@description('ACS SMTP 使用者名稱；空字串表示尚未完成 SMTP 帳號設定，不設定 SMTP_USER／SMTP_PASSWORD')
param smtpUserName string

@description('Application Insights 連線字串')
param appInsightsConnectionString string

// 組出 Key Vault 參考字串
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
  // Windows App Service 的 Application Insights 免程式碼代理程式
  ApplicationInsightsAgent_EXTENSION_VERSION: '~2'
  WEBSITE_RUN_FROM_PACKAGE: '1'
}

var smtpCredentialSettings = empty(smtpUserName) ? {} : {
  SMTP_USER: smtpUserName
  SMTP_PASSWORD: '@Microsoft.KeyVault(${kvRef}smtp-password)'
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-shopping'
  location: location
  tags: tags
  sku: {
    name: 'B1'
    tier: 'Basic'
    capacity: 1
  }
  properties: {
    reserved: false
  }
}

resource site 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  tags: tags
  kind: 'app'
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
      netFrameworkVersion: 'v4.0'
      metadata: [
        {
          name: 'CURRENT_STACK'
          value: 'dotnet'
        }
      ]
      alwaysOn: true
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
      use32BitWorkerProcess: true
    }
  }
}

resource appSettings 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: site
  name: 'appsettings'
  properties: union(baseSettings, smtpCredentialSettings)
}

// 停用 FTP 與 SCM 的基本驗證；部署改用 Entra ID（OIDC）
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

@description('Web App 名稱')
output name string = site.name

@description('Web App 預設主機名稱')
output hostName string = site.properties.defaultHostName
