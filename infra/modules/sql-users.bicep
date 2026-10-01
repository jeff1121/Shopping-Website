// deploymentScript：建立資料庫使用者 shopping_app、shopping_migrator，密碼產生後只存在 Key Vault。
// 以預建的受控識別 id-shopping-deployscript 執行（Key Vault Secrets Officer，ADR-0004）。
@description('部署區域')
param location string

@description('共用資源標籤')
param tags object

@description('deploymentScript 使用的使用者指派受控識別資源 ID')
param identityId string

@description('Key Vault 名稱')
param keyVaultName string

@description('SQL 伺服器完整網域名稱')
param sqlServerFqdn string

@description('資料庫名稱')
param databaseName string

@description('SQL 管理員登入名稱')
param adminLogin string

@secure()
@description('SQL 管理員密碼（以安全環境變數傳入腳本）')
param adminPassword string

@description('每次部署都重新執行腳本，確保使用者與 Key Vault 密碼一致')
param forceUpdateTag string = utcNow()

resource script 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'ds-sql-users'
  location: location
  tags: tags
  kind: 'AzurePowerShell'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    azPowerShellVersion: '12.3'
    forceUpdateTag: forceUpdateTag
    retentionInterval: 'PT1H'
    cleanupPreference: 'OnSuccess'
    timeout: 'PT20M'
    scriptContent: loadTextContent('../scripts/sql-users.ps1')
    arguments: '-KeyVaultName ${keyVaultName} -SqlServerFqdn ${sqlServerFqdn} -DatabaseName ${databaseName} -AdminLogin ${adminLogin}'
    environmentVariables: [
      {
        name: 'SQL_ADMIN_PASSWORD'
        secureValue: adminPassword
      }
    ]
  }
}
