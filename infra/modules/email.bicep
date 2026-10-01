// Azure Communication Services Email：Email 服務、Azure 受管網域與連結的 Communication Service。
@description('共用資源標籤')
param tags object

@description('名稱後綴（uniqueString）')
param suffix string

resource emailService 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: 'ecs-shopping-${suffix}'
  location: 'global'
  tags: tags
  properties: {
    dataLocation: 'Asia Pacific'
  }
}

resource managedDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: emailService
  name: 'AzureManagedDomain'
  location: 'global'
  tags: tags
  properties: {
    domainManagement: 'AzureManaged'
    userEngagementTracking: 'Disabled'
  }
}

resource communicationService 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: 'acs-shopping-${suffix}'
  location: 'global'
  tags: tags
  properties: {
    dataLocation: 'Asia Pacific'
    linkedDomains: [
      managedDomain.id
    ]
  }
}

@description('Communication Service 名稱（ACS SMTP 帳號設定使用）')
output communicationServiceName string = communicationService.name

@description('寄件網域（例：xxxxxxxx.azurecomm.net）')
output fromDomain string = managedDomain.properties.mailFromSenderDomain
