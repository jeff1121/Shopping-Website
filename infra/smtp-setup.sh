#!/usr/bin/env bash
# ACS SMTP 帳號設定（Plan.md §9.4 第 5 步）。需在第一次 infra.yml 部署完成後執行。
#
# 用途：建立 Entra 應用程式 acs-shopping-smtp、在 Communication Service 指派只含寄信權限的自訂角色、
#       建立 SMTP 使用者名稱、把 client secret 寫入 Key Vault `smtp-password`，
#       最後設定 GitHub Environment 變數 SMTP_USER_NAME 並重新執行 infra.yml，讓 App Service 取得 SMTP 帳密。
#       可重複執行：已存在的項目會略過；Key Vault 已有 smtp-password 時不會重新產生 secret。
#
# 前置條件：
#   - 已登入 Azure CLI（可建立 App Registration、自訂角色與一般角色指派）與 GitHub CLI（repo admin；
#     若 GH_TOKEN 權限不足，請以 `env -u GH_TOKEN ./infra/smtp-setup.sh` 執行）。
#
# 用法：
#   SUBSCRIPTION_ID=<訂用帳戶 ID> ./infra/smtp-setup.sh
#
# 安全：client secret 只經由管線與權限 600 的暫存檔寫入 Key Vault（ARM 控制平面），不會輸出到畫面，暫存檔用完即刪。
# 輪替密碼：先在 Key Vault 刪除並清除 smtp-password，或設定 ROTATE_SECRET=1 再執行。

set -euo pipefail

SUBSCRIPTION_ID="${SUBSCRIPTION_ID:?請設定 SUBSCRIPTION_ID}"
RESOURCE_GROUP="${RESOURCE_GROUP:-rg-shopping}"
SMTP_APP_NAME="${SMTP_APP_NAME:-acs-shopping-smtp}"
SMTP_USER_NAME="${SMTP_USER_NAME:-shopping-smtp}"
# SMTP 使用者名稱資源的名稱（Azure 規定不能與使用者名稱相同）
SMTP_USERNAME_RESOURCE="${SMTP_USERNAME_RESOURCE:-smtp-user-web}"
ROLE_NAME="${ROLE_NAME:-ACS SMTP Sender (shopping)}"
GITHUB_REPO="${GITHUB_REPO:-jeff1121/Shopping-Website}"
GITHUB_ENV_NAME="${GITHUB_ENV_NAME:-azure}"
ROTATE_SECRET="${ROTATE_SECRET:-0}"
ACS_API="2025-09-01"
KV_API="2023-07-01"

# 輸出步驟標題。
step() { printf '\n==> %s\n' "$*"; }

step "切換訂用帳戶"
az account set --subscription "$SUBSCRIPTION_ID"
RG_ID="/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}"

step "取得 Communication Service 與 Key Vault"
ACS_NAME=$(az resource list -g "$RESOURCE_GROUP" --resource-type Microsoft.Communication/communicationServices --query "[0].name" -o tsv)
KV_NAME=$(az resource list -g "$RESOURCE_GROUP" --resource-type Microsoft.KeyVault/vaults --query "[0].name" -o tsv)
if [[ -z "$ACS_NAME" || -z "$KV_NAME" ]]; then
  echo "找不到 Communication Service 或 Key Vault，請先執行 infra.yml 部署。" >&2
  exit 1
fi
ACS_ID="${RG_ID}/providers/Microsoft.Communication/communicationServices/${ACS_NAME}"
echo "Communication Service：${ACS_NAME}；Key Vault：${KV_NAME}"

step "建立 Entra 應用程式 ${SMTP_APP_NAME}"
APP_ID=$(az ad app list --display-name "$SMTP_APP_NAME" --query "[0].appId" -o tsv)
if [[ -z "$APP_ID" ]]; then
  APP_ID=$(az ad app create --display-name "$SMTP_APP_NAME" --query appId -o tsv)
  echo "已建立應用程式"
else
  echo "已存在應用程式"
fi
SP_ID=$(az ad sp list --filter "appId eq '${APP_ID}'" --query "[0].id" -o tsv)
if [[ -z "$SP_ID" ]]; then
  SP_ID=$(az ad sp create --id "$APP_ID" --query id -o tsv)
  echo "已建立 Service Principal"
fi
echo "App ID：${APP_ID}"

step "建立自訂角色「${ROLE_NAME}」"
if [[ -z "$(az role definition list --name "$ROLE_NAME" --scope "$RG_ID" --query "[0].id" -o tsv)" ]]; then
  role_json=$(mktemp)
  cat > "$role_json" <<JSON
{
  "Name": "${ROLE_NAME}",
  "Description": "只允許以 ACS SMTP 寄信（Shopping Website）",
  "Actions": [
    "Microsoft.Communication/CommunicationServices/Read",
    "Microsoft.Communication/CommunicationServices/Write",
    "Microsoft.Communication/EmailServices/write"
  ],
  "AssignableScopes": ["${RG_ID}"]
}
JSON
  az role definition create --role-definition "$role_json" -o none
  rm -f "$role_json"
  echo "已建立自訂角色"
else
  echo "已存在自訂角色"
fi

step "在 Communication Service 指派自訂角色"
# 新建的自訂角色需要時間傳播，等到可以查到角色 ID 再指派
ROLE_ID=""
for _ in $(seq 1 20); do
  ROLE_ID=$(az role definition list --name "$ROLE_NAME" --scope "$RG_ID" --query "[0].name" -o tsv)
  [[ -n "$ROLE_ID" ]] && break
  echo "角色尚未傳播，15 秒後重試"
  sleep 15
done
if [[ -z "$ROLE_ID" ]]; then
  echo "查不到自訂角色「${ROLE_NAME}」" >&2
  exit 1
fi
count=$(az role assignment list --assignee "$SP_ID" --role "$ROLE_ID" --scope "$ACS_ID" --query "length(@)" -o tsv)
if [[ "$count" == "0" ]]; then
  az role assignment create --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal \
    --role "$ROLE_ID" --scope "$ACS_ID" -o none
  echo "已指派角色"
else
  echo "已存在角色指派"
fi

step "建立 SMTP 使用者名稱 ${SMTP_USER_NAME}"
TENANT_ID=$(az account show --query tenantId -o tsv)
az rest --method put \
  --url "${ACS_ID}/smtpUsernames/${SMTP_USERNAME_RESOURCE}?api-version=${ACS_API}" \
  --body "{\"properties\":{\"username\":\"${SMTP_USER_NAME}\",\"entraApplicationId\":\"${APP_ID}\",\"tenantId\":\"${TENANT_ID}\"}}" \
  -o none
echo "已設定 SMTP 使用者名稱"

step "寫入 Key Vault secret smtp-password"
SECRET_URL="${RG_ID}/providers/Microsoft.KeyVault/vaults/${KV_NAME}/secrets/smtp-password?api-version=${KV_API}"
if [[ "$ROTATE_SECRET" != "1" ]] && az rest --method get --url "$SECRET_URL" -o none 2>/dev/null; then
  echo "Key Vault 已有 smtp-password，略過（輪替請設定 ROTATE_SECRET=1）"
else
  # client secret 不輸出到畫面：經管線寫入只有本人可讀的暫存檔，送到 Key Vault（ARM 控制平面）後立即刪除
  body_file=$(mktemp)
  chmod 600 "$body_file"
  trap 'rm -f "$body_file"' EXIT
  az ad app credential reset --id "$APP_ID" --append --display-name smtp --years 1 --query password -o tsv \
    | python3 -c 'import json,sys; print(json.dumps({"properties":{"value":sys.stdin.read().strip(),"contentType":"ACS SMTP（acs-shopping-smtp client secret）"}}))' \
    > "$body_file"
  az rest --method put --url "$SECRET_URL" --body "@${body_file}" -o none
  rm -f "$body_file"
  echo "已寫入 smtp-password（有效期 1 年）"
fi

step "設定 GitHub Environment 變數並重新部署基礎設施"
gh variable set SMTP_USER_NAME --env "$GITHUB_ENV_NAME" --repo "$GITHUB_REPO" --body "$SMTP_USER_NAME"
gh workflow run infra.yml --repo "$GITHUB_REPO" --ref main
echo "已觸發 infra.yml；完成後 App Service 會取得 SMTP_USER 與 SMTP_PASSWORD。"
