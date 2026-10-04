#!/usr/bin/env bash
# Release 單元與整合測試，排除 E2E 的執行與覆蓋率收集，再檢查兩個產品套件。
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"
rm -rf "$root/TestResults"
dotnet test Shopping.sln \
  --configuration Release \
  --nologo \
  --verbosity minimal \
  --collect:"XPlat Code Coverage" \
  --results-directory "$root/TestResults" \
  --filter "FullyQualifiedName!~Shopping.E2E"
xml="$(find "$root/TestResults" -name coverage.cobertura.xml -type f | tail -n 1)"
if [[ -z "${xml}" ]]; then
  echo "找不到 coverage.cobertura.xml"
  exit 1
fi
python3 - "$xml" <<'PY'
import sys
import xml.etree.ElementTree as ET
from decimal import Decimal, InvalidOperation

path = sys.argv[1]
try:
    document = ET.parse(path)
except ET.ParseError as error:
    print("不是 Cobertura：" + str(error))
    sys.exit(1)

packages = list(document.iter("package"))

def belongs(name, assembly):
    if name is None:
        return False
    return name == assembly or name.startswith(assembly + ".")

def exact(value):
    if value is None:
        return False
    try:
        return Decimal(value) == Decimal(1)
    except InvalidOperation:
        return False

for assembly in ("Shopping.Web", "Shopping.Data"):
    matched = [item for item in packages if belongs(item.get("name"), assembly)]
    if len(matched) == 0:
        print("缺少套件 " + assembly)
        sys.exit(1)
    for package in matched:
        if not exact(package.get("line-rate")):
            print(assembly + " 的 line-rate 不是 1")
            sys.exit(1)
        if not exact(package.get("branch-rate")):
            print(assembly + " 的 branch-rate 不是 1")
            sys.exit(1)
        print(package.get("name") + " line-rate=" + package.get("line-rate") + " branch-rate=" + package.get("branch-rate"))

print("coverage ok " + path)
PY
