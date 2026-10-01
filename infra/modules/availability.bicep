// 可用性監控（Plan.md §11.2 6-3）：標準可用性測試每 5 分鐘自 3 個位置請求首頁，
// 2 個以上位置失敗即觸發告警。alertEmail 有值時另建 Action Group 寄信；空字串時告警只顯示在 Azure 入口網站。
@description('部署區域（須與 Application Insights 相同）')
param location string

@description('共用資源標籤')
param tags object

@description('Application Insights 資源 ID')
param appInsightsId string

@description('Web App 主機名稱，例：app-shopping-xxxxxx.azurewebsites.net')
param appHostName string

@description('告警收件信箱；空字串時不建立 Action Group')
param alertEmail string = ''

var webTestName = 'avail-shopping-home'
var actionGroupName = 'ag-shopping'

resource webTest 'Microsoft.Insights/webtests@2022-06-15' = {
  name: webTestName
  location: location
  // hidden-link 標籤讓可用性測試顯示在 Application Insights 底下
  tags: union(tags, {
    'hidden-link:${appInsightsId}': 'Resource'
  })
  kind: 'standard'
  properties: {
    SyntheticMonitorId: webTestName
    Name: '首頁可用性'
    Enabled: true
    Frequency: 300
    Timeout: 30
    Kind: 'standard'
    RetryEnabled: true
    Locations: [
      {
        Id: 'apac-hk-hkn-azr' // East Asia
      }
      {
        Id: 'apac-sg-sin-azr' // Southeast Asia
      }
      {
        Id: 'apac-jp-kaw-edge' // Japan East
      }
    ]
    Request: {
      RequestUrl: 'https://${appHostName}/index.aspx'
      HttpVerb: 'GET'
      ParseDependentRequests: false
    }
    ValidationRules: {
      ExpectedHttpStatusCode: 200
      SSLCheck: true
      SSLCertRemainingLifetimeCheck: 7
    }
  }
}

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (!empty(alertEmail)) {
  name: actionGroupName
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'shopping'
    enabled: true
    emailReceivers: [
      {
        name: 'owner'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

resource availabilityAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-shopping-availability'
  location: 'global'
  tags: tags
  properties: {
    description: '首頁可用性測試在 2 個以上位置失敗'
    severity: 1
    enabled: true
    scopes: [
      webTest.id
      appInsightsId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria'
      webTestId: webTest.id
      componentId: appInsightsId
      failedLocationCount: 2
    }
    actions: empty(alertEmail)
      ? []
      : [
          {
            actionGroupId: actionGroup.id
          }
        ]
  }
}

@description('可用性測試資源 ID')
output webTestId string = webTest.id
