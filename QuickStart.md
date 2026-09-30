# QuickStart：安裝與啟動指南

本文件說明如何從零開始，在本機把 Shopping Website（ASP.NET Web Forms + SQL Server）建置並執行起來。
專案目前的原始碼**無法直接編譯**，需先完成下方「必要修正」章節中的步驟。

> 專案功能、架構與已知問題的完整說明請見 [README.md](README.md)。

---

## 目錄

1. [環境需求](#1-環境需求)
2. [取得原始碼](#2-取得原始碼)
3. [建立資料庫](#3-建立資料庫)
4. [設定連線字串](#4-設定連線字串)
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
| iTextSharp | 5.x（`itextsharp.dll`），供「訂單 PDF 匯出」使用 |
| NuGet | 還原 `Microsoft.CodeDom.Providers.DotNetCompilerPlatform` 2.0.1 |

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
├── packages/                # 已簽入的 NuGet 套件（Roslyn 編譯器）
└── Website/                 # Web Forms 網站專案
    ├── *.aspx / *.aspx.cs   # 頁面與後置程式碼
    ├── Web.config           # 連線字串與編譯設定
    ├── Website.csproj       # 專案檔（舊式格式）
    └── img/                 # 靜態圖片與商品圖片
```

---

## 3. 建立資料庫

> ⚠️ **不要直接執行 `DbSql.sql`**。該檔案有拼字錯誤（`CREATE DATABSE`）、缺少資料表與欄位，
> 且檔尾有未結束的單引號字串，與程式碼實際使用的結構不符。請改用下方腳本。

在 SSMS 中連線到 SQL Server，開新查詢視窗後執行：

```sql
CREATE DATABASE website;
GO
USE website;
GO

-- 會員與賣家帳號（共用同一張表；uid > 5000 視為賣家）
-- 欄位順序必須與 register.aspx.cs 的位置式 INSERT 一致
CREATE TABLE violet_user_login (
    uname    VARCHAR(50)   NOT NULL PRIMARY KEY,   -- 姓名，同時作為購物車/訂單/商品的關聯鍵
    email    VARCHAR(50)   NOT NULL UNIQUE,
    username VARCHAR(20)   NOT NULL UNIQUE,
    password VARCHAR(20)   NOT NULL,               -- 明碼儲存（原始設計）
    phone    DECIMAL(10,0) NOT NULL UNIQUE,
    dob      DATE          NOT NULL,
    country  VARCHAR(20)   NOT NULL,
    state    VARCHAR(20)   NOT NULL,
    city     VARCHAR(20)   NOT NULL,
    gender   VARCHAR(10)   NOT NULL,
    address  VARCHAR(500)  NOT NULL,
    secq     VARCHAR(100)  NOT NULL,               -- 安全問題
    seca     VARCHAR(20)   NOT NULL,               -- 安全問題答案
    uid      INT           NOT NULL DEFAULT 0      -- 1~4998：一般會員；> 5000：賣家
);

-- 商品（uname 為賣家姓名，對應 violet_user_login.uname）
CREATE TABLE violet_products (
    pname    VARCHAR(50)   NOT NULL PRIMARY KEY,
    price    DECIMAL(20,2) NOT NULL,
    pimage   VARCHAR(250)  NOT NULL,               -- 相對路徑，例如 img/products/seller1/a.png
    category VARCHAR(50)   NULL,                   -- 須與 violet_categories.name 相同才能被分類頁篩選
    uname    VARCHAR(50)   NULL,
    keywords VARCHAR(500)  NULL,                   -- 首頁搜尋以 LIKE '%關鍵字%' 比對此欄
    stock    INT           NOT NULL DEFAULT 0      -- 加入購物車時即預扣
);

-- 購物車（欄位順序必須與 cart.aspx.cs 的 savecartdetail() 一致）
CREATE TABLE violet_cart (
    uname    VARCHAR(50)   NOT NULL,               -- 買家姓名
    sno      INT           NOT NULL,               -- 購物車內序號（刪除後會重新編號）
    pimage   VARCHAR(250)  NULL,
    pname    VARCHAR(50)   NOT NULL,
    price    DECIMAL(20,2) NOT NULL,
    quantity INT           NOT NULL,
    total    DECIMAL(20,2) NOT NULL,
    sname    VARCHAR(50)   NULL                    -- 賣家姓名
);

-- 訂單（欄位順序必須與 checkout.aspx.cs 的位置式 INSERT 一致）
CREATE TABLE violet_order (
    uname     VARCHAR(50)   NOT NULL,
    pname     VARCHAR(50)   NOT NULL,
    orderID   VARCHAR(30)   NOT NULL,              -- 例如 #2235122992026aBc9x（同一次結帳共用）
    orderDate VARCHAR(20)   NOT NULL,              -- DateTime.ToShortDateString() 字串，依伺服器地區設定而異
    quantity  INT           NOT NULL,
    total     DECIMAL(20,2) NOT NULL
);

-- 商品分類（categories.aspx 讀取 name 與 cimage）
CREATE TABLE violet_categories (
    name   VARCHAR(50)  NOT NULL PRIMARY KEY,
    cimage VARCHAR(250) NULL
);

-- 聯絡我們留言（contact.aspx.cs 依序寫入 3 欄）
CREATE TABLE violet_contact (
    uname   VARCHAR(50)  NULL,
    email   VARCHAR(50)  NULL,
    message VARCHAR(500) NULL
);
GO

-- 分類名稱須與 addProducts.aspx.cs 中寫死的選項一致
INSERT INTO violet_categories (name, cimage) VALUES
    ('Computer',            'img/categories/desktop.png'),
    ('Computer Accesories', 'img/categories/laptop.png');
GO
```

設計說明：

- 刻意**不建立外鍵**：賣家可在個人頁刪除商品，而購物車/訂單中仍可能保留該商品名稱；加上外鍵會導致刪除失敗。
- `category` 使用 `VARCHAR(50)`：原腳本的 `VARCHAR(15)` 放不下 `Computer Accesories`（19 字元），會出現截斷錯誤。
- `violet_seller_login` 未被任何程式碼使用，因此不建立。

### （選用）範例資料

```sql
USE website;
GO
-- 一位賣家（uid 6001）與一位一般會員（uid 1001），密碼皆為明碼
INSERT INTO violet_user_login VALUES
 ('Demo Seller', 'seller@example.com', 'seller1', 'Seller@123', 9000000001, '1990-01-01',
  'India', 'Maharashtra', 'Mumbai', 'Male', 'Seller address', 'What is the name of your first school?', 'abc', 6001),
 ('Demo Buyer',  'buyer@example.com',  'buyer1',  'Buyer@123',  9000000002, '1995-05-05',
  'India', 'Maharashtra', 'Pune',   'Female', 'Buyer address', 'What is the name of your first school?', 'abc', 1001);

-- 使用 Repo 內既有的商品圖片
INSERT INTO violet_products (pname, price, pimage, category, uname, keywords, stock) VALUES
 ('Smart Watch',    2999.00, 'img/products/watch.png',   'Computer Accesories', 'Demo Seller', 'watch smart wearable', 20),
 ('Laser Printer',  8999.00, 'img/products/printer.png', 'Computer Accesories', 'Demo Seller', 'printer office',       5),
 ('Arcade Machine', 15999.00,'img/products/arcade.png',  'Computer',            'Demo Seller', 'arcade game',          0);
GO
```

> 最後一項庫存為 0，可用來確認首頁會顯示「售完」圖示並停用加入購物車按鈕。

---

## 4. 設定連線字串

本專案有**兩處**需要設定連線字串，兩處都必須完成：

### 4.1 `Website/Web.config`

供頁面中的 `asp:SqlDataSource`（商品清單、搜尋、排序、分類、訂單歷史、賣家商品管理）使用：

```xml
<connectionStrings>
  <add name="cmpConnectionString"
       connectionString="Data Source=localhost\SQLEXPRESS;Initial Catalog=website;Integrated Security=True"
       providerName="System.Data.SqlClient" />
</connectionStrings>
```

常見連線字串範例：

| 環境 | 連線字串 |
| --- | --- |
| SQL Server Express（Windows 驗證） | `Data Source=localhost\SQLEXPRESS;Initial Catalog=website;Integrated Security=True` |
| LocalDB | `Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=website;Integrated Security=True` |
| Docker / SQL 驗證 | `Data Source=<主機>,1433;Initial Catalog=website;User ID=sa;Password=<你的強密碼>;TrustServerCertificate=True` |

### 4.2 後置程式碼中的 12 個佔位字串

下列檔案含有無法編譯的佔位字串 `new SqlConnection(<enter your database connection>)`：

`addProducts.aspx.cs`、`cart.aspx.cs`、`checkout.aspx.cs`、`contact.aspx.cs`、`forgotpass.aspx.cs`、
`index.aspx.cs`、`login.aspx.cs`、`profile.aspx.cs`、`register.aspx.cs`、`sellerProfile.aspx.cs`、
`sellerRegister.aspx.cs`、`sellerSignIn.aspx.cs`

**建議做法**：統一改為讀取 Web.config，之後只需維護一處（專案已參考 `System.Configuration`）：

```csharp
SqlConnection con = new SqlConnection(
    System.Configuration.ConfigurationManager.ConnectionStrings["cmpConnectionString"].ConnectionString);
```

在 Visual Studio 中可用「在檔案中取代」（`Ctrl+Shift+H`）一次完成：

- 尋找：`<enter your database connection>`
- 取代為：`System.Configuration.ConfigurationManager.ConnectionStrings["cmpConnectionString"].ConnectionString`
- 範圍：`Website` 資料夾，檔案類型 `*.cs`

> 🔒 請勿將含有真實帳號密碼的連線字串提交到版本控制。

---

## 5. 必要修正（建置前）

以下項目在 Repo 中缺漏，不處理會導致建置失敗或功能異常。

### 5.0 版控與專案檔現況（先確認）

- Repo 目前**沒有** `.gitignore`。
- `git ls-files` 顯示 `Website/obj/Debug/DesignTimeResolveAssemblyReferences.cache` 與
  `Website/obj/Debug/DesignTimeResolveAssemblyReferencesInput.cache` 已被簽入，這是建置快取，不是執行所需檔案。
- `Website/img/products/apple.png`、`Website/img/products/appol.png`、`Website/img/products/img2.png`、
  `Website/img/products/human99/laptop.png` 已在 Git 中，但未列入 `Website.csproj` 的 `<Content Include>`；
  以 Web Application 專案發佈時若需要這些圖片，請在 Visual Studio 將它們加入專案。
- `Website/css/style.css` 與 `Website/Properties/AssemblyInfo.cs` 被 `Website.csproj` 引用，但不在 Git 中。

### 5.1 缺少 `Properties/AssemblyInfo.cs`（建置失敗）

`Website.csproj` 引用了 `Properties\AssemblyInfo.cs`，但 Repo 中沒有此檔。擇一處理：

- 建立 `Website/Properties/AssemblyInfo.cs`，最少內容：

  ```csharp
  using System.Reflection;
  using System.Runtime.InteropServices;

  [assembly: AssemblyTitle("Website")]
  [assembly: ComVisible(false)]
  [assembly: AssemblyVersion("1.0.0.0")]
  [assembly: AssemblyFileVersion("1.0.0.0")]
  ```

- 或在 Visual Studio 方案總管中，將顯示為遺失的 `AssemblyInfo.cs` 從專案中移除。

### 5.2 iTextSharp 參考路徑（建置失敗）

`Website.csproj` 中 iTextSharp 的 `HintPath` 指向原作者電腦上的 `..\..\..\..\Files\OrderInvoice\itextsharp.dll`。擇一處理：

- 透過 NuGet 安裝：套件管理器主控台執行 `Install-Package iTextSharp -Version 5.5.13.3`，再移除舊的 `itextsharp` 參考；
- 或將 `itextsharp.dll` 放到專案可存取的位置，於「參考」中重新加入。

### 5.3 缺少 `css/style.css`（僅影響外觀）

所有頁面都以 `<link href="css/style.css">` 引用共用樣式，但此檔未納入版控。網站仍可執行，
只是會失去共用樣式（各頁的內嵌 `<style>` 與 inline style 仍有效）。可自行建立空白的
`Website/css/style.css` 以消除 404。

### 5.4 NuGet 還原

`packages/` 已簽入 Roslyn 編譯器套件，一般不需額外動作。若出現 `csc.exe` 找不到之類的錯誤，
在方案上按右鍵執行「還原 NuGet 套件」，或於命令列執行：

```powershell
nuget restore Website.sln
```

---

## 6. 建置與執行

### Visual Studio

1. 開啟 `Website.sln`。
2. 確認 `Website` 為啟始專案（預設起始頁為 `index.aspx`）。
3. `Ctrl+Shift+B` 建置方案。
4. `F5`（偵錯）或 `Ctrl+F5`（不偵錯）啟動，瀏覽器會開啟 `https://localhost:44337/index.aspx`。
5. 第一次使用 HTTPS 時，IIS Express 會詢問是否信任開發憑證，請選擇「是」。

### 命令列（Developer PowerShell for VS）

```powershell
nuget restore Website.sln
msbuild Website.sln /p:Configuration=Debug
& "C:\Program Files\IIS Express\iisexpress.exe" /path:"$PWD\Website" /port:8080
```

之後瀏覽 `http://localhost:8080/index.aspx`。

---

## 7. 建立測試帳號與賣家

### 一般會員

1. 從頁首「Register → User」進入 `register.aspx`。
2. 依表單規則填寫：
   - 姓名只能有英文字母與空白；
   - 密碼至少 8 碼，需含大寫、小寫、數字與特殊字元（`@$!%*?&`），且資料庫上限 20 字元；
   - 電話必須剛好 10 位數字；
   - 生日不可晚於今天；
   - 國家/州/城市的驗證器已被註解，保留 `Select` 也會被接受，請自行選擇實際選項。
3. 送出後系統自動指派 1～4998 的 `uid`，並導向登入頁。

### 賣家

`sellerRegister.aspx` 的 INSERT 只提供 13 個值（缺少 `uid`），在上述資料表結構下**會失敗**。
請改用以下方式建立賣家：

1. 以 `register.aspx` 註冊一般帳號；
2. 在資料庫將該帳號的 `uid` 改為大於 5000：

   ```sql
   UPDATE violet_user_login SET uid = 5001 WHERE username = '<你的帳號>';
   ```

3. 重新登入後，`profile.aspx` 會出現「Add a Product」按鈕與「Products Sold on Website」清單。

### 上架商品

`addProducts.aspx.cs` 以 6 個值寫入 `violet_products`（缺少 `stock`），在上述資料表結構下**會失敗**。擇一處理：

- 修改 `addProducts.aspx.cs`，將 INSERT 改為指定欄位：

  ```csharp
  "INSERT INTO violet_products (pname, price, pimage, category, uname, keywords) VALUES(@pname, @price, @Image, @category, @uname, @keywords)"
  ```

  上架後庫存預設為 0，再到 `profile.aspx` 的商品清單按「Edit」設定 Stock；
- 或直接以 SQL 新增商品（見第 3 節範例資料）。

> 上傳的圖片會存到 `Website/img/products/<登入帳號>/`，IIS 應用程式集區帳號需有此資料夾的寫入權限。

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
| 9 | Checkout → Place Order | 顯示訂單完成面板；`violet_order` 新增資料、`violet_cart` 清空 |
| 10 | 個人頁 | 顯示訂單歷史，可下載 `OrderInvoice.pdf` |
| 11 | Contact 送出留言 | `violet_contact` 新增一筆 |
| 12 | 忘記密碼 | 答對安全問題後寄出密碼信（需先完成第 9 節 SMTP 設定） |

---

## 9. 選用設定

### SMTP（忘記密碼寄信）

`forgotpass.aspx.cs` 使用 Gmail SMTP（`smtp.gmail.com:587`，SSL）。請將程式中的佔位字串替換為實際值：

- `new MailAddress("enter email id")` → 寄件者 Email
- `new NetworkCredential("enter email id", "enter password")` → Gmail 帳號與**應用程式密碼**（需啟用兩步驟驗證）

> 🔒 建議改從 `Web.config` 的 `appSettings` 或環境變數讀取，避免將密碼寫入原始碼。

### 發行（Release）

`Web.Release.config` 會在發行時移除 `compilation` 的 `debug` 屬性。可在 Visual Studio
「建置 → 發佈 Website」選擇「資料夾」或「IIS」目標。部署到 IIS 時：

- 應用程式集區使用 .NET CLR v4.0、整合式管線；
- 確保 `img/products/` 具寫入權限；
- 正式環境請使用獨立的 SQL 帳號，勿使用 `sa`。

---

## 10. 常見問題排除

| 症狀 | 原因與解法 |
| --- | --- |
| 編譯錯誤 `CS1525: Invalid expression term '<'` | 尚未替換 12 個 `<enter your database connection>` 佔位字串，見 4.2 |
| 編譯錯誤 `CS2001: Source file 'Properties\AssemblyInfo.cs' could not be found` | 見 5.1 |
| 找不到 `iTextSharp` 命名空間 | 見 5.2 |
| `Format of the initialization string does not conform to specification` | `Web.config` 仍是 `Add connection string here`，見 4.1 |
| `Invalid object name 'violet_xxx'` | 資料表未建立或連到錯誤的資料庫，見第 3 節 |
| `Column name or number of supplied values does not match table definition` | 賣家註冊或上架商品的位置式 INSERT 欄位數不符，見第 7 節 |
| `String or binary data would be truncated` | 欄位長度不足（例如 `category`、`password` 超過 20 字元） |
| 進入購物車頁出現 `NullReferenceException` | 未經首頁直接開啟購物車時 `Session["addproduct"]` 為 null；請先從首頁進入 |
| 註冊時卡住或出現「連線已開啟」錯誤 | `generateUID()` 抽到重複 uid 時未關閉連線；重新註冊即可 |
| 個人頁購物車徽章總是 0 | `profile.aspx.cs` 讀取 `Session["count1"]` 而非 `Session["count"]`（已知問題） |
| 下單後購物車徽章仍顯示舊數量 | 結帳未清空 `Session["count"]`，重新登入即恢復（已知問題） |
| 上傳圖片時 `Access to the path is denied` | IIS / IIS Express 執行帳號對 `img/products/` 無寫入權限 |
| 首頁商品圖片破圖 | `pimage` 路徑錯誤；頁面會自動改顯示 `img/products/human.png` |
