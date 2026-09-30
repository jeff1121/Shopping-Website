# 在 App 啟動時執行資料庫 migration，並分成兩個 SQL 帳號

資料庫結構由 App 在 `Application_Start` 以 DbUp 套用，不在 GitHub Actions 中執行。這樣就不需要為 IP 不固定的 GitHub runner 開放 SQL 防火牆。代價是 App 需要具 DDL 權限的帳號，而現有程式有 SQL Injection，所以拆成兩個資料庫使用者：`shopping_migrator`（DDL + 讀寫）只在啟動 migration 時使用，`shopping_app`（僅讀寫）處理所有請求。兩者都由 Bicep `deploymentScript` 建立，密碼自動產生並直接寫入 Key Vault，不經過人手。

## Considered Options

- GitHub Actions 執行 DbUp 並暫時開放 runner IP：App 不需要 DDL 權限，但 workflow 需要 SQL 管理權限與防火牆操作。
- 人工以 SSMS 執行：無法追蹤版本，容易漏跑。
