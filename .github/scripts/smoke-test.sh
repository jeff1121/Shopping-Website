#!/usr/bin/env bash
# 部署後冒煙測試（Plan.md §11.2 6-1）。
# 用法：smoke-test.sh <網站網址> <圖片根網址>
#   例：smoke-test.sh https://app-shopping-xxxxxx.azurewebsites.net https://afd-xxxx.azurefd.net
# 網站剛部署時會執行資料庫 migration，因此每個檢查都會重試（次數與間隔可用環境變數調整）。
set -euo pipefail

app_url="${1:?用法：smoke-test.sh <網站網址> <圖片根網址>}"
image_base_url="${2:?用法：smoke-test.sh <網站網址> <圖片根網址>}"
app_url="${app_url%/}"
image_base_url="${image_base_url%/}"
max_attempts="${SMOKE_MAX_ATTEMPTS:-30}"
delay_seconds="${SMOKE_DELAY_SECONDS:-10}"

body_file=$(mktemp)
header_file=$(mktemp)
trap 'rm -f "$body_file" "$header_file"' EXIT

# 檢查頁面回應 200、不是錯誤頁，且含有指定文字（用來確認 migration 與示範資料已就緒）。
check_page() {
  local path="$1" expected="$2" attempt status
  for ((attempt = 1; attempt <= max_attempts; attempt++)); do
    status=$(curl -sS -L -o "$body_file" -w '%{http_code}' --max-time 60 "${app_url}${path}" || echo 000)
    if [[ "$status" == 200 ]] &&
      ! grep -q -e 'Server Error' -e '網站啟動失敗' "$body_file" &&
      grep -q -F -- "$expected" "$body_file"; then
      echo "通過：${path}（含「${expected}」）"
      return 0
    fi
    echo "等待：${path}（第 ${attempt}/${max_attempts} 次，HTTP ${status}）"
    sleep "$delay_seconds"
  done
  echo "::error::冒煙測試失敗：${path} 未在時間內回應 200 並包含「${expected}」"
  head -c 2000 "$body_file" || true
  echo
  return 1
}

# 檢查圖片可經 Front Door 取得（回應 200 且為圖片）。
check_image() {
  local url="$1" attempt status
  for ((attempt = 1; attempt <= max_attempts; attempt++)); do
    status=$(curl -sS -L -o /dev/null -D "$header_file" -w '%{http_code}' --max-time 60 "${url}" || echo 000)
    if [[ "$status" == 200 ]] && grep -qi '^content-type: image/' "$header_file"; then
      echo "通過：${url}"
      return 0
    fi
    echo "等待：${url}（第 ${attempt}/${max_attempts} 次，HTTP ${status}）"
    sleep "$delay_seconds"
  done
  echo "::error::冒煙測試失敗：圖片 ${url} 無法取得"
  return 1
}

check_page /index.aspx 'Demo Smart Watch'
check_page /login.aspx 'Login Page'
check_page /categories.aspx 'Computer Accesories'
check_image "$image_base_url/products/demo_seller/watch.png"
echo '冒煙測試全部通過。'
