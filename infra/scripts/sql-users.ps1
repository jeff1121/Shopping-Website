# 建立（或更新）資料庫使用者 shopping_app、shopping_migrator。
# 由 infra/modules/sql-users.bicep 的 deploymentScript 以受控識別 id-shopping-deployscript 執行：
#   1. Key Vault 中沒有 sql-app-password／sql-migrator-password 時，產生 32 字元隨機密碼並寫入。
#   2. 以 SQL 管理員連線，使用者不存在就 CREATE USER，存在就 ALTER USER 重設為 Key Vault 中的密碼，再加入角色。
# 可重複執行；不輸出任何密碼。
param(
    [Parameter(Mandatory)] [string] $KeyVaultName,
    [Parameter(Mandatory)] [string] $SqlServerFqdn,
    [Parameter(Mandatory)] [string] $DatabaseName,
    [Parameter(Mandatory)] [string] $AdminLogin
)

$ErrorActionPreference = 'Stop'

# 產生 32 字元密碼：英文大小寫、數字與 -_，確保符合 Azure SQL 複雜度規則，且不含引號或分號
function New-RandomPassword {
    $upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ'
    $lower = 'abcdefghijkmnopqrstuvwxyz'
    $digit = '23456789'
    $all = $upper + $lower + $digit + '-_'
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $bytes = New-Object byte[] 32
    $rng.GetBytes($bytes)
    $chars = for ($i = 0; $i -lt 32; $i++) { $all[$bytes[$i] % $all.Length] }
    $chars[0] = $upper[$bytes[0] % $upper.Length]
    $chars[1] = $lower[$bytes[1] % $lower.Length]
    $chars[2] = $digit[$bytes[2] % $digit.Length]
    return [string](-join $chars)
}

# 讀取 Key Vault secret；不存在時產生並寫入。
# 訊息必須用 Write-Host：Write-Output 會混入函式回傳值，讓密碼變成「訊息＋密碼」的陣列。
function Get-OrCreateSecret([string] $Name) {
    $value = Get-AzKeyVaultSecret -VaultName $KeyVaultName -Name $Name -AsPlainText -ErrorAction SilentlyContinue
    if ([string]::IsNullOrEmpty($value)) {
        $value = New-RandomPassword
        $secure = ConvertTo-SecureString -String $value -AsPlainText -Force
        Set-AzKeyVaultSecret -VaultName $KeyVaultName -Name $Name -SecretValue $secure -ContentType "資料庫使用者密碼（deploymentScript 產生）" | Out-Null
        Write-Host "已產生 Key Vault secret $Name"
    }
    else {
        Write-Host "沿用既有 Key Vault secret $Name"
    }
    return [string]$value
}

$appPassword = Get-OrCreateSecret 'sql-app-password'
$migratorPassword = Get-OrCreateSecret 'sql-migrator-password'

# 建立或更新使用者並加入角色的 T-SQL（密碼只含英數與 -_，可安全放入字串常值）
function Get-UserSql([string] $User, [string] $Password, [string[]] $Roles) {
    $sql = "IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$User') " +
        "CREATE USER [$User] WITH PASSWORD = N'$Password'; " +
        "ELSE ALTER USER [$User] WITH PASSWORD = N'$Password';"
    foreach ($role in $Roles) {
        $sql += " ALTER ROLE [$role] ADD MEMBER [$User];"
    }
    return $sql
}

$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source'] = "tcp:$SqlServerFqdn,1433"
$builder['Initial Catalog'] = $DatabaseName
$builder['User ID'] = $AdminLogin
$builder['Password'] = $env:SQL_ADMIN_PASSWORD
$builder['Encrypt'] = $true
$builder['TrustServerCertificate'] = $false
$builder['Connect Timeout'] = 60

$commands = @(
    (Get-UserSql 'shopping_app' $appPassword @('db_datareader', 'db_datawriter')),
    (Get-UserSql 'shopping_migrator' $migratorPassword @('db_ddladmin', 'db_datareader', 'db_datawriter'))
)

# 新建的資料庫可能還在上線中，連線失敗時重試
for ($attempt = 1; $attempt -le 10; $attempt++) {
    try {
        $connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
        $connection.Open()
        foreach ($text in $commands) {
            $command = $connection.CreateCommand()
            $command.CommandText = $text
            [void] $command.ExecuteNonQuery()
        }
        $connection.Close()
        Write-Output "已建立／更新資料庫使用者 shopping_app、shopping_migrator"
        exit 0
    }
    catch {
        Write-Output "第 $attempt 次連線或執行失敗：$($_.Exception.Message)"
        if ($attempt -eq 10) { throw }
        Start-Sleep -Seconds 30
    }
}
