// Key Vault（RBAC 授權模式）；SQL 管理員密碼另存一份備查；稽核記錄（AuditEvent）送 Log Analytics。
// 角色指派由 infra/bootstrap.sh 在 Resource Group 範圍完成（ADR-0004），這裡不做。
@description('部署區域')
param location string

@description('共用資源標籤')
param tags object

@description('Key Vault 名稱（全域唯一）')
param name string

@secure()
@description('SQL 管理員密碼，寫入 secret sql-admin-password')
param sqlAdminPassword string

@description('Log Analytics 工作區資源 ID（診斷記錄目的地）')
param logAnalyticsWorkspaceId string

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
  }
}

resource adminSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'sql-admin-password'
  properties: {
    value: sqlAdminPassword
    contentType: 'SQL 管理員密碼（來自 GitHub Environment Secret SQL_ADMIN_PASSWORD）'
  }
}

// 診斷設定：Key Vault 存取稽核（誰在何時讀取或變更了哪個 secret，含被拒絕的存取）送 Log Analytics（AzureDiagnostics）
resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: vault
  name: 'to-log-analytics'
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        categoryGroup: 'audit'
        enabled: true
      }
    ]
  }
}

@description('Key Vault 名稱')
output name string = vault.name

@description('Key Vault URI')
output uri string = vault.properties.vaultUri
