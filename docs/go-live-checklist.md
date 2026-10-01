# 轉入正式營運檢查清單（M8）

本清單對應 [Plan.md §13](../Plan.md#13-m8轉入正式營運)。目前 Azure 環境（`rg-shopping`）**仍作為 Demo 環境使用**，
因此會破壞 Demo 的項目（清除示範資料、輪替密碼、部署核准）暫不執行，等確定轉為正式環境時再依序完成。

狀態：✅ 已完成　⏸️ 暫緩（Demo 期間保留）　⬜ 尚未執行

| # | 項目 | 狀態 | 說明 |
| --- | --- | --- | --- |
| 1 | M7 驗收完成 | ✅ | PR #20～#24；Azure 上以 Playwright 驗證登入、購物車、結帳、權限、上傳驗證、錯誤頁與回應標頭 |
| 2 | 清除示範資料與範例圖片 | ⏸️ | 保留供 Demo，見[第 2 步](#2-清除示範資料) |
| 3 | 更換敏感設定 | ⏸️ | 目前為測試帳密，正式環境時更換，見[第 3 步](#3-更換敏感設定) |
| 4 | 重新啟動 App Service | ⏸️ | 隨第 3 步執行 |
| 5 | （選用）ACS 改用自有網域 | ⏸️ | 見[第 5 步](#5-選用acs-改用自有網域) |
| 6 | （選用）升級 App Service／SQL 層級 | ⏸️ | 見[第 6 步](#6-選用升級服務層級) |
| 7 | GitHub Environment `azure` 開啟 Required reviewers | ⏸️ | 開啟後每次部署都需核准，Demo 期間暫不開啟，見[第 7 步](#7-開啟部署核准) |
| 8 | Code scanning 合併保護：High 以上都擋 | ✅ | `main` 分支規則的 CodeQL 門檻為 `high_or_higher`（安全）與 `errors`（品質） |
| 9 | Key Vault、SQL 診斷記錄與 SQL 稽核 | ✅ | 由 Bicep 建立（`keyvault.bicep`、`sql.bicep`），送 Log Analytics `log-shopping`，見[第 9 步](#9-確認稽核記錄) |
| 10 | 更新 README「專案現況」 | ✅ | M1～M7 完成、M8 部分完成 |

> 原則：任何密碼**只**存在 Key Vault 與 GitHub Environment Secret，不得出現在版控、Issue、PR、對話或終端機輸出。
> 以下指令都以管線直接寫入，不會把密碼印出。

## 2. 清除示範資料

會刪除 `demo_customer`、`demo_seller` 帳號及其商品、購物車、訂單；分類保留。migration 已記錄在 `dbo.SchemaVersions`，清除後不會被重新灌入。

1. 以 SQL 管理員執行 [`db/cleanup/remove_demo_data.sql`](../db/cleanup/remove_demo_data.sql)（Azure 入口網站的查詢編輯器，或 `sqlcmd`）。
2. 刪除範例圖片：

   ```bash
   az storage blob delete-batch --account-name <Storage 帳戶> --source products \
     --pattern 'demo_seller/*' --auth-mode login
   ```

3. `deploy.yml` 每次部署都會重新上傳 `Website/img/products/demo_seller/*`，且冒煙測試會檢查首頁的「Demo Smart Watch」與示範圖片。
   清除前需先修改 `deploy.yml` 的上傳步驟與 `.github/scripts/smoke-test.sh` 的檢查項目，否則下次部署會失敗並回滾。

## 3. 更換敏感設定

密碼不可含 `;`、`'`、`"`、`{`、`}`（連線字串以權杖直接代入）。以下以 32 字元英數與 `-_` 為例。

1. **App 與 migrator 帳號**（`shopping_app`、`shopping_migrator`）：在 Key Vault 建立新版本，再重新執行「基礎設施」workflow；
   `infra/scripts/sql-users.ps1` 會以 `ALTER USER` 把資料庫密碼改成 Key Vault 的目前版本。

   ```bash
   for s in sql-app-password sql-migrator-password; do
     openssl rand -base64 48 | tr -dc 'A-Za-z0-9_-' | head -c 32 |
       az keyvault secret set --vault-name kv-shopping-<suffix> --name "$s" --file /dev/stdin -o none
   done
   env -u GH_TOKEN gh workflow run 基礎設施 -R jeff1121/Shopping-Website
   ```

   執行者需要 Key Vault Secrets Officer 角色（ADR-0004 未預設授予個人帳號，需要時再暫時指派）；
   或比照 `infra/smtp-setup.sh` 以 `az rest` 經 ARM 控制平面寫入（Resource Group 的 Contributor 即可）。
2. **SQL 管理員**：更新 GitHub Environment `azure` 的 Secret `SQL_ADMIN_PASSWORD`，再執行「基礎設施」workflow（Bicep 會更新伺服器管理員密碼與 Key Vault `sql-admin-password`）。

   ```bash
   openssl rand -base64 48 | tr -dc 'A-Za-z0-9_-' | head -c 32 |
     env -u GH_TOKEN gh secret set SQL_ADMIN_PASSWORD --env azure -R jeff1121/Shopping-Website
   ```

3. **SMTP client secret**：`ROTATE_SECRET=1 SUBSCRIPTION_ID=<訂用帳戶 ID> ./infra/smtp-setup.sh`（產生新的 Entra client secret 並寫入 Key Vault `smtp-password`）。

## 4. 重新啟動 App Service

Key Vault 參考平時約 24 小時快取，重新啟動時一定會重新讀取：

```bash
az webapp restart -g rg-shopping -n app-shopping-<suffix>
```

重新啟動後手動執行「部署」workflow（或開啟首頁、登入頁、分類頁），確認網站與寄信正常。

## 5. （選用）ACS 改用自有網域

受管網域（`*.azurecomm.net`）寄信量很低。正式營運建議在 Communication Services 加入自有網域、完成 DNS（SPF、DKIM）驗證，
再更新 `email.bicep` 的寄件網域與 App Service 的 `SMTP_FROM`。

## 6. （選用）升級服務層級

App Service 目前為 B1，SQL 為 Basic。正式營運建議 App Service S1 或 P0v3（部署位置、自動調整），SQL S0 以上；
修改 `appservice.bicep`、`sql.bicep` 的 SKU 後經 PR（what-if）合併。

## 7. 開啟部署核准

GitHub → Settings → Environments → `azure` → Required reviewers 加入核准者。開啟後「部署」與「基礎設施」workflow 都會等待核准。

## 9. 確認稽核記錄

記錄送到 Log Analytics `log-shopping`，約有數分鐘延遲：

```kusto
// Key Vault：secret 讀取與變更（含被拒絕的存取）
AzureDiagnostics
| where ResourceProvider == "MICROSOFT.KEYVAULT"
| project TimeGenerated, OperationName, ResultSignature, CallerIPAddress, identity_claim_upn_s

// SQL 稽核：登入成功／失敗與執行的批次
AzureDiagnostics
| where Category == "SQLSecurityAuditEvents"
| project TimeGenerated, action_name_s, succeeded_s, server_principal_name_s, client_ip_s, statement_s

// 應用程式資料庫：錯誤、逾時、封鎖、死結
AzureDiagnostics
| where ResourceProvider == "MICROSOFT.SQL" and Category in ("Errors", "Timeouts", "Blocks", "Deadlocks")
```

## 已知限制

- 忘記密碼寄信：ACS 會拒收示範帳號的 `@example.com`（RFC 2606 保留網域，SMTP 5.1.5），網站顯示「Unable to send the reset email…」。
  以可收信的 Email 註冊新帳號即可驗證完整流程。
- ZAP Baseline 仍有的警示（不阻擋部署）：CSP 含 `'unsafe-inline'`／`'unsafe-eval'`／`https:`（Web Forms 與 Front Door 圖片需要）、
  App Service 的 `ARRAffinitySameSite` Cookie 為 `SameSite=None`（平台工作階段親和性）、未使用 Anti-CSRF 權杖、快取標頭與
  使用者可控制的 HTML 屬性（ViewState 回傳值）。
- 賣家刪除商品只刪資料列，Blob 中的商品圖片不會一併刪除；Storage 停用共用金鑰，清理時需具備 Storage Blob Data Contributor 角色。
- OpenSSF Scorecard 的 Branch-Protection、Code-Review、Maintained 等警示屬 Repo 層級評分（單人維護、Repo 年資），不影響部署。
