// 部署參數；不含任何密碼。sqlAdminPassword 由 infra.yml 從 GitHub Environment Secret 傳入，
// sqlAdminLogin 由 GitHub Environment 變數 SQL_ADMIN_LOGIN 傳入。
using './main.bicep'

param sqlAdminLogin = readEnvironmentVariable('SQL_ADMIN_LOGIN', 'sqladminshop')
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD', '')
param smtpUserName = readEnvironmentVariable('SMTP_USER_NAME', '')
