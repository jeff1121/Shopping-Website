// Front Door Standard：只負責商品圖片，origin 為 Storage 的 Blob 主要端點。
@description('共用資源標籤')
param tags object

@description('Front Door 端點名稱（全域唯一）')
param endpointName string

@description('Blob 主要端點的主機名稱（例：stshoppingxxxx.blob.core.windows.net）')
param blobHostName string

resource profile 'Microsoft.Cdn/profiles@2024-02-01' = {
  name: 'afd-shopping'
  location: 'global'
  tags: tags
  sku: {
    name: 'Standard_AzureFrontDoor'
  }
}

resource endpoint 'Microsoft.Cdn/profiles/afdEndpoints@2024-02-01' = {
  parent: profile
  name: endpointName
  location: 'global'
  tags: tags
  properties: {
    enabledState: 'Enabled'
  }
}

resource originGroup 'Microsoft.Cdn/profiles/originGroups@2024-02-01' = {
  parent: profile
  name: 'og-blob'
  properties: {
    loadBalancingSettings: {
      sampleSize: 4
      successfulSamplesRequired: 3
      additionalLatencyInMilliseconds: 50
    }
    // 只有一個 origin，不需要健康探查（也避免探查流量）
    sessionAffinityState: 'Disabled'
  }
}

resource origin 'Microsoft.Cdn/profiles/originGroups/origins@2024-02-01' = {
  parent: originGroup
  name: 'blob'
  properties: {
    hostName: blobHostName
    originHostHeader: blobHostName
    httpPort: 80
    httpsPort: 443
    priority: 1
    weight: 1000
    enabledState: 'Enabled'
    enforceCertificateNameCheck: true
  }
}

resource route 'Microsoft.Cdn/profiles/afdEndpoints/routes@2024-02-01' = {
  parent: endpoint
  name: 'images'
  dependsOn: [
    origin
  ]
  properties: {
    originGroup: {
      id: originGroup.id
    }
    supportedProtocols: [
      'Http'
      'Https'
    ]
    patternsToMatch: [
      '/*'
    ]
    forwardingProtocol: 'HttpsOnly'
    httpsRedirect: 'Enabled'
    linkToDefaultDomain: 'Enabled'
    enabledState: 'Enabled'
    cacheConfiguration: {
      // 依 origin 的 Cache-Control（上傳時設為 public, max-age=86400）
      queryStringCachingBehavior: 'IgnoreQueryString'
      compressionSettings: {
        isCompressionEnabled: true
        contentTypesToCompress: [
          'image/svg+xml'
        ]
      }
    }
  }
}

@description('Front Door 端點主機名稱（Azure 產生，例：afd-shopping-xxxx-abcd.z01.azurefd.net）')
output endpointHostName string = endpoint.properties.hostName
