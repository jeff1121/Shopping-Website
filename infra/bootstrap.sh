#!/usr/bin/env bash
# Azure 與 GitHub 一次性初始化腳本（Plan.md §9.4 第 1～4、6 步）。
#
# 用途：建立 Resource Group、GitHub OIDC 部署身分、預建受控識別與角色指派，
#       並設定 GitHub Environment `azure`。可重複執行：已存在的項目會略過。
#
# 前置條件：
#   - 已安裝並登入 Azure CLI（az login）與 GitHub CLI（gh auth login，需 repo admin 權限；
#     若環境變數 GH_TOKEN 權限不足，請以 `env -u GH_TOKEN ./infra/bootstrap.sh` 執行）。
#   - 在目標訂用帳戶具 Owner（或 Contributor + 可指派一般角色的權限）。
#     本腳本「不」指派 Owner、User Access Administrator、RBAC Administrator 等高權限角色，
#     因此在 Owner 受 ABAC 條件限制的訂用帳戶也可執行。
#
# 用法（所有參數皆可用環境變數覆寫）：
#   SUBSCRIPTION_ID=<訂用帳戶 ID> ./infra/bootstrap.sh
#
# 安全：SQL 管理員密碼只在 GitHub Secret 尚未存在時於本機產生，
#       直接以管線寫入 `gh secret set`，不會輸出到畫面或寫入檔案。

set -euo pipefail

SUBSCRIPTION_ID="${SUBSCRIPTION_ID:?請設定 SUBSCRIPTION_ID}"
LOCATION="${LOCATION:-eastasia}"
RESOURCE_GROUP="${RESOURCE_GROUP:-rg-shopping}"
TAGS="${TAGS:-project=shopping-website owner=jeff.hou purpose=demo}"
DEPLOY_APP_NAME="${DEPLOY_APP_NAME:-gh-shopping-deploy}"
GITHUB_REPO="${GITHUB_REPO:-jeff1121/Shopping-Website}"
GITHUB_ENV_NAME="${GITHUB_ENV_NAME:-azure}"
WEB_IDENTITY="${WEB_IDENTITY:-id-shopping-web}"
DEPLOYSCRIPT_IDENTITY="${DEPLOYSCRIPT_IDENTITY:-id-shopping-deployscript}"
SQL_ADMIN_LOGIN="${SQL_ADMIN_LOGIN:-sqladminshop}"
PROVIDERS=(Microsoft.Web Microsoft.Sql Microsoft.KeyVault Microsoft.Storage Microsoft.Cdn
  Microsoft.Communication Microsoft.Insights Microsoft.OperationalInsights
  Microsoft.ManagedIdentity Microsoft.ContainerInstance)

# 輸出步驟標題。
step() { printf '\n==> %s\n' "$*"; }

# 在指定範圍指派角色；已存在相同指派時略過。
# 參數：$1 主體 Object ID、$2 角色名稱、$3 範圍。
assign_role() {
  local principal="$1" role="$2" scope="$3" existing
  existing=$(az role assignment list --assignee "$principal" --role "$role" --scope "$scope" --query "length(@)" -o tsv)
  if [[ "$existing" == "0" ]]; then
    az role assignment create --assignee-object-id "$principal" --assignee-principal-type ServicePrincipal \
      --role "$role" --scope "$scope" -o none
    echo "已指派：$role"
  else
    echo "已存在：$role"
  fi
}

step "切換訂用帳戶"
az account set --subscription "$SUBSCRIPTION_ID"
TENANT_ID=$(az account show --query tenantId -o tsv)
echo "Tenant：$TENANT_ID"

step "註冊 Resource Provider"
for p in "${PROVIDERS[@]}"; do
  if [[ "$(az provider show -n "$p" --query registrationState -o tsv)" != "Registered" ]]; then
    az provider register -n "$p" --wait
  fi
  echo "${p}：Registered"
done

step "建立 Resource Group ${RESOURCE_GROUP}（${LOCATION}）"
# shellcheck disable=SC2086 # TAGS 需拆成多個 key=value 參數
az group create -n "$RESOURCE_GROUP" -l "$LOCATION" --tags $TAGS -o none
RG_SCOPE=$(az group show -n "$RESOURCE_GROUP" --query id -o tsv)

step "建立部署身分 ${DEPLOY_APP_NAME}（App Registration + Service Principal）"
APP_ID=$(az ad app list --display-name "$DEPLOY_APP_NAME" --query "[0].appId" -o tsv)
if [[ -z "$APP_ID" ]]; then
  APP_ID=$(az ad app create --display-name "$DEPLOY_APP_NAME" --sign-in-audience AzureADMyOrg --query appId -o tsv)
fi
SP_ID=$(az ad sp show --id "$APP_ID" --query id -o tsv 2>/dev/null || az ad sp create --id "$APP_ID" --query id -o tsv)
echo "App ID：${APP_ID}；SP Object ID：$SP_ID"

step "新增 Federated Credential（GitHub Environment ${GITHUB_ENV_NAME}）"
FIC_SUBJECT="repo:${GITHUB_REPO}:environment:${GITHUB_ENV_NAME}"
if [[ "$(az ad app federated-credential list --id "$APP_ID" --query "[?subject=='$FIC_SUBJECT'] | length(@)" -o tsv)" == "0" ]]; then
  az ad app federated-credential create --id "$APP_ID" --parameters "{
    \"name\": \"github-env-${GITHUB_ENV_NAME}\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"$FIC_SUBJECT\",
    \"audiences\": [\"api://AzureADTokenExchange\"],
    \"description\": \"GitHub Actions environment ${GITHUB_ENV_NAME}\"
  }" -o none
fi
echo "Subject：$FIC_SUBJECT"

step "指派部署身分角色（範圍：${RESOURCE_GROUP}）"
assign_role "$SP_ID" "Contributor" "$RG_SCOPE"
assign_role "$SP_ID" "Storage Blob Data Contributor" "$RG_SCOPE"

step "預建使用者指派受控識別並指派資料角色"
# shellcheck disable=SC2086
az identity create -g "$RESOURCE_GROUP" -n "$WEB_IDENTITY" -l "$LOCATION" --tags $TAGS -o none
# shellcheck disable=SC2086
az identity create -g "$RESOURCE_GROUP" -n "$DEPLOYSCRIPT_IDENTITY" -l "$LOCATION" --tags $TAGS -o none
WEB_PRINCIPAL=$(az identity show -g "$RESOURCE_GROUP" -n "$WEB_IDENTITY" --query principalId -o tsv)
DS_PRINCIPAL=$(az identity show -g "$RESOURCE_GROUP" -n "$DEPLOYSCRIPT_IDENTITY" --query principalId -o tsv)
# 新建的受控識別需要數秒才會同步到 Entra ID，指派前先等待。
sleep 20
assign_role "$WEB_PRINCIPAL" "Key Vault Secrets User" "$RG_SCOPE"
assign_role "$WEB_PRINCIPAL" "Storage Blob Data Contributor" "$RG_SCOPE"
assign_role "$DS_PRINCIPAL" "Key Vault Secrets Officer" "$RG_SCOPE"

step "設定 GitHub Environment $GITHUB_ENV_NAME"
gh api -X PUT "repos/$GITHUB_REPO/environments/$GITHUB_ENV_NAME" --input - >/dev/null <<'EOF'
{"deployment_branch_policy":{"protected_branches":false,"custom_branch_policies":true}}
EOF
EXISTING_POLICIES=$(gh api "repos/$GITHUB_REPO/environments/$GITHUB_ENV_NAME/deployment-branch-policies" --jq '.branch_policies[].name')
for pattern in main 'refs/pull/*/merge'; do
  if ! grep -qxF "$pattern" <<<"$EXISTING_POLICIES"; then
    gh api -X POST "repos/$GITHUB_REPO/environments/$GITHUB_ENV_NAME/deployment-branch-policies" \
      -f name="$pattern" -f type=branch >/dev/null
  fi
  echo "允許分支：$pattern"
done
gh variable set AZURE_CLIENT_ID --env "$GITHUB_ENV_NAME" -R "$GITHUB_REPO" --body "$APP_ID"
gh variable set AZURE_TENANT_ID --env "$GITHUB_ENV_NAME" -R "$GITHUB_REPO" --body "$TENANT_ID"
gh variable set AZURE_SUBSCRIPTION_ID --env "$GITHUB_ENV_NAME" -R "$GITHUB_REPO" --body "$SUBSCRIPTION_ID"
gh variable set AZURE_RESOURCE_GROUP --env "$GITHUB_ENV_NAME" -R "$GITHUB_REPO" --body "$RESOURCE_GROUP"
gh variable set SQL_ADMIN_LOGIN --env "$GITHUB_ENV_NAME" -R "$GITHUB_REPO" --body "$SQL_ADMIN_LOGIN"

if gh secret list --env "$GITHUB_ENV_NAME" -R "$GITHUB_REPO" | cut -f1 | grep -qx SQL_ADMIN_PASSWORD; then
  echo "SQL_ADMIN_PASSWORD 已存在，保留原值"
else
  # 32 字元，只含英數與 -_（不含 `;`），並確保有大小寫與數字，符合 Azure SQL 複雜度規則。
  python3 -c "
import secrets, string
a = string.ascii_letters + string.digits + '-_'
while True:
    p = ''.join(secrets.choice(a) for _ in range(32))
    if any(c.isupper() for c in p) and any(c.islower() for c in p) and any(c.isdigit() for c in p):
        break
print(p, end='')" | gh secret set SQL_ADMIN_PASSWORD --env "$GITHUB_ENV_NAME" -R "$GITHUB_REPO"
  echo "已產生並寫入 SQL_ADMIN_PASSWORD（未輸出）"
fi

step "完成"
cat <<EOF
Resource Group：${RESOURCE_GROUP}（${LOCATION}）
部署身分：${DEPLOY_APP_NAME}（App ID ${APP_ID}）
受控識別：${WEB_IDENTITY}、$DEPLOYSCRIPT_IDENTITY
GitHub Environment：$GITHUB_ENV_NAME
下一步：執行「Azure OIDC 驗證」workflow 確認 GitHub 可登入 Azure。
EOF
