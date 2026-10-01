// Azure SQL：邏輯伺服器（SQL 驗證）、Basic 資料庫與「允許 Azure 服務」防火牆規則。
@description('部署區域')
param location string

@description('共用資源標籤')
param tags object

@description('SQL 邏輯伺服器名稱（全域唯一）')
param serverName string

@description('資料庫名稱')
param databaseName string

@description('SQL 管理員登入名稱')
param adminLogin string

@secure()
@description('SQL 管理員密碼')
param adminPassword string

resource server 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    version: '12.0'
  }
}

// 0.0.0.0 ～ 0.0.0.0 代表「允許 Azure 服務與資源存取此伺服器」：App Service（B1 無 VNet 整合）
// 與 deploymentScript 容器的對外 IP 都不固定。
resource allowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: server
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: server
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
    requestedBackupStorageRedundancy: 'Local'
  }
}

@description('SQL 伺服器完整網域名稱')
output fqdn string = server.properties.fullyQualifiedDomainName

@description('資料庫名稱')
output databaseName string = database.name
