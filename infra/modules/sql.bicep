// Azure SQL：邏輯伺服器（SQL 驗證）、Basic 資料庫與「允許 Azure 服務」防火牆規則；
// 伺服器稽核與資料庫診斷記錄送 Log Analytics。
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

@description('Log Analytics 工作區資源 ID（稽核與診斷記錄目的地）')
param logAnalyticsWorkspaceId string

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

// 伺服器稽核：預設稽核動作群組（成功／失敗登入與所有批次）寫入 Azure Monitor；
// 實際送往 Log Analytics 的設定是下方 master 資料庫的診斷設定（SQLSecurityAuditEvents）。
resource auditing 'Microsoft.Sql/servers/auditingSettings@2023-08-01-preview' = {
  parent: server
  name: 'default'
  properties: {
    state: 'Enabled'
    isAzureMonitorTargetEnabled: true
    auditActionsAndGroups: [
      'SUCCESSFUL_DATABASE_AUTHENTICATION_GROUP'
      'FAILED_DATABASE_AUTHENTICATION_GROUP'
      'BATCH_COMPLETED_GROUP'
    ]
  }
}

// master 資料庫由平台建立，只引用不建立；伺服器層級稽核記錄必須經它的診斷設定送出
resource masterDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' existing = {
  parent: server
  name: 'master'
}

resource auditDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: masterDatabase
  name: 'to-log-analytics'
  dependsOn: [
    auditing
  ]
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'SQLSecurityAuditEvents'
        enabled: true
      }
    ]
  }
}

// 應用程式資料庫的診斷記錄：錯誤、逾時、封鎖與死結（Basic 層級支援的類別），方便對照網站錯誤
resource databaseDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: database
  name: 'to-log-analytics'
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'Errors'
        enabled: true
      }
      {
        category: 'Timeouts'
        enabled: true
      }
      {
        category: 'Blocks'
        enabled: true
      }
      {
        category: 'Deadlocks'
        enabled: true
      }
    ]
  }
}

@description('SQL 伺服器完整網域名稱')
output fqdn string = server.properties.fullyQualifiedDomainName

@description('資料庫名稱')
output databaseName string = database.name
