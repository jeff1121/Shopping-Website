// Storage：商品圖片容器 products（存取層級 Blob：可讀單一檔案、不可列出清單）。
@description('部署區域')
param location string

@description('共用資源標籤')
param tags object

@description('Storage Account 名稱（全域唯一，3～24 個小寫英數字）')
param name string

@description('商品圖片容器名稱')
param containerName string

resource account 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: true
    // 應用程式與部署流程都以 Entra ID 存取 Blob，不需要帳戶金鑰
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    publicNetworkAccess: 'Enabled'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: account
  name: 'default'
}

resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: containerName
  properties: {
    publicAccess: 'Blob'
  }
}

@description('Blob 主要端點（含結尾斜線）')
output blobEndpoint string = account.properties.primaryEndpoints.blob

@description('Blob 主要端點的主機名稱，作為 Front Door origin')
output blobHostName string = replace(replace(account.properties.primaryEndpoints.blob, 'https://', ''), '/', '')
