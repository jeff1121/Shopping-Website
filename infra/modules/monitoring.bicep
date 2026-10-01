// 監控：Log Analytics 工作區與 Workspace-based Application Insights。
@description('部署區域')
param location string

@description('共用資源標籤')
param tags object

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-shopping'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-shopping'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
  }
}

@description('Application Insights 資源 ID（可用性測試掛在此資源下）')
output appInsightsId string = appInsights.id

@description('Application Insights 連線字串（非機密，作為應用程式設定）')
output connectionString string = appInsights.properties.ConnectionString
