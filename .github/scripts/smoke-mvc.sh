#!/usr/bin/env bash
# 新站部署後冒煙。只打新網址，不檢查舊站。
# 用法：smoke-mvc.sh <網站網址> <圖片根網址>
set -euo pipefail

app_url="${1:?用法：smoke-mvc.sh <網站網址> <圖片根網址>}"
image_base_url="${2:?用法：smoke-mvc.sh <網站網址> <圖片根網址>}"
app_url="${app_url%/}"
image_base_url="${image_base_url%/}"
max_attempts="${SMOKE_MAX_ATTEMPTS:-30}"
delay_seconds="${SMOKE_DELAY_SECONDS:-10}"

body_file=$(mktemp)
header_file=$(mktemp)
trap 'rm -f "$body_file" "$header_file"' EXIT

check_page() {
  local path="$1" expected="$2" attempt status
  for ((attempt = 1; attempt <= max_attempts; attempt++)); do
    status=$(curl -sS -L -o "$body_file" -w '%{http_code}' --max-time 60 "${app_url}${path}" || echo 000)
    if [[ "$status" == 200 ]] && grep -q -F -- "$expected" "$body_file"; then
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

check_headers() {
  local path="$1" attempt name problems csp
  for ((attempt = 1; attempt <= max_attempts; attempt++)); do
    problems=()
    curl -sS -D "$header_file" -o "$body_file" --max-time 60 "${app_url}${path}" || true
    for name in X-Frame-Options X-Content-Type-Options Content-Security-Policy Referrer-Policy Permissions-Policy Strict-Transport-Security; do
      grep -qi "^${name}:" "$header_file" || problems+=("缺少 ${name}")
    done
    csp=$(grep -i '^content-security-policy:' "$header_file" || true)
    if [[ "$csp" == *unsafe-inline* || "$csp" == *unsafe-eval* ]]; then
      problems+=("CSP 含 unsafe-inline 或 unsafe-eval")
    fi
    if grep -q 'href="/seller/products/new"' "$body_file"; then
      problems+=("匿名首頁提供上架入口")
    fi
    if [[ "${#problems[@]}" == 0 ]]; then
      echo "通過：${path} 安全性回應標頭"
      return 0
    fi
    echo "等待：${path} 安全性回應標頭（第 ${attempt}/${max_attempts} 次：${problems[*]}）"
    sleep "$delay_seconds"
  done
  echo "::error::冒煙測試失敗：${path} 安全性回應標頭不正確（${problems[*]}）"
  return 1
}

check_not_found() {
  local path="$1" attempt status
  for ((attempt = 1; attempt <= max_attempts; attempt++)); do
    status=$(curl -sS -o "$body_file" -w '%{http_code}' --max-time 60 "${app_url}${path}" || echo 000)
    if [[ "$status" == 404 ]] && grep -q '找不到頁面' "$body_file"; then
      echo "通過：${path} 回傳 404"
      return 0
    fi
    echo "等待：${path}（第 ${attempt}/${max_attempts} 次，HTTP ${status}）"
    sleep "$delay_seconds"
  done
  echo "::error::冒煙測試失敗：${path} 應回傳 404（實際 HTTP ${status}）"
  head -c 2000 "$body_file" || true
  echo
  return 1
}

check_headers /
check_page / 'Demo Laptop'
check_page /account/login '登入'
check_page /categories 'Computer'
check_image "$image_base_url/products/demo_seller/laptop.png"
check_not_found /smoke-test-not-found
echo '新站冒煙測試全部通過。'
