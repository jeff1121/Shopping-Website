# 預建使用者指派受控識別，Bicep 不做角色指派

Azure 訂用帳戶 BD-CIS-Testing 的 Owner 受組織 ABAC 條件限制，不能指派 Owner、User Access Administrator、Role Based Access Control Administrator，因此 GitHub OIDC 部署身分 `gh-shopping-deploy` 無法取得指派角色的權限，Bicep 也就不能建立角色指派。我們改由一次性腳本 `infra/bootstrap.sh` 預先建立使用者指派受控識別 `id-shopping-web`（App Service 使用）與 `id-shopping-deployscript`（`deploymentScript` 使用），並以執行者自己的權限在 `rg-shopping` 範圍指派 Key Vault 與 Blob 的資料角色；Bicep 只以 `existing` 參照這兩個識別並掛到資源上。

## Considered Options

- 請訂用帳戶管理員授予部署身分附條件的 RBAC Administrator：完全照原計畫，但要等管理員，而且每次重建 Demo 都依賴他人。
- Bicep 不指派角色，每次部署後手動補指派：自動化程度最低，每次重建都要人工介入。
- 自訂含 `roleAssignments/write` 的角色：等於繞過組織刻意設下的限制，不採用。

## Consequences

- App Service 改用使用者指派受控識別：Web App 需設定 `keyVaultReferenceIdentity`，程式的 `DefaultAzureCredential` 需要應用程式設定 `AZURE_CLIENT_ID` 指定識別。
- 角色範圍是 Resource Group 而非個別資源；RG 內只有本專案的 Key Vault 與 Storage，實際效果相同。
- 刪除並重建 `rg-shopping` 時，受控識別與角色指派會一起消失，必須先重跑 `infra/bootstrap.sh` 再部署 Bicep。
