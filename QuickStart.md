# QuickStart：安裝與啟動指南

本文件說明如何從零開始，在本機把 Shopping Website（ASP.NET Web Forms + SQL Server）建置並執行起來。
專案已補齊建置所需檔案；所有連線與寄信、圖片儲存設定都由**環境變數**提供，開始前需設定本機環境變數（第 4 節）。

> 專案功能、架構與已知問題的完整說明請見 [README.md](README.md)。

---

## 目錄

1. [環境需求](#1-環境需求)
2. [取得原始碼](#2-取得原始碼)
3. [建立資料庫](#3-建立資料庫)
4. [設定環境變數](#4-設定環境變數)
5. [必要修正（建置前）](#5-必要修正建置前)
6. [建置與執行](#6-建置與執行)
7. [建立測試帳號與賣家](#7-建立測試帳號與賣家)
8. [功能驗證清單](#8-功能驗證清單)
9. [選用設定](#9-選用設定)
10. [常見問題排除](#10-常見問題排除)

---

## 1. 環境需求

| 項目 | 版本 / 說明 |
| --- | --- |
| 作業系統 | Windows 10 / 11 或 Windows Server（ASP.NET Web Forms 僅能在 .NET Framework 上執行） |
| Visual Studio | 2019 以上，需安裝「ASP.NET 與網頁程式開發」工作負載 |
| .NET Framework | 4.7.2 Developer Pack |
| IIS Express | 隨 Visual Studio 安裝 |
| SQL Server | 2016 以上任一版本（Express / Developer / LocalDB 皆可） |
| SQL 管理工具 | SQL Server Management Studio（SSMS）或 Azure Data Studio |
| iTextSharp | 5.5.13.6（NuGet），供「訂單 PDF 匯出」使用 |
| NuGet | 還原 `packages.config` 列出的套件（iTextSharp、Roslyn CodeDom、Configuration Builders、Azure Storage／Identity、DbUp 與其相依套件） |
| Azurite（選用） | 本機模擬 Blob Storage，供上架商品上傳圖片；可用 `npm i -g azurite` 或 Docker `mcr.microsoft.com/azure-storage/azurite` |
| SMTP 測試伺服器（選用） | 例如 smtp4dev、Papercut，供忘記密碼寄信測試 |

### macOS / Linux 使用者

本專案**無法**在 macOS 或 Linux 上以 .NET（Core）執行。可選擇：

- 使用 Windows 虛擬機（Parallels、VMware、UTM）或雲端 Windows VM 執行 Visual Studio。
- SQL Server 可改用 Docker 在本機執行，再由 Windows VM 連線：

  ```bash
  docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=<你的強密碼>" \
    -p 1433:1433 --name mssql -d mcr.microsoft.com/mssql/server:2022-latest
  ```

  Apple Silicon 需在 Docker Desktop 啟用 Rosetta 模擬 x86_64。

---

## 2. 取得原始碼

```bash
git clone https://github.com/Jeff1121/Shopping-Website.git
cd Shopping-Website
```

目錄重點：

```text
Shopping-Website/
├── Website.sln              # Visual Studio 方案檔
├── DbSql.sql                # 原始資料庫腳本（已過時，僅供參考）
├── QuickStart.md            # 本文件
├── README.md                # 專案說明
├── Plan.md                  # CI/CD 與 Azure 部署計畫書
├── .github/workflows/       # CI：Lint、CodeQL、SBOM、建置、Azure OIDC 驗證
├── infra/                   # bootstrap.sh、smtp-setup.sh、Bicep（main.bicep、modules/）
└── Website/                 # Web Forms 網站專案
    ├── *.aspx / *.aspx.cs   # 頁面與後置程式碼
    ├── Data/Db.cs           # 後置程式碼集中建立 SQL 連線
    ├── Web.config           # 連線字串與編譯設定
    ├── Website.csproj       # 專案檔（舊式格式）
    └── img/                 # 靜態圖片與商品圖片
```

---

## 3. 建立資料庫

> ⚠️ **不要執行 `DbSql.sql`**。該檔案有拼字錯誤（`CREATE DATABSE`）、缺少資料表與欄位，與程式碼實際使用的結構不符，僅保留作為歷史參考。

資料表與示範資料由網站**啟動時自動建立**（DbUp，見 [ADR-0002](docs/adr/0002-migration-on-app-startup.md)）。你只需要準備一個空資料庫與 SQL 登入：

```sql
CREATE DATABASE website;
GO
-- 選用：建立專用登入。本機也可以讓 SQL_USER 與 SQL_MIGRATOR_USER 使用同一個帳號。
CREATE LOGIN shopping_local WITH PASSWORD = '<自行設定的強密碼>';
GO
USE website;
GO
CREATE USER shopping_local FOR LOGIN shopping_local;
ALTER ROLE db_ddladmin ADD MEMBER shopping_local;   -- migration 需要建立資料表
ALTER ROLE db_datareader ADD MEMBER shopping_local;
ALTER ROLE db_datawriter ADD MEMBER shopping_local;
GO
```

設定好第 4 節的環境變數後啟動網站，`Global.asax` 會在檢查設定後執行 `Website/Data/DatabaseMigrator.cs`：

1. 以 `migratorConnectionString`（`SQL_MIGRATOR_USER`）連線，透過 `sp_getapplock` 取得排他鎖，避免多個執行個體同時執行；
2. 依檔名順序執行內嵌於組件的 `Website/Migrations/*.sql`，每支腳本一個交易；
3. 已執行的腳本記錄在 `dbo.SchemaVersions`，下次啟動只會執行新增的腳本；
4. 任一步驟失敗時，網站進入啟動失敗狀態（每個請求回應 500），錯誤寫入 Trace／Application Insights。

| 腳本 | 內容 |
| --- | --- |
| `0001_create_tables.sql` | 建立 `violet_user_login`、`violet_products`、`violet_cart`、`violet_order`、`violet_categories`、`violet_contact`（程式中的 INSERT 皆指定欄位名稱） |
| `0002_seed_categories.sql` | 分類 `Computer`、`Computer Accesories`（名稱須與 `addProducts.aspx.cs` 的固定清單一致） |
| `0003_seed_demo_users.sql` | 示範帳號（見下表） |
| `0004_seed_demo_products.sql` | 示範賣家的 4 項商品；圖片網址為 `IMAGE_BASE_URL/products/demo_seller/*.png`；`Demo Arcade Machine` 庫存為 0，用來展示「售完」 |
| `0005_password_hash_and_reset_tokens.sql` | `password` 擴充為 `VARCHAR(200)` 存放 PBKDF2 雜湊；新增重設密碼權杖表 `violet_password_reset` |

所有腳本都會先確認資料表或資料列是否已存在，因此套用到依舊版 QuickStart 手動建立的資料庫也不會失敗，也不會重複新增資料。

**示範帳號**（Repo 是公開的，以下密碼僅供展示，不得用於真實帳號）：

| 帳號 | 密碼 | 姓名（`uname`） | 角色 |
| --- | --- | --- | --- |
| `demo_customer` | `Demo@1234` | Demo Customer | 一般會員（`uid` 1001） |
| `demo_seller` | `Demo@1234` | Demo Seller | 賣家（`uid` 5001），擁有 4 項示範商品 |

示範商品圖片放在 `Website/img/products/demo_seller/`。本機請依 4.3 上傳到 Azurite；Azure 環境由部署流程上傳到 Blob。

資料表設計說明：

- 刻意**不建立外鍵**：賣家可在個人頁刪除商品，而購物車/訂單中仍可能保留該商品名稱；加上外鍵會導致刪除失敗。
- `category` 使用 `VARCHAR(50)`：原腳本的 `VARCHAR(15)` 放不下 `Computer Accesories`（19 字元）。
- `password` 存 PBKDF2 雜湊（`VARCHAR(200)`）。示範帳號在 0003 中是明碼，第一次登入成功時會自動改存雜湊。
- `violet_seller_login` 未被任何程式碼使用，因此不建立。
- 各欄位的完整說明見 [README.md 的資料庫結構](README.md#資料庫結構)。

### 新增 migration

1. 在 `Website/Migrations/` 新增 `NNNN_說明.sql`（編號遞增，CRLF、無 BOM）；可使用變數 `$IMAGE_BASE_URL$`。
2. 在 `Website/Website.csproj` 加入 `<EmbeddedResource Include="Migrations\NNNN_說明.sql" />`，否則不會打包進組件。
3. 以 CI 相同的 sqlfluff 檢查：`docker run --rm -v "$PWD:/sql" -w /sql --entrypoint sqlfluff sqlfluff/sqlfluff:4.3.0 lint --dialect tsql db Website/Migrations`。
4. **不要修改已經執行過的腳本**（DbUp 以檔名判斷是否執行過，修改內容不會重新執行）；需要變更時新增一支腳本。

### 清除示範資料

轉入正式營運前執行 `db/cleanup/remove_demo_data.sql`（刪除示範帳號的購物車、訂單、商品與帳號，保留分類）。此檔不在 `Migrations/`，不會自動執行。

---

## 4. 設定環境變數

`Website/Web.config` 不含任何真實連線資訊。網站啟動時，Configuration Builders（`Microsoft.Configuration.ConfigurationBuilders.Environment`，Token 模式）會把 `${名稱}` 權杖替換為**同名環境變數**；`Global.asax` 會檢查必要設定，缺漏時每個請求都回應 500，並在本機請求中列出缺漏的設定名稱（不顯示值）。

### 4.1 設定清單

| 環境變數 | 必要 | 本機範例 | 說明 |
| --- | --- | --- | --- |
| `SQL_SERVER` | ✅ | `localhost\SQLEXPRESS` 或 `localhost,1433` | 連線字串的 `Server`；Azure 為 `tcp:<伺服器>.database.windows.net,1433` |
| `SQL_DATABASE` | ✅ | `website` | 資料庫名稱 |
| `SQL_ENCRYPT` | ✅ | `False` | 是否加密連線；本機未設定憑證時用 `False`，Azure 為 `True` |
| `SQL_USER`／`SQL_PASSWORD` | ✅ | 自建的 SQL 登入 | 網站一般請求使用（只需讀寫權限） |
| `SQL_MIGRATOR_USER`／`SQL_MIGRATOR_PASSWORD` | ✅ | 可與上面相同 | 資料庫 migration 使用（需 DDL 權限）；本機可沿用同一個帳號 |
| `SMTP_HOST`／`SMTP_PORT` | ✅ | `localhost`／`25` | 忘記密碼寄信；連接埠 25 不加密，其他連接埠一律 STARTTLS |
| `SMTP_FROM` | ✅ | `noreply@localhost` | 寄件者 |
| `SMTP_USER`／`SMTP_PASSWORD` | 選用 | 留空 | 有值時才使用帳密驗證 |
| `STORAGE_BLOB_ENDPOINT` | ✅ | `http://127.0.0.1:10000/devstoreaccount1` | Blob 端點；指向本機（Azurite）時自動使用開發儲存體帳號 |
| `STORAGE_CONTAINER` | ✅ | `products` | 商品圖片容器 |
| `IMAGE_BASE_URL` | ✅ | `http://127.0.0.1:10000/devstoreaccount1` | 圖片網址的根，圖片網址為「此值/容器/賣家/檔名」；Azure 為 Front Door 端點 |

> 連線字串採 SQL 驗證（`User ID`／`Password`）。本機 SQL Server 若只開 Windows 驗證，請改為「SQL Server 及 Windows 驗證模式」並建立 SQL 登入，或使用第 1 節的 Docker SQL Server。密碼不可含 `;`、`'`、`"`、`{`、`}`。

### 4.2 在 Windows 設定（使用者環境變數）

以 PowerShell 執行，完成後**重新啟動 Visual Studio**（IIS Express 才會讀到新的環境變數）：

```powershell
setx SQL_SERVER "localhost\SQLEXPRESS"
setx SQL_DATABASE "website"
setx SQL_ENCRYPT "False"
setx SQL_USER "shopping_dev"
setx SQL_PASSWORD "<本機密碼>"
setx SQL_MIGRATOR_USER "shopping_dev"
setx SQL_MIGRATOR_PASSWORD "<本機密碼>"
setx SMTP_HOST "localhost"
setx SMTP_PORT "25"
setx SMTP_FROM "noreply@localhost"
setx STORAGE_BLOB_ENDPOINT "http://127.0.0.1:10000/devstoreaccount1"
setx STORAGE_CONTAINER "products"
setx IMAGE_BASE_URL "http://127.0.0.1:10000/devstoreaccount1"
```

### 4.3 本機 Blob（Azurite）

上架商品時圖片會上傳到 Blob。啟動 Azurite 並建立可匿名讀取單一檔案的容器：

```bash
azurite-blob --blobHost 127.0.0.1 --blobPort 10000
az storage container create --name products --public-access blob --connection-string "UseDevelopmentStorage=true"
```

上傳示範商品圖片（對應 `0004_seed_demo_products.sql` 的圖片網址）：

```bash
az storage blob upload-batch --destination products --destination-path demo_seller \
  --source Website/img/products/demo_seller --connection-string "UseDevelopmentStorage=true"
```

### 4.4 程式中的讀取方式

- 12 個後置程式碼以 `Website.Data.Db.CreateConnection()` 讀取 `cmpConnectionString`；`asp:SqlDataSource` 以 `<%$ ConnectionStrings:cmpConnectionString %>` 讀取同一設定。
- `Website.Data.Db.CreateMigratorConnection()` 讀取 `migratorConnectionString`，供資料庫 migration 使用。
- 寄信與圖片設定集中由 `Website.Config.AppSettings` 讀取；圖片上傳由 `Website.Data.ImageStore` 處理。

> 🔒 請勿把真實帳號密碼寫進 `Web.config` 或任何檔案；Azure 上的密碼都放在 Key Vault，由 App Service 以 Key Vault 參考提供。

---

## 5. 必要修正（建置前）

以下項目已在專案中補齊；保留本節供檢查與疑難排解。

### 5.0 版控與專案檔現況（先確認）

- Repo 已有 `.gitignore`：`bin/`、`obj/`、`packages/`、`*.user`、`.vs/` 與本機敏感設定（`secrets.xml`、`.env`）都不進版控。
- `packages/` 不在 Repo 中，第一次建置前**必須**執行 NuGet 還原（見 5.4）。
- `Website/Website.csproj.user` 不在 Repo 中；根網址改由 `Web.config` 的 `defaultDocument` 導向 `index.aspx`（見第 6 節）。
- `.editorconfig` 與 `.gitattributes` 規定編碼與行尾：`.cs`、`.aspx`、`.csproj`、`.sln` 為 UTF-8 BOM + CRLF，`.config`、`.sql` 為 CRLF，其餘為 UTF-8 + LF。
- `Website/img/products/apple.png`、`Website/img/products/appol.png`、`Website/img/products/img2.png`、
  `Website/img/products/human99/laptop.png` 已在 Git 中，但未列入 `Website.csproj` 的 `<Content Include>`；
  以 Web Application 專案發佈時若需要這些圖片，請在 Visual Studio 將它們加入專案。
- `Website/css/style.css` 與 `Website/Properties/AssemblyInfo.cs` 已列入專案並應隨原始碼取得。

### 5.1 `Properties/AssemblyInfo.cs`

`Website.csproj` 引用的 `Properties\AssemblyInfo.cs` 已納入專案，內容包含版本資訊與 CI 可替換的 `AssemblyInformationalVersion`。若檔案遺失，可依下列最小內容重建：

- 建立 `Website/Properties/AssemblyInfo.cs`，最少內容：

  ```csharp
  using System.Reflection;
  using System.Runtime.InteropServices;

  [assembly: AssemblyTitle("Website")]
  [assembly: ComVisible(false)]
  [assembly: AssemblyVersion("1.0.0.0")]
  [assembly: AssemblyFileVersion("1.0.0.0")]
  ```

### 5.2 iTextSharp NuGet 參考

`Website.csproj` 已改用 NuGet 還原的 iTextSharp 與 BouncyCastle.Cryptography：

- `..\packages\iTextSharp.5.5.13.6\lib\net461\itextsharp.dll`
- `..\packages\BouncyCastle.Cryptography.2.6.2\lib\net461\BouncyCastle.Cryptography.dll`

若 Visual Studio 顯示找不到 `iTextSharp` 命名空間，請先執行 NuGet 還原。

### 5.3 `css/style.css`

所有頁面都以 `<link href="css/style.css">` 引用共用樣式；此檔已納入專案，僅提供中性基礎樣式，
各頁的內嵌 `<style>` 與 inline style 仍是主要版面來源。

### 5.4 NuGet 還原

NuGet 還原會取得 iTextSharp 5.5.13.6、BouncyCastle.Cryptography 2.6.2 與 Roslyn 編譯器套件（Microsoft.CodeDom.Providers.DotNetCompilerPlatform 4.1.0）。若出現 `csc.exe`、`itextsharp.dll` 或 `BouncyCastle.Cryptography.dll` 找不到之類的錯誤，
在方案上按右鍵執行「還原 NuGet 套件」，或於命令列執行：

```powershell
nuget restore Website.sln
```

---

## 6. 建置與執行

### Visual Studio

1. 開啟 `Website.sln`。
2. 確認 `Website` 為啟始專案。
   `Web.config` 的 `system.webServer/defaultDocument` 已將 `index.aspx` 設為預設文件，不需另外設定起始頁；個人起始頁設定存在 `Website.csproj.user`，此檔不進版控。
3. `Ctrl+Shift+B` 建置方案。
4. `F5`（偵錯）或 `Ctrl+F5`（不偵錯）啟動，瀏覽器會開啟 `https://localhost:44337/`，並顯示 `index.aspx` 首頁。
5. 第一次使用 HTTPS 時，IIS Express 會詢問是否信任開發憑證，請選擇「是」。

### 命令列（Developer PowerShell for VS）

```powershell
nuget restore Website.sln
msbuild Website.sln /p:Configuration=Debug
& "C:\Program Files\IIS Express\iisexpress.exe" /path:"$PWD\Website" /port:8080
```

之後瀏覽 `http://localhost:8080/index.aspx`。

### GitHub Actions 建置產物

每次 PR 與 `main` push 都會執行「建置」workflow：在 Windows runner 還原 NuGet、以 MSBuild 發行網站、壓縮成 `site.zip`，並產生部署套件 SBOM。可在 GitHub Actions 的對應 workflow run 下載 `site` artifact 取得 `site.zip`（保留 30 天）。

`main` 上產出的 `site.zip` 附有建置來源證明與 SBOM 簽章，可用下列指令驗證：

```bash
gh attestation verify site.zip -R jeff1121/Shopping-Website
```

其他 CI（CodeQL、SBOM、相依套件審查、Lint、Scorecard）與本機 Lint 指令請見 [README 的 CI/CD 與 Azure 部署](README.md#cicd-與-azure-部署)。

---

## 7. 建立測試帳號與賣家

> 想直接展示時，可使用第 3 節自動建立的示範帳號 `demo_customer`、`demo_seller`。以下說明自行建立帳號的方式。

### 一般會員

1. 從頁首「Register → User」進入 `register.aspx`。
2. 依表單規則填寫：
   - 姓名只能有英文字母與空白；
   - 密碼至少 8 碼，需含大寫、小寫、數字與特殊字元（`@$!%*?&`）
   - 電話必須剛好 10 位數字；
   - 生日不可晚於今天；
   - 國家/州/城市的驗證器已被註解，保留 `Select` 也會被接受，請自行選擇實際選項。
3. 送出後系統自動指派 1～4998 的 `uid`，並導向登入頁。

### 賣家

1. 從頁首「Register → Seller」進入 `sellerRegister.aspx`，表單規則同一般會員；
2. 送出後系統自動指派 5001～9999 的 `uid`，並導向登入頁；
3. 登入後，`profile.aspx` 會出現「Add a Product」按鈕與「Products Sold on Website」清單。

### 上架商品

1. 以賣家帳號登入，在 `profile.aspx` 按「Add a Product」進入 `addProducts.aspx`；
2. 填寫名稱、價格、分類、關鍵字並選擇圖片後送出；
3. 新商品庫存為 0（首頁顯示售完），到 `profile.aspx` 的商品清單按「Edit」設定 Stock 後即可購買。

> 圖片限 JPEG／PNG／GIF／WebP、2 MB 以內，檔案內容須與副檔名相符；只有賣家帳號能開啟此頁。
> 上傳的圖片會寫入 Blob 容器 `products`，路徑為 `<登入帳號>/<GUID>.<副檔名>`；`pimage` 會存完整網址（`IMAGE_BASE_URL/products/...`）。本機需先啟動 Azurite（見 4.3）。

---

## 8. 功能驗證清單

| # | 操作 | 預期結果 |
| --- | --- | --- |
| 1 | 開啟首頁 | 以隨機順序顯示商品；庫存 0 的商品顯示售完圖示 |
| 2 | 排序選 `Low to High` / `High to Low` | 商品依價格排序 |
| 3 | 左側搜尋框輸入關鍵字 → Search | 顯示 `keywords` 含該字的商品 |
| 4 | Shop → Categories → 點選分類 | 導向 `index.aspx?category=...`，只顯示該分類 |
| 5 | 登入 | 頁首出現 Log Out、個人與購物車圖示，購物車徽章顯示品項數 |
| 6 | 選數量後按加入購物車 | 導向購物車頁，商品庫存同步減少 |
| 7 | 購物車按 Modify 修改數量 → Update | 小計與庫存同步調整 |
| 8 | 購物車按 Remove Item(s) | 品項移除、庫存加回、序號重新編號 |
| 9 | Checkout → Place Order | 顯示訂單完成面板，購物車徽章歸零；`violet_order` 新增資料、`violet_cart` 清空 |
| 10 | 個人頁 | 顯示訂單歷史，可下載 `OrderInvoice.pdf` |
| 11 | Contact 送出留言 | `violet_contact` 新增一筆 |
| 12 | 忘記密碼 | 答對安全問題後寄出重設連結（需先完成第 9 節 SMTP 設定，且帳號 Email 可收信；示範帳號為 `@example.com`，會顯示寄送失敗）；開啟連結設定新密碼後可用新密碼登入，同一連結再次開啟顯示「已失效」 |
| 13 | 以一般會員開啟 `addProducts.aspx` | 導向 `profile.aspx` |

---

## 9. 選用設定

### SMTP（忘記密碼寄信）

`forgotpass.aspx.cs` 以 `SmtpClient` 寄出重設連結（網址取自目前請求的主機名稱），主機、連接埠、寄件者與帳密都來自環境變數 `SMTP_*`（見 4.1）。
本機可用 smtp4dev 或 Papercut 接收測試信（`SMTP_HOST=localhost`、`SMTP_PORT=25`、帳密留空）。
Azure 上使用 Azure Communication Services Email 的 SMTP 介面（`smtp.azurecomm.net:587`，STARTTLS），密碼放在 Key Vault。

### 發行（Release）

`Web.Release.config` 會在發行時移除 `compilation` 的 `debug` 屬性。可在 Visual Studio
「建置 → 發佈 Website」選擇「資料夾」或「IIS」目標。部署到 IIS 時：

- 應用程式集區使用 .NET CLR v4.0、整合式管線；
- 依第 4 節設定應用程式集區可讀到的環境變數（或在 IIS 設定中加入）；
- 正式環境請使用獨立的 SQL 帳號，勿使用 `sa`。

### Azure 部署

Azure 基礎設施由 Bicep 部署（「基礎設施」workflow）；程式合併到 `main` 後由「部署」workflow 自動建置、部署並執行冒煙測試（[Plan.md](Plan.md) M6）。
要在新的訂用帳戶重建這些設定，需要 Azure CLI、GitHub CLI（具 Repo admin 權限）與 Python 3，並在 macOS、Linux、WSL 或 Cloud Shell 執行：

```bash
az login
SUBSCRIPTION_ID=<訂用帳戶 ID> env -u GH_TOKEN ./infra/bootstrap.sh
```

腳本可重複執行，已存在的項目會略過；SQL 管理員密碼只在 GitHub Secret 不存在時產生，且不會顯示。
完成後到 GitHub Actions 手動執行「Azure OIDC 驗證」workflow，確認 GitHub 可以登入 Azure，再手動執行「基礎設施」workflow 建立資源。
第一次部署完成後執行一次 ACS SMTP 帳號設定：

```bash
SUBSCRIPTION_ID=<訂用帳戶 ID> env -u GH_TOKEN ./infra/smtp-setup.sh
```

細節見 [README](README.md#azure-基礎設施)。

---

## 10. 常見問題排除

| 症狀 | 原因與解法 |
| --- | --- |
| 開啟 `https://localhost:44337/` 出現 403 或目錄清單錯誤 | 確認 `Web.config` 保留 `system.webServer/defaultDocument`（`index.aspx`）；也可直接瀏覽 `/index.aspx` |
| 找不到 `csc.exe` 或 `packages\...` 路徑 | 尚未執行 NuGet 還原（`packages/` 不在 Repo 中），見 5.4 |
| 編譯錯誤 `CS1525: Invalid expression term '<'` | 工作區仍殘留舊版連線佔位字串，請同步最新程式碼 |
| 每個頁面都顯示「網站啟動失敗：設定不完整…」 | 環境變數未設定或未生效；本機請求會列出缺漏的設定名稱，設定後重新啟動 Visual Studio，見第 4 節 |
| 編譯錯誤 `CS2001: Source file 'Properties\AssemblyInfo.cs' could not be found` | 確認 `Website/Properties/AssemblyInfo.cs` 存在，見 5.1 |
| 找不到 `iTextSharp` 命名空間 | 請執行 NuGet 還原，見 5.2 |
| `Format of the initialization string does not conform to specification` | `SQL_*` 環境變數含 `;` 等特殊字元，見 4.1 |
| 連線時出現 SSL／憑證錯誤 | 本機 SQL Server 沒有受信任憑證時請設 `SQL_ENCRYPT=False` |
| `Invalid object name 'violet_xxx'` | 連到錯誤的資料庫，或 migration 未執行；確認 `SQL_DATABASE` 與 `dbo.SchemaVersions`，見第 3 節 |
| 每個頁面都顯示「網站啟動失敗…」且記錄有「資料庫 migration 失敗」 | migrator 帳號無法連線、缺少 `db_ddladmin` 權限，或某支腳本失敗；記錄會列出腳本名稱，見第 3 節 |
| `String or binary data would be truncated` | 欄位長度不足（例如 `category`、`password` 超過 20 字元） |
| 按加入購物車後仍停在首頁 | 庫存已被其他人買走或不足；重新整理首頁確認剩餘數量 |
| 註冊時顯示「An account with this Name, Email-ID, Username or Phone Number already exists.」 | 姓名、使用者名稱、Email 或電話已被註冊，請換一組 |
| 忘記密碼顯示「Unable to send the reset email right now…」 | SMTP 設定錯誤或收件地址無效（示範帳號的 `@example.com` 是保留網域，ACS 會拒收）；記錄中有「重設密碼信寄送失敗」與 SMTP 狀態碼 |
| 上架商品時出現 Blob 連線錯誤 | Azurite 未啟動或容器 `products` 不存在，見 4.3；Azure 上請確認 `id-shopping-web` 具 Storage Blob Data Contributor |
| 首頁商品圖片破圖 | `pimage` 網址錯誤或容器不可匿名讀取；頁面會自動改顯示 `img/products/human.png` |
| Azure 上每個頁面都顯示「網站啟動失敗」（HTTP 500） | 遠端請求不顯示細節；到 Log Analytics `log-shopping` 查詢 `AppServiceAppLogs \| where Level == "Error"`（錯誤內容在 `StackTrace` 欄位），記錄約有數分鐘延遲 |
| 「部署」workflow 冒煙測試失敗 | 已自動回滾上一版；依上一列查詢記錄。若與基礎設施變更同時合併，可能是部署早於「基礎設施」完成，等它完成後重新執行「部署」 |
