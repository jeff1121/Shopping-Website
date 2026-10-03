# Shopping Website 重構計畫

本文件是把現有 ASP.NET Web Forms 站重構為 .NET 10 ASP.NET Core MVC 的實施規格。它不取代 [Plan.md](Plan.md)。`Plan.md` 仍是 CI/CD 與 Azure 試運行的里程碑紀錄。本文件核准之後才寫程式、基礎設施與新的 ADR。

| 項目 | 內容 |
| --- | --- |
| 檔案 | `Refactory-Plan.md` |
| 狀態 | 已定案，尚未實施 |
| 日期 | 2026-10-04 |
| 現況依據 | [README.md](README.md)、[CONTEXT.md](CONTEXT.md)、[docs/adr/](docs/adr/)、`Website/` 原始碼 |
| 目標執行環境 | `net10.0`、ASP.NET Core MVC、Azure SQL、同一個資源群組 `rg-shopping` |

## 1. 結論

新站是一個 ASP.NET Core MVC 應用程式，Razor 產生 HTML，Tailwind 提供樣式。公開註冊只建立會員。賣家由種子管理者授予。介面預設繁體中文，可切換英文。視覺採用市集資訊架構與 Bento 商品格。

舊的 `Website/` Web Forms 專案留在儲存庫裡，`deploy.yml` 在切換 PR 之前繼續部署它。新站先有自己的建置與測試工作，不接管 Front Door。

資料放在同一台 Azure SQL 的新資料表。`violet_*` 保留到切換完成，讓舊站回滾仍讀得到舊資料。不搬移舊密碼雜湊。示範帳號在新結構重新建立。

測試有兩道獨立門檻，兩道都必須通過，不能互相抵銷：

1. `src/Shopping.Web` 與 `src/Shopping.Data` 的手寫 C#，Coverlet 行覆蓋率與分支覆蓋率都是 100%。
2. Playwright E2E 覆蓋第 9 節每一條使用者可見功能，含兩個語系，以及每個頁面在 375px 與 1440px 的版面檢查。

## 2. 已定案

| 項目 | 決定 |
| --- | --- |
| 第一版範圍 | 現有購物能力的對等功能，加上本文件列出的結構修正。不含金流、訂單狀態機、完整管理後台 |
| 資料庫 | 同一 Azure SQL 新增一組表。`violet_*` 留到切換完成 |
| 前端 | MVC Razor + Tailwind。不引入 React |
| 賣家資格 | 公開註冊只建立會員。管理者授予或收回賣家 |
| 介面語言 | `zh-Hant` 與 `en` 可切換。預設 `zh-Hant` |
| 視覺 | 市集結構 + Bento 商品格。Primary `#3B82F6`、CTA `#F97316`、標題 Rubik、內文 Nunito Sans |
| 執行環境 | `net10.0`。不升級到 .NET Framework 4.8，也不以 .NET 8 作為目標框架 |
| 庫存 | 下單時在單一交易扣庫存。購物車不預扣 |
| 忘記密碼 | 只留 30 分鐘、一次性的 Email 權杖。安全問題下線 |
| PDF | QuestPDF 重畫訂單 PDF。授權說明見第 13.3 節，不稱它為 MIT |
| 身分 | ASP.NET Core Identity。角色 `Customer`、`Seller`、`Operator` |
| 舊密碼 | 不搬移。示範帳號重設 |
| 環境 | 仍是一個資源群組。切換時新增 Linux App Service，舊 Windows App 先停不刪 |

管理者功能只有一頁：以使用者名稱或 Email 找到會員，授予或收回 `Seller`。這頁不做訂單管理、商品審核或金流。

## 3. 現況盤點

### 3.1 技術與部署

| 項目 | 現況 |
| --- | --- |
| 框架 | ASP.NET Web Forms、.NET Framework 4.7.2、C# |
| 資料 | ADO.NET 與 `asp:SqlDataSource` 並存。SQL Server。啟動時 DbUp 套用 `Website/Migrations/*.sql` |
| 執行 | 僅 Windows、IIS Express。macOS 不能建置舊站 |
| 方案 | `Website.sln`、舊式 `Website/Website.csproj` |
| 測試 | 無 |
| Azure | 資源群組 `rg-shopping`，試運行。App Service B1 Windows、Azure SQL Basic、Key Vault、Blob 容器 `products`、Front Door、ACS Email、Log Analytics、Application Insights |
| 身分基礎 | 使用者指派受控識別由 [infra/bootstrap.sh](infra/bootstrap.sh) 預先建立。Bicep 不指派角色（ADR 0004） |
| 環境數 | 一個環境，沒有 staging slot（ADR 0001） |
| Migration 執行點 | App 啟動，不用 GitHub runner 連 SQL（ADR 0002）。帳號 `shopping_migrator` 有 DDL，`shopping_app` 只讀寫 |
| PDF | iTextSharp 5.5.13.6，AGPL（ADR 0003）。組件目標是 .NET Framework，不能原樣用於 `net10.0` |
| 里程碑 | M1–M7 已完成。M8 正式營運尚未做完 |

頁面沒有 Master Page。頁首、頁尾與「已登入就切換頁首」在每個 `.aspx` 各寫一份。角色沒有權限表：`violet_user_login.uid` 1–4998 是會員，5001–9999 是賣家。

### 3.2 頁面與必須保留的能力

| 現有頁面 | 能力 | 新站去向 |
| --- | --- | --- |
| `index.aspx` | 商品清單、隨機／價格排序、關鍵字搜尋、分類篩選、選數量、加入購物車 | `GET /` |
| `categories.aspx` | 分類清單，連到該分類商品 | `GET /categories` |
| 無獨立商品頁 | 商品沒有穩定網址 | 新增 `GET /products/{id}` |
| `cart.aspx` | 改數量、移除、總額 | `GET /cart` |
| `checkout.aspx` | 建立訂單。無金流 | `GET/POST /checkout` |
| `login.aspx` | 使用者名稱或 Email 加密碼。登入後還原購物車 | `GET/POST /account/login` |
| `register.aspx` | 會員註冊 | `GET/POST /account/register` |
| `sellerRegister.aspx` | 任何人註冊成賣家 | 刪除。與 `CONTEXT.md` 衝突，已定案不保留 |
| `sellerSignIn.aspx` | 與登入相同，選單沒有連結 | 刪除 |
| `forgotpass.aspx` | 安全問題通過後寄重設信 | 改為直接寄權杖。安全問題下線 |
| `resetPassword.aspx` | 一次性權杖設新密碼 | `GET/POST /account/reset` |
| `profile.aspx` | 檢視資料；改電話、地址、國家、州、城市；訂單歷史；下載 PDF；賣家商品編輯與刪除 | `GET/POST /account` 與賣家商品動作 |
| `addProducts.aspx` | 賣家上架。新商品庫存 0 | `GET/POST /seller/products/new` |
| `sellerProfile.aspx` | 更新功能未完成 | 刪除 |
| `about.aspx`、`blog.aspx` | 靜態內容 | `GET /about`、`GET /blog`。兩套文案 |
| `contact.aspx` | 寫入聯絡留言 | `GET/POST /contact` |
| `error.aspx` | 一般訊息，不洩漏例外 | `GET /error/{statusCode}` |

示範帳號（密碼都是公開的 `Demo@1234`，不得當成真實密碼）：

| 使用者名稱 | Email | 現有角色 | 新站角色 |
| --- | --- | --- | --- |
| `demo_customer` | `demo_customer@example.com` | 會員 uid 1001 | `Customer` |
| `demo_seller` | `demo_seller@example.com` | 賣家 uid 5001 | `Customer` + `Seller` |
| 無 | 無 | 無管理者帳號 | 新增 `demo_operator`，角色 `Operator`。試運行使用同一個公開示範密碼，正式營運時跟著示範資料清除並更換 |

分類種子只有 `Computer` 與拼寫錯誤的 `Computer Accesories`。`img/categories/` 裡的服裝圖不是現用分類。新站種子改為正確拼寫 `Computer Accessories`。舊表的那一列不修改。

### 3.3 現有規則（新站仍要有對應測試）

這些規則來自現行程式，不是推測：

- 加入購物車必須已登入。未登入不改庫存，導向登入。
- 數量選項是 1 到 10，而且不超過當時庫存。庫存 0 顯示售完並停用按鈕。
- 現況在加入購物車時就 `UPDATE stock = stock - 數量 WHERE stock >= 數量`。放棄購物車不會歸還。新站改為下單才扣，這是結構修正。
- 購物車同時在 Session 與 `violet_cart`。下次登入還原。新站只存資料庫，登入後徽章顯示該會員的購物車數量。
- 結帳把每一列寫進 `violet_order`（沒有訂單主檔），`orderDate` 用 `ToShortDateString()` 的字串。訂單編號是 `#` + 未補零的時分秒日月年 + 5 碼隨機英數字。新站改為訂單主檔加明細，編號改為不可碰撞的格式。
- 付款方式只是文字。
- 上架檢查：必須有檔、副檔名 jpg／jpeg／png／gif／webp、不超過 2 MB、檔頭 magic bytes 與副檔名一致、名稱最多 50 字且不重複、關鍵字最多 500 字、價格最多兩位小數。路徑是 `products/<登入帳號>/<GUID>.<副檔名>`。新商品庫存 0，賣家之後才能把庫存改成可購買。
- 賣家只能改、刪自己的商品。價格與庫存須為 0 以上，關鍵字最多 500 字。
- 刪除確認目前掛在 Edit 按鈕上。新站把確認放在刪除動作。
- 密碼規則：至少 8 字，含一個大寫、一個小寫、一個數字、一個特殊字元 `@$!%*?&`。
- 電話恰好 10 位數字，且不重複。現況欄位型別是 `DECIMAL(10,0)`。新站改存字串，規則不變。
- Email 現況正則要求頂層網域 2 到 3 個字母，會拒絕較長的頂層網域。新站改以 `MailAddress` 能解析、長度不超過欄位、且含一個 `@` 為準。
- 姓名現況正則是 `[a-zA-Z ]*`，會拒絕中文姓名。新站顯示名稱允許字母、空白與 CJK，最長 50。使用者名稱仍是 1 到 20 碼英數字。
- 生日不可是未來日期。
- 註冊的國家／州／城市驗證被註解，州與城市是固定印度地名。新站改為必填自由文字，並加長欄位。
- 個人資料可改的只有電話、地址、國家、州、城市。姓名、Email、使用者名稱、生日、性別維持唯讀。
- 重設權杖是 32 位元組隨機值的 Base64URL。資料庫只存 SHA-256。30 分鐘、一次有效。同一帳號再申請時，尚未使用的舊權杖作廢。新站改用 Identity 權杖，期限同樣 30 分鐘。
- 安全問題答錯 5 次就停止。安全問題移除後，改為同一帳號每小時最多寄出 5 封重設信。
- 聯絡留言欄位是姓名、Email、訊息。訊息現況最長 500。
- 回應標頭：`X-Frame-Options: DENY`、`X-Content-Type-Options: nosniff`、`Referrer-Policy: strict-origin-when-cross-origin`、`Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=()`、CSP、移除 `X-Powered-By`。HSTS 只在正式環境。Cookie 為 `HttpOnly`、`Secure`、`SameSite=Lax`。
- CSP 目前含 `script-src 'unsafe-inline' 'unsafe-eval'` 與 `style-src 'unsafe-inline'`，原因是 Web Forms。新站移除這三個放寬。

### 3.4 保留的部署約束

- 秘密不進版控。連線、SMTP、Blob、圖片基底網址繼續來自環境變數與 Key Vault。
- App 啟動時套用 migration。GitHub-hosted runner 不開 SQL 防火牆。
- 角色指派仍在 bootstrap，不放進 Bicep。
- 切換前不重建資源群組，不新增第二套環境。
- 示範資料與試運行資料仍要能用 [db/cleanup/remove_demo_data.sql](db/cleanup/remove_demo_data.sql) 的精神清除。新站另備一份針對新表的清除腳本，不在啟動時自動執行。

## 4. 範圍

### 4.1 要交付的能力

- 瀏覽、搜尋、排序、分類、商品詳情、購物車、無金流結帳、訂單歷史、訂單 PDF。
- 會員註冊、登入、登出、忘記密碼、重設密碼、個人資料。
- 賣家上架、修改自己的價格／庫存／關鍵字、刪除自己的商品、圖片進 Blob。
- 管理者授予與收回賣家。
- 關於、部落格、聯絡、404 與 500 錯誤頁。
- 繁中與英文介面。
- 響應式版面：375、768、1024、1440。
- 第 16 節的測試與覆蓋率門檻。

### 4.2 不做

- 信用卡、Stripe、PayPal 或任何扣款。
- 訂單狀態（待付款、出貨、完成、退貨）。下單成功即為一筆完成的訂單紀錄。
- 訪客購物車。未登入不能加入購物車。
- 商品多圖、商品規格、庫存預約超時、願望清單、評價、優惠券。
- 完整管理後台：訂單列表、代客下單、分類 CRUD、公告編輯、稽核畫面。
- 把舊 `violet_*` 資料列搬進新表。分類與示範帳號、示範商品在新結構重新種子化。
- 自動翻譯會員輸入的商品名稱、關鍵字、地址、聯絡留言。
- 在切換 PR 之前改變現有 `deploy.yml` 的部署目標。
- 刪除 `Website/` 或改寫舊頁。切換完成後的刪除是後續工作，不在本計畫的完成定義裡。

### 4.3 結構修正

這些是對現況的刻意改變。E2E 要鎖定新行為，不要鎖定舊缺陷。

| 編號 | 現況 | 新行為 |
| --- | --- | --- |
| C1 | 加入購物車預扣庫存 | 購物車只記數量。下單交易內檢查並扣除 |
| C2 | `uid` 範圍代表角色 | Identity 角色 |
| C3 | 公開賣家註冊 | 沒有這條路徑。管理者授予 |
| C4 | 商品名稱與姓名當關聯鍵 | 代理鍵。訂單明細快照名稱與單價 |
| C5 | 訂單編號可能碰撞、日期是字串 | 唯一編號 `V` + ULID。日期存 `datetimeoffset` |
| C6 | 安全問題答案明碼 | 移除安全問題 |
| C7 | 帳號存在與否會透過安全問題洩漏 | 忘記密碼不論帳號是否存在，畫面都是同一句話 |
| C8 | 絕對定位、約 1500px | 第 10 節的響應式版面 |
| C9 | 刪除確認掛在 Edit | 確認掛在刪除 |
| C10 | 姓名拒絕中文；地址是印度下拉 | 顯示名稱允許 CJK；地址為必填文字 |
| C11 | Email 頂層網域只能 2–3 個字母 | `MailAddress` 解析成功且長度合法 |
| C12 | CSP 允許 `unsafe-inline` 與 `unsafe-eval` | 不允許。字體自架，樣式來自建置後的 CSS 檔 |
| C13 | 無登入鎖定 | Identity 鎖定：連續失敗 5 次，鎖 15 分鐘 |
| C14 | 無商品網址 | 每項商品有 `GET /products/{id}` |
| C15 | 分類名 `Computer Accesories` | 新種子為 `Computer Accessories` |

## 5. 架構

### 5.1 方案

```text
Shopping-Website/
├── Shopping.sln
├── src/
│   ├── Shopping.Web/                 MVC、Razor、應用服務、驗證
│   │   ├── Program.cs                只做組合。邏輯放在可測的擴充方法與類別
│   │   ├── Controllers/
│   │   ├── Models/                   檢視模型與輸入模型
│   │   ├── Services/                 應用服務與介面
│   │   ├── Localization/             SharedResources.*.resx
│   │   ├── Views/
│   │   ├── wwwroot/css/site.css      Tailwind 建置產物，納入版控
│   │   └── Migrations/*.sql          內嵌資源。不是 EF 產生的 Designer
│   └── Shopping.Data/                EF Core 對應、SQL migration 執行器、Blob、SMTP、PDF
├── tests/
│   ├── Shopping.Web.Tests/           單元 + WebApplicationFactory。負責 100% 覆蓋率
│   └── Shopping.E2E/                 Playwright。不列入覆蓋率分母
├── Website/                          舊站。切換前保持可部署
└── infra/                            切換 PR 才改
```

`Shopping.Web` 參考 `Shopping.Data`。`Shopping.Data` 不參考 Web。測試專案可以參考兩者。

不使用 EF Core Migrations 產生的 `*.Designer.cs` 與 `*ModelSnapshot.cs`。結構變更是手寫 SQL，由手寫的 `DatabaseMigrator` 在啟動時依檔名順序套用，並沿用「已套用腳本」紀錄表。這樣 Coverlet 的分母裡沒有產生檔，100% 才量得到手寫程式。

### 5.2 分層責任

| 層 | 放什麼 | 不放什麼 |
| --- | --- | --- |
| Controller | 驗證模型狀態、呼叫一個應用服務、選擇 View 或重新導向 | SQL、Blob SDK、寄信、組 PDF |
| 應用服務 | 交易邊界、授權決策、庫存與訂單不變量 | `HttpContext`、Razor |
| `Shopping.Data` | EF 對應、migration 執行器、SMTP、Blob、QuestPDF 繪製 | 頁面文案 |
| Razor | HTML、在地化字串、Tag Helper | 業務判斷 |

時間、亂數與權杖產生都從邊界注入：`TimeProvider`、Identity 的權杖提供者、郵件介面、圖片儲存介面。禁止在業務判斷裡直接呼叫 `DateTime.Now` 或 `Random`。

`Program.cs` 的每一條環境分支（開發例外頁、正式 HSTS、缺設定就拒絕啟動）都抽成具名方法，由 `WebApplicationFactory` 覆蓋。不把邏輯留在無法單獨呼叫的頂層陳述。

### 5.3 警告與語言

兩個新專案：`net10.0`、nullable enable、implicit usings、file-scoped namespace、`TreatWarningsAsErrors`。公開型別有繁體中文 `/// <summary>`。Commit 訊息沿用儲存庫慣例，使用繁體中文。

## 6. 領域模型

用語沿用 [CONTEXT.md](CONTEXT.md)：會員、賣家、商品圖片、示範資料、環境、營運階段。實施時要更新該文件的賣家定義，使「不能自行註冊、由管理者設定」與程式一致，並把角色來源從 `uid` 改為角色名稱。

### 6.1 角色

| 角色 | 誰有 | 能力 |
| --- | --- | --- |
| `Customer` | 每位註冊會員，包含賣家與管理者 | 購物車、下單、個人資料、自己的訂單 PDF |
| `Seller` | 種子賣家，或管理者授予的會員 | 上架與維護自己的商品。同時仍是會員 |
| `Operator` | 只有種子管理者。畫面與 API 都不能授予此角色 | 授予與收回 `Seller`。不能收回自己的 `Operator` |

收回 `Seller` 不刪除該賣家既有商品，也不刪除歷史訂單。收回後不能再上架或修改。既有商品仍可被購買。

### 6.2 新資料表

表名使用 `shop_` 前綴，與 `violet_*` 分開。字串用 `nvarchar`。金額用 `decimal(18,2)`。時間用 `datetimeoffset`，應用程式以 UTC 寫入。

`shop_user` 擴充 Identity 使用者：

| 欄位 | 約束 |
| --- | --- |
| Identity 標準欄位 | 使用者名稱最長 20，唯一。Email 最長 50，唯一 |
| `DisplayName` | 必填，最長 50。不必唯一 |
| `PhoneNumber` | 恰好 10 位數字，唯一 |
| `DateOfBirth` | 日期，必填，不可晚於寫入當日（UTC 的日期） |
| `Country`、`Region`、`City` | 必填，各最長 50 |
| `Gender` | `Male`、`Female`、`Others` |
| `Address` | 必填，最長 500 |

`shop_category`：`Id`、`Name` 唯一且最長 50、`ImagePath`。

`shop_product`：

| 欄位 | 約束 |
| --- | --- |
| `Id` | 代理鍵，商品網址用它 |
| `Name` | 必填，唯一，最長 50 |
| `Price` | `>= 0`，最多兩位小數 |
| `ImageUrl` | 必填，最長 250。存完整圖片網址 |
| `CategoryId` | 必填，參考 `shop_category` |
| `SellerId` | 必填，參考使用者。刪除使用者時不連帶刪商品；本版沒有刪使用者功能 |
| `Keywords` | 可空，最長 500 |
| `Stock` | 整數，`>= 0`。新商品為 0 |

`shop_cart_line`：`UserId` + `ProductId` 唯一。`Quantity` 為 1 到 10 的整數。不存單價。顯示時讀商品現價。商品被賣家刪除時，該購物車列一併刪除。

`shop_order`：`Id`、`Number`（`varchar(32)`，唯一）、`UserId`、`CreatedAt`、`Total`。

`shop_order_line`：`OrderId`、`ProductId` 可空、`ProductName`、`UnitPrice`、`Quantity`、`LineTotal`。`ProductName` 與 `UnitPrice` 是下單當時的快照。賣家之後改價、改名或刪除商品，訂單內容不變。`LineTotal = UnitPrice * Quantity`，由服務計算，不信任客戶端傳來的金額。

`shop_contact_message`：`Id`、`Name` 最長 50、`Email` 最長 50、`Message` 最長 500、`CreatedAt`。

`shop_password_reset_quota`：`UserId`、`WindowStart`、`Count`。用來實施每小時 5 封。沒有帳號的申請不寫入，也不寄信。

Identity 的角色、使用者角色、權杖資料表使用框架預設名稱，放在同一個資料庫。

不建立指向 `violet_*` 的外鍵。兩組表互不讀寫。

### 6.3 不變量

1. 註冊完成的使用者恰好有 `Customer`，沒有 `Seller`，沒有 `Operator`。
2. 購物車數量變更不修改 `shop_product.Stock`。
3. 下單是單一資料庫交易：鎖定相關商品列、每一列 `Stock >= 購買量`、扣除、寫入訂單與明細、刪除該會員購物車。任一步失敗就整筆回滾。
4. 兩筆同時結帳同一件最後庫存時，恰好一筆成功。
5. 訂單編號在資料庫有唯一索引。產生碰撞時重試一次；再碰撞則失敗並留下購物車。
6. 賣家修改商品時，`SellerId` 必須是目前使用者。不符合就拒絕，不洩漏另一位賣家的商品是否存在：對外是 404。
7. 訂單 PDF 只發給該訂單的 `UserId`。其他人 404。
8. 重設信的畫面不區分「沒有這個人」與「已經寄出」。測試信箱只在帳號存在時收到信。
9. 圖片位元組與副檔名的 magic bytes 不一致時不上傳、不寫商品。
10. 新商品 `Stock = 0`，目錄上顯示售完，不能加入購物車。

## 7. 應用服務

每個服務都是覆蓋率範圍內的具體類別。Controller 保持薄。

| 服務 | 責任 |
| --- | --- |
| `CatalogService` | 關鍵字、分類、排序、庫存顯示。關鍵字比對名稱與 `Keywords`，不分大小寫 |
| `CartService` | 加入、改數量、移除、總額。數量合併到同一商品列，上限 10，且不可超過目前庫存 |
| `CheckoutService` | 第 6.3 節的交易。成功回傳訂單編號 |
| `RegistrationService` | 建立會員與 `Customer`。重複的 Email、使用者名稱、電話各自有錯誤碼 |
| `ProfileService` | 只更新電話、地址、國家、州、城市 |
| `PasswordResetService` | 配額、寄信、重設。寄信失敗時權杖流程對使用者顯示「暫時無法寄信」 |
| `ProductAdminService` | 上架與維護。僅 `Seller`，且僅自己的商品 |
| `SellerRoleService` | 僅 `Operator`。授予與收回 `Seller` |
| `ContactService` | 寫入留言 |
| `InvoiceService` | 讀自己的訂單並產生 PDF 位元組 |
| `DatabaseMigrator` | 啟動時套用內嵌 SQL。使用應用程式鎖或既有的 SQL applock，避免多執行個體同時改結構 |
| `ImageStore` | 副檔名、大小、magic bytes、上傳、回傳完整網址 |
| `SmtpEmailSender` | 實際 SMTP。Web 測試與 E2E 改用記憶體實作 |

排序三選一：`random`、`price-asc`、`price-desc`。預設 `random`。隨機順序用資料庫端的隨機，不在記憶體排序後再分頁。本版沒有分頁，商品量維持示範站規模；查詢仍必須是參數化。

關鍵字空白或未提供時不套用關鍵字條件。分類 id 不存在時，目錄是空清單，頁面仍是 200。

## 8. 路由與授權

| 方法與路徑 | 授權 | 說明 |
| --- | --- | --- |
| `GET /` | 匿名 | 目錄 |
| `GET /categories` | 匿名 | 分類 |
| `GET /products/{id}` | 匿名 | 不存在為 404 |
| `GET /cart` | 會員 | 未登入導向登入，並帶回來源 |
| `POST /cart` | 會員 | 加入 |
| `POST /cart/{productId}` | 會員 | 更新數量或移除 |
| `GET /checkout` | 會員 | 空購物車導回購物車 |
| `POST /checkout` | 會員 | 下單 |
| `GET/POST /account/register` | 僅匿名 | 已登入者導向目錄 |
| `GET/POST /account/login` | 僅匿名 | |
| `POST /account/logout` | 會員 | 必須是 POST，帶防偽造權杖 |
| `GET/POST /account/forgot` | 僅匿名 | |
| `GET/POST /account/reset` | 僅匿名 | |
| `GET/POST /account` | 會員 | 個人資料、訂單、賣家商品區 |
| `GET /account/orders/{number}/invoice` | 該訂單主人 | 其他人 404 |
| `GET/POST /seller/products/new` | `Seller` | 會員但非賣家為 403 |
| `POST /seller/products/{id}` | `Seller` 且是貨主 | 更新或刪除 |
| `GET/POST /operator/sellers` | `Operator` | 其他登入者 403 |
| `GET /about`、`GET /blog` | 匿名 | |
| `GET/POST /contact` | 匿名 | |
| `GET /error/404`、`GET /error/500` | 匿名 | 不用資料庫 |

所有 POST 都要防偽造權杖。缺少或錯誤的權杖回 400，不執行業務。

未登入存取會員頁時導向登入。已登入但角色不足時回 403，使用錯誤頁文案，不把人導去登入。

## 9. 功能規格

每條都有編號。實施完成時，每條至少一個單元或整合測試；使用者看得到的動作另外至少一個 E2E。E2E 欄的「兩語系」表示繁中與英文各跑一次斷言文案，資料操作可以只在繁中跑完整流程，英文跑同一流程的代表案例（表中標成「英文代表」）。

### 9.1 全站版面

| 編號 | 行為 | E2E |
| --- | --- | --- |
| FR-01 | 每個頁面使用同一個版面：標誌回目錄、主要連結、搜尋、語系切換 | 每個路由都斷言版面存在 |
| FR-02 | 匿名顯示登入與註冊。已登入顯示帳戶、登出、購物車數量 | 兩種狀態 |
| FR-03 | 賣家才看得到「上架商品」。管理者才看得到「賣家資格」 | 三種角色 |
| FR-04 | 購物車數量等於該會員購物車件數加總。登出後徽章消失，資料庫列還在 | 加入、登出、再登入 |
| FR-05 | 頁首有跳到主要內容的連結，鍵盤可達 | 一個代表頁 |
| FR-06 | 375px 時導覽可開關，打開後連結可點，關閉後不擋內容 | 目錄頁 |
| FR-07 | 語系切換寫入 Cookie。下一頁仍是該語系。預設繁中 | 切到英文再重新整理 |
| FR-08 | 搜尋框送出後到目錄，並帶關鍵字 | 版面上的搜尋 |

### 9.2 目錄、分類、商品

| 編號 | 行為 | E2E |
| --- | --- | --- |
| FR-10 | 目錄顯示商品圖、名稱、價格、庫存、賣家顯示名稱 | 有資料 |
| FR-11 | 預設順序是隨機。重新載入允許不同順序，但集合相同 | 斷言集合，不斷言順序 |
| FR-12 | 價格由低到高、由高到低。同價時以名稱升冪穩定排序 | 兩種排序 |
| FR-13 | 關鍵字比對名稱與關鍵字，不分大小寫，前後空白忽略 | 命中與大小寫 |
| FR-14 | 沒有命中時顯示空狀態，頁面 200 | 兩語系文案 |
| FR-15 | 分類頁列出全部分類。點分類後目錄只留該分類 | 點 `Computer` |
| FR-16 | 不存在的分類 id 得到空目錄，不是 500 | 是 |
| FR-17 | 庫存 0 顯示售完文字且加入按鈕停用。不能只靠顏色 | 售完商品 |
| FR-18 | 有庫存時數量選項為 1 到 min(10, 庫存) | 庫存 3 與庫存 20 各一 |
| FR-19 | 未登入按加入，導向登入，庫存與購物車不變 | 是 |
| FR-20 | 已登入加入後購物車出現該列，庫存數字不變 | 是 |
| FR-21 | 同一商品再加入會合併數量。合併後超過 10 或超過庫存則拒絕並留在原頁 | 兩種拒絕 |
| FR-22 | 商品詳情顯示圖、名稱、價格、庫存、分類、賣家、關鍵字，並可加入購物車 | 是 |
| FR-23 | 不存在的商品 id 為 404 錯誤頁 | 是 |
| FR-24 | 商品圖有替代文字（商品名稱）。載入失敗時仍有名稱文字 | 斷言 alt |

### 9.3 購物車與結帳

| 編號 | 行為 | E2E |
| --- | --- | --- |
| FR-30 | 未登入開啟購物車或結帳會去登入頁 | 兩個網址 |
| FR-31 | 空購物車有空狀態與回目錄連結，沒有結帳按鈕 | 是 |
| FR-32 | 改數量後小計與總額用現價重算 | 是 |
| FR-33 | 移除一列後總額更新，徽章更新 | 是 |
| FR-34 | 結帳頁列出品項、總額、今天的日期，並寫明沒有線上付款 | 兩語系 |
| FR-35 | 空購物車直接 POST 結帳不會產生訂單 | 整合測試為主，E2E 走一次 |
| FR-36 | 成功下單：庫存減少購買量、購物車清空、畫面出現訂單編號、訂單歷史看得到 | 是 |
| FR-37 | 明細單價是下單當下價格。其後賣家改價，訂單歷史不變 | 整合 + E2E |
| FR-38 | 庫存在結帳前被改到不夠：整筆失敗，購物車還在，庫存不變，畫面說明哪一項不夠 | 是 |
| FR-39 | 兩個工作階段同時買走最後一件：恰好一筆成功 | 整合測試。E2E 不強制並行 |
| FR-40 | 訂單 PDF 為附件 `OrderInvoice.pdf`，檔頭是 PDF，內容含訂單編號、品名、數量、總額 | E2E 下載並檢查檔頭與文字 |
| FR-41 | 別的會員用網址要這張 PDF 得到 404 | 是 |
| FR-42 | 賣家刪除商品後，已成立訂單的品名仍在 | 是 |

### 9.4 帳戶

| 編號 | 行為 | E2E |
| --- | --- | --- |
| FR-50 | 註冊欄位：顯示名稱、Email、使用者名稱、密碼、確認密碼、電話、生日、性別、國家、州／省、城市、地址。全部必填 | 空白送出，每個錯誤在欄位旁 |
| FR-51 | 密碼不符合複雜度時拒絕 | 是 |
| FR-52 | 確認密碼不同時拒絕 | 是 |
| FR-53 | 電話不是恰好 10 位數字時拒絕 | 是 |
| FR-54 | 未來生日拒絕 | 是 |
| FR-55 | 中文顯示名稱可以註冊成功 | 是 |
| FR-56 | Email、使用者名稱、電話、以及 Email 格式不合法，各自有錯誤。頂層網域超過 3 個字母的合法信箱可以註冊 | 各一 |
| FR-57 | 成功後是 `Customer`，不是 `Seller`。接著可登入 | 登入後看不到上架 |
| FR-58 | 已登入再開註冊頁會離開該頁 | 是 |
| FR-60 | 登入可用使用者名稱或 Email。錯誤密碼留在登入頁 | 兩種成功、一種失敗 |
| FR-61 | 連續 5 次錯誤密碼後鎖定 15 分鐘。鎖定期內正確密碼也進不去 | 整合測試把鎖定時間縮到可等待的長度；E2E 跑縮短設定的主機 |
| FR-62 | 登出必須走 POST。登出後目錄變成匿名版面，再登入購物車還在 | 是 |
| FR-63 | 忘記密碼：存在與不存在的帳號畫面文案相同 | 兩語系各一 |
| FR-64 | 存在的帳號會寄出一封含重置連結的信。不存在不寄 | 測試信箱斷言 |
| FR-65 | 同一帳號第 6 次申請（同一小時）不再寄信，畫面仍是同一句話 | 整合 + E2E |
| FR-66 | 寄信失敗時畫面說明暫時無法寄信，且不建立可用權杖 | 整合測試讓寄信拋出 |
| FR-67 | 有效權杖可設新密碼。舊密碼失效，新密碼可登入 | E2E |
| FR-68 | 過期權杖、已使用權杖、空權杖都失敗，且不改密碼 | 三個案例 |
| FR-69 | 再次申請使前一封連結失效 | 是 |
| FR-70 | 個人資料顯示唯讀的姓名、Email、使用者名稱、生日、性別 | 是 |
| FR-71 | 可改電話、地址、國家、州、城市。非法電話與重複電話拒絕 | 是 |
| FR-72 | 沒有訂單時訂單區是空狀態 | 是 |

### 9.5 賣家與管理者

| 編號 | 行為 | E2E |
| --- | --- | --- |
| FR-80 | 非賣家開啟上架頁為 403。未登入則去登入 | 會員與匿名 |
| FR-81 | 上架成功後庫存為 0，商品出現在該賣家的清單，目錄上是售完 | 是 |
| FR-82 | 未選檔、副檔名不允許、超過 2 MB、magic bytes 不符、名稱超過 50、關鍵字超過 500、名稱重複、價格不是非負且最多兩位小數：各自拒絕，且不留下 Blob 物件 | 每種 E2E 或整合。magic bytes 的 gif／webp／png／jpeg 正例與反例在單元測試全部分支；E2E 至少走 png 成功、改副檔名的 png、超過大小 |
| FR-83 | 允許的副檔名是 jpg、jpeg、png、gif、webp，不分大小寫 | 單元測試全部分支 |
| FR-84 | Blob 路徑是 `<使用者名稱>/<GUID>.<副檔名>`。資料庫存完整網址 | 整合測試對 Azurite |
| FR-85 | 賣家可改自己的價格、庫存、關鍵字。庫存改成大於 0 後，目錄可購買 | E2E |
| FR-86 | 價格或庫存小於 0，或關鍵字超過 500，拒絕且不寫入 | 是 |
| FR-87 | 刪除前有確認。確認後商品消失，別人的購物車裡這一列也消失 | 是 |
| FR-88 | 非貨主的更新或刪除得到 404，商品不變 | 賣家 A 對賣家 B 的 id |
| FR-89 | 管理者以使用者名稱或 Email 找到會員。找不到有空結果 | 兩種 |
| FR-90 | 授予後該會員看得到上架，可以上架。收回後上架頁變 403，既有商品仍在目錄 | 是 |
| FR-91 | 會員或賣家開啟管理者頁為 403 | 是 |
| FR-92 | 不能從這頁授予或收回 `Operator`。不能收回自己的管理者角色 | 整合測試 |

### 9.6 靜態、聯絡、錯誤、安全

| 編號 | 行為 | E2E |
| --- | --- | --- |
| FR-100 | 關於與部落格在兩語系都有標題與內文，不是空頁 | 四個組合 |
| FR-101 | 聯絡表單三欄必填。Email 不合法或訊息超過 500 時拒絕 | 是 |
| FR-102 | 成功後顯示已送出，資料庫有一筆。匿名可送 | 是 |
| FR-103 | 未知網址回 404 與錯誤頁，不出現例外型別或堆疊 | 是 |
| FR-104 | 未處理例外回 500 與一般訊息。測試以測試組態中的拋出點觸發，正式組態不註冊該端點 | 整合 + 一條 E2E 對測試主機 |
| FR-105 | 回應含第 3.3 節的安全標頭。CSP 的 `script-src` 與 `style-src` 沒有 `unsafe-inline` 與 `unsafe-eval` | 每個 E2E 的代表回應都檢查 |
| FR-106 | 正式環境有 HSTS。開發與測試主機沒有，避免本機被瀏覽器釘死 | 整合測試兩種環境 |
| FR-107 | 身分 Cookie 是 `HttpOnly`、`Secure`、`SameSite=Lax` | 登入後讀 Set-Cookie |
| FR-108 | 無防偽造權杖的 POST 為 400，購物車與庫存不變 | 是 |
| FR-109 | 每個頁面在 375px 與 1440px 沒有水平捲軸，主要標題可見，第一個互動元件可點 | 第 16.4 節的頁面清單各兩次 |

## 10. 視覺規格

依據 UI/UX Pro Max 對市集／電商的檢索。產品建議是 Marketplace：搜尋是主要行動，分類要有圖像，信任資訊用藍與綠。第一個風格命中是 Vibrant & Block-based；本計畫不整站採用高能量色塊，因為結帳需要穩定的層級，而且該風格的無障礙評級並未直接給出 WCAG AA。商品區改用同一次檢索中、評級為 WCAG AA、且適合商品格的 Bento 卡片。

| 權杖 | 值 |
| --- | --- |
| Primary | `#3B82F6` |
| Secondary | `#60A5FA` |
| CTA | `#F97316` |
| Background | `#F8FAFC` |
| Surface | `#FFFFFF` |
| Text | `#1E293B` |
| Muted text | 最淺只用到能維持 4.5:1 的層級，不用 `#94A3B8` 當內文 |
| Success | 綠色，且售完／成功旁邊一定有文字或圖示 |
| 標題字 | Rubik |
| 內文字 | Nunito Sans |
| 內文行高 | 1.5 到 1.75 |
| 內文最小字級 | 16px |
| 容器 | 全站同一個 `max-w-7xl` |
| 圓角 | 卡片 `rounded-xl`（16px） |
| 動效 | 150–300ms，只動顏色、透明度、陰影。遵守 `prefers-reduced-motion` |
| 觸控 | 可點擊目標至少 44×44px |
| 圖示 | Lucide，24×24。不用 emoji 當圖示 |
| 斷點 | 375、768、1024、1440 都要在 E2E 或版面測試出現。功能測試的手機寬度用 375，桌面用 1440 |

頁面結構：

- 目錄：頁首搜尋、分類入口、商品格、信任列（配送說明、賣家、聯絡）、次要的「如何成為賣家」文字。成為賣家不是註冊選項，文案說明須由管理者授予。
- 商品格：圖在上，名稱最多兩行，價格與庫存、數量、加入購物車。Hover 只改陰影或邊框，不造成版面位移。
- 結帳：單欄。總額在明細之後。主按鈕使用 CTA 色。旁邊的文字說明不會向卡片扣款。
- 表單：每個控制項有 `<label for>`。錯誤用 `role="alert"`，出現在欄位旁，不只靠紅框。
- 焦點環在鍵盤操作時可見。
- 字體檔放在 `wwwroot/fonts`，CSP 的 `font-src` 維持 `'self'`。不在執行時向 Google Fonts 請求。

Tailwind 在建置時產出 `wwwroot/css/site.css`，該檔納入版控，讓沒有 Node 的環境仍能編譯 C#。CI 會再跑一次 Tailwind 建置，並在 git 有差異時失敗，避免產物與原始樣式分叉。

## 11. 在地化

- 支援的文化是 `zh-Hant` 與 `en`。預設 `zh-Hant`。不支援的文化值落到預設，不丟 500。
- 用 ASP.NET Core request localization。頁首切換寫入 Cookie。`?culture=en` 或 `?culture=zh-Hant` 也會寫入同一個 Cookie，方便測試與連結。
- 介面字串在 `SharedResources.resx` 與 `SharedResources.en.resx`。Razor 用 `IViewLocalizer`。驗證訊息用資源檔，不用寫死在屬性裡的英文句子。
- 兩份資源的鍵集合必須相同。測試掃描兩份 `.resx`，鍵不一致就失敗。
- 關於與部落格的段落也在資源檔，不寫死在 cshtml。
- 性別、排序、售完等列舉有兩邊的顯示字。存進資料庫的值仍是 `Male`／`Female`／`Others` 與排序代碼。
- 會員輸入的內容以寫入文字顯示，不翻譯。示範商品名稱維持英文。
- 驗證與模型錯誤在兩語系都要有 E2E：註冊空白送出各跑一次，斷言該語系的必填句存在。

## 12. 安全

- 參數化查詢只透過 EF Core。不把使用者輸入拼進 SQL。
- 授權用 `[Authorize]` 與政策：`Seller`、`Operator`、訂單主人。不在頁面裡比較 `uid`。
- 防偽造權杖覆蓋全部 POST。登出是 POST。
- 密碼用 Identity 的雜湊。重設權杖只在信裡出現一次，資料庫不存原始權杖。
- 忘記密碼的回應不透露帳號是否存在。日誌可以記失敗類型，不記收件者完整地址。
- 上傳：副檔名白名單、2 MB、magic bytes、檔名改 GUID。不採用原始檔名。
- 錯誤頁與日誌分離。回應不含堆疊、SQL、連線字串。
- 安全標頭見 FR-105 到 FR-107。CSP 的 `img-src` 允許 `'self'`、`data:` 與 `https:`，因為商品圖在 Front Door。其餘維持 `'self'`。
- 依賴審查繼續阻擋新的 GPL／AGPL。iTextSharp 不進入新專案，切換完成後例外可以從新專案拿掉。舊 `Website/` 在被刪除前仍可能觸發既有例外，這不構成新站採用 AGPL 的理由。
- 示範密碼只存在種子與文件。不新增真實秘密到 git。

## 13. 外部系統

### 13.1 圖片

正式環境用使用者指派受控識別連 Blob，容器 `products`，對外網址是 `IMAGE_BASE_URL`。本機與測試用 Azurite。`ImageStore` 的正式路徑與 Azurite 路徑都要有測試：正式路徑以注入的用戶端介面覆蓋分支，Azurite 整合測試覆蓋上傳、覆寫不會發生（GUID 檔名）、以及回傳網址前綴。

### 13.2 郵件

正式環境用現有 `SMTP_*` 設定。`SmtpEmailSender` 的成功、認證失敗、連線失敗由測試內的 SMTP 替身覆蓋，不排除在覆蓋率之外。Web 的功能測試與 E2E 注入 `Mailbox`，測試直接讀最後一封信的連結。

### 13.3 PDF 與授權

iTextSharp 5 不能作為 `net10.0` 的訂單 PDF 工具。新站使用 QuestPDF 畫一頁 A4：訂單編號、日期、品名、單價、數量、小計、總額。

QuestPDF Community License 3.0（2026-07-06 生效）是原始碼可得的商業授權，不是 OSI 的 MIT。資格包含以 OSI 授權散布的開源專案，以及年營收低於 100 萬美元等條件。本儲存庫是 MIT，以開源專案資格使用 Community License。實施時在 `docs/adr/` 寫明這項授權選擇與「若專案改為閉源或資格不再符合，就改為自行繪製或購買授權」。不要在 README 把 QuestPDF 寫成 MIT。

PDF 測試斷言：位元組以 `%PDF` 開頭，抽出的文字含訂單編號與品名。文字抽取用測試依賴，不進網站專案。

## 14. 設定

新站用環境變數繫結，名稱沿用舊站語意，避免切換時營運人員要記第二套名字：

| 變數 | 用途 |
| --- | --- |
| `SQL_SERVER`、`SQL_DATABASE`、`SQL_USER`、`SQL_PASSWORD`、`SQL_ENCRYPT` | 應用程式連線，帳號只有讀寫 |
| `SQL_MIGRATOR_USER`、`SQL_MIGRATOR_PASSWORD` | 只有啟動 migration 使用 |
| `SMTP_HOST`、`SMTP_PORT`、`SMTP_USER`、`SMTP_PASSWORD`、`SMTP_FROM` | 寄信 |
| `STORAGE_BLOB_ENDPOINT`、`STORAGE_CONTAINER`、`IMAGE_BASE_URL` | 圖片 |
| `AZURE_CLIENT_ID` | 使用者指派受控識別 |

缺少任一正式環境必要值時，啟動失敗，健康檢查回 500，日誌寫出缺少的名稱，不寫出值。這條分支在測試裡用一份不完整的設定啟動並斷言例外訊息含變數名稱。

開發設定放在 `appsettings.Development.json`，只含本機 Azurite 與本機 SQL 的非秘密預設，或明確要求環境變數。不把試運行的真實密碼寫進檔案。

## 15. 切換

切換是單獨的 PR，在第 18 節階段 0 到 6 都完成之後才做。

1. 在 `rg-shopping` 新增 Linux App Service，執行 `net10.0`。掛上既有的 `id-shopping-web`。不新建資源群組。
2. 新站的 migration 只建立 `shop_*` 與 Identity 表。不修改 `violet_*`。
3. 部署新站，但 Front Door 仍指向舊站。對新站的暫用主機名稱跑冒煙與 E2E 清單中的部署後子集：目錄、登入、加入購物車、下單、PDF、安全標頭、404。
4. 通過後把 Front Door 來源改到新站。舊 Windows App 停止，不刪除。
5. 回滾是把 Front Door 指回舊站並啟動它。因為 `violet_*` 沒有被新站寫入，舊站資料與回滾前一致。新站在切換後寫入的 `shop_*` 訂單不會出現在舊站；這一點寫進切換 PR 的說明。若已有真實訂單，就不把來源指回去，改為修正新站再部署。
6. 試運行仍是同一個環境。本計畫不把 M8 正式營運做完。

切換 PR 同時更新 README、QuickStart、冒煙腳本與 `CONTEXT.md`。在那之前，README 仍描述舊站，避免文件先於軟體。

## 16. 測試與 100% 覆蓋率

### 16.1 兩個門檻

| 門檻 | 定義 | 失敗條件 |
| --- | --- | --- |
| 程式覆蓋率 | `src/Shopping.Web` 與 `src/Shopping.Data` 每一個手寫 C# 檔的行覆蓋率與分支覆蓋率都是 100% | 任一個低於 100.0。99.9 不算通過。不四捨五入 |
| 功能覆蓋率 | 第 9 節每個 FR 都有測試。使用者可見者另有 E2E | 追溯表有空格 |
| 頁面覆蓋率 | 第 16.4 節每個路由在兩個語系、兩個寬度都開啟過 | 缺一個組合 |

E2E 不拿來湊 Coverlet 數字。100% 必須由 `tests/Shopping.Web.Tests` 在行程內達成，包含 `WebApplicationFactory` 走完的 Controller、篩選器、中介軟體與應用服務。Playwright 負責使用者看得見的行為與版面。

### 16.2 計入與不計入

計入：兩個 `src` 專案裡的 `.cs`。

不計入，而且不准用它們規避門檻：

- `tests/**`。
- Razor、`.resx`、SQL、CSS、JavaScript、靜態圖。這些由 E2E、資源鍵測試與 migration 結構測試負責。
- 舊的 `Website/**`。

禁止：

- `[ExcludeFromCodeCoverage]`。
- Coverlet 的 `Exclude`、`ExcludeByAttribute`、`ExcludeByFile`。
- 以註解或編譯符號把失敗分支從正式建置拿掉，只留在測試看不到的地方。
- 空的 `catch` 後不記錄、不轉譯，用來消掉一個分支。
- 測試裡 `Skip`。環境沒有 Docker 時測試失敗，不跳過。

分支的算法以 Coverlet 的 branch 為準，包含 `if`、`switch` 臂、`?.`、`??`、三元運算子、`catch`。每個臂都要有測試。魔法位元組的每種格式與每種不匹配都是明確測試，不靠迴圈碰巧掃到。

### 16.3 單元與整合要單獨點名的分支

下列分支很容易被 E2E 漏掉，必須在 `Shopping.Web.Tests` 有名字對得起來的測試：

- JPEG、PNG、GIF87a、GIF89a、WebP 的合法檔頭。
- 副檔名與檔頭不一致、檔案短於檔頭、空串流、不支援的副檔名、大小恰為 2 MB、大小為 2 MB + 1。
- 下單交易成功、庫存不足回滾、第二個並行成功者失敗、訂單編號碰撞後重試成功、第二次仍碰撞則失敗。
- 註冊重複 Email、使用者名稱、電話。
- 重設配額第 5 封寄出、第 6 封不寄、過期、已使用、再次申請使舊權杖失效、SMTP 拋出。
- 非貨主、非賣家、非管理者、匿名。
- 缺少每個必要環境變數時啟動失敗，訊息含該變數名稱。
- migration 在空資料庫可重複執行：跑兩次，第二次不改資料。腳本順序與「已套用」紀錄被斷言。
- 鎖定 5 次與鎖定期間內的成功密碼。
- 文化不在支援清單時落到 `zh-Hant`。
- 兩份 `.resx` 鍵集合相等。
- PDF 含編號與品名；別人的編號沒有檔案。
- 防偽造權杖缺失。
- 500 回應不含例外型別名稱。

資料庫整合測試使用 Docker 上的 SQL Server，不用 EF InMemory。InMemory 不實作交易與唯一索引，會讓第 6.3 節的不變量變成假通過。Blob 整合測試使用 Azurite。

每個測試自己建立資料並在結束時刪掉，或使用獨立資料庫。測試之間不能靠執行順序。

### 16.4 E2E 頁面清單

以下路由都要在 `zh-Hant` 與 `en`、寬度 375 與 1440 開啟。此檢查對應 FR-109，並確認該語系的頁首文案。功能細節由第 9 節的 E2E 欄負責，不必把每個 FR 再乘上四種組合。

| 路由 | 開啟身分 |
| --- | --- |
| `/` | 匿名、會員 |
| `/categories` | 匿名 |
| `/products/{id}` | 匿名。另加一條不存在的 id |
| `/cart` | 會員。匿名預期被帶到登入 |
| `/checkout` | 有購物車的會員。空購物車的會員預期回購物車 |
| `/account/register` | 匿名 |
| `/account/login` | 匿名 |
| `/account/forgot` | 匿名 |
| `/account/reset` | 匿名，帶一個無效權杖 |
| `/account` | 會員、賣家 |
| `/seller/products/new` | 賣家。會員預期 403 |
| `/operator/sellers` | 管理者。會員預期 403 |
| `/about`、`/blog`、`/contact` | 匿名 |
| `/error/404` | 匿名 |
| 不存在的路徑 | 匿名，預期 404 頁 |

E2E 專案是 `tests/Shopping.E2E`，使用 `Microsoft.Playwright.NUnit`。測試主機由集合 fixture 以測試組態啟動：本機 SQL、Azurite、記憶體信箱、縮短的鎖定時間。Playwright 不看開發者的 Chrome 設定檔。

功能 E2E 的最小劇本（每個劇本是一支測試，裡面可以含多個斷言）：

1. 匿名看目錄、排序、搜尋命中、搜尋落空、打開分類、打開商品、售完按鈕不能按、未登入加入會去登入。
2. 註冊中文姓名的新會員、登入、加入購物車、合併數量、改數量、移除、再加入、結帳成功、訂單歷史、下載 PDF、登出、再登入後購物車仍在（此劇本在結帳前先留一項）。
3. 庫存被改到不夠時結帳失敗，購物車還在。
4. 註冊驗證：空白、密碼不符、電話錯誤、未來生日、重複 Email。英文語系再跑一次空白表單。
5. 登入失敗與鎖定。
6. 忘記密碼的兩種帳號文案相同；存在的帳號從測試信箱完成重設；舊連結失效。
7. 修改個人資料的成功與重複電話。
8. 賣家上架 png、目錄售完、把庫存改成 2、會員買走 1、賣家刪除時有確認。
9. 會員對上架頁得到 403。賣家對別人的商品 POST 得到 404。
10. 管理者授予、該會員可以上架、收回後 403，商品仍在。
11. 關於、部落格、聯絡成功。兩語系都檢查標題。
12. 未知網址是 404 頁。測試用的拋出點是 500 頁且沒有堆疊。
13. 無權杖 POST 購物車，數量不變。
14. 頁首切換英文後重新整理仍是英文，再切回繁中。

### 16.5 追溯

`tests/Shopping.Web.Tests/CoverageMap.md` 不另開文件。追溯放在測試名稱裡：單元與整合測試的名稱以 `FR-xx` 開頭，E2E 亦然。一支測試可以對應多個 FR，名稱列出全部編號。CI 腳本掃描第 9 節的 FR 編號與測試名稱，缺號就失敗。這支掃描腳本放在 `tests/tools`，它的分支也要被自己的測試覆蓋，否則就寫成 `Shopping.Web.Tests` 裡的一個測試，直接讀原始碼檢查，避免第二套未覆蓋的工具。

採用後者：`RequirementTraceTests` 讀取 `Refactory-Plan.md` 的 FR 編號，以及兩個測試專案的原始碼，斷言每個編號至少出現在 `Shopping.Web.Tests` 一次；標了 E2E 的編號還要出現在 `Shopping.E2E` 一次。這支測試自己的名稱不含 FR 編號，避免自我滿足。它的每一條分支（檔案打不開、編號缺失、E2E 缺失）都有測試，方式是把掃描函式抽成純函式，餵入假的檔案內容。

### 16.6 本機如何跑

```bash
docker compose -f docker-compose.tests.yml up -d
dotnet test Shopping.sln --configuration Release --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

`docker-compose.tests.yml` 只包含測試用 SQL Server 與 Azurite，不包含網站。網站由測試主機啟動。

覆蓋率檢查讀取 Cobertura XML，對 `Shopping.Web` 與 `Shopping.Data` 兩個套件分別計算 line-rate 與 branch-rate。任何一個不等於 `1` 就以非零結束碼結束。合併後的總數達標但其中一個套件未達標，也算失敗。

E2E：

```bash
dotnet test tests/Shopping.E2E/Shopping.E2E.csproj --configuration Release
```

CI 先安裝 Playwright 瀏覽器。本機文件寫明同一條安裝指令。

## 17. CI

新工作流程 `.github/workflows/build-mvc.yml`，在 PR 與 `main` 上，路徑包含 `src/**`、`tests/**`、`Refactory-Plan.md`、該工作流程時執行。它不部署。

步驟：還原、檢查 Tailwind 產物沒有漂移、`dotnet build` 警告即錯誤、單元與整合測試、覆蓋率門檻、FR 追溯、Playwright、上傳失敗時的 trace。

既有 `build.yml` 與 `deploy.yml` 維持部署舊站，直到切換 PR。切換 PR 把部署物改為 `dotnet publish` 的新站，冒煙測試改打新路由：`/` 看得到示範商品、`/account/login`、`/categories`、示範圖片可經 Front Door 取得、安全標頭、未知路徑 404。回滾用 Front Door，不再依賴舊的 `site.zip` 語意；舊 `site.zip` 工作在切換後從部署流程移除，原始碼仍留著直到後續刪除舊站的 PR。

依賴審查對新專案生效。QuestPDF 不是 AGPL，不需要 AGPL 例外。若掃描器把 Community License 標成未知，在 PR 說明附上第 13.3 節的資格判斷，不把套件放進允許清單裡的 AGPL 例外。

## 18. 實施階段

每個階段結束時覆蓋率都是 100%。不採用「先寫功能、最後補測試」。階段以 PR 合併。PR 標題即 squash commit，繁體中文。

| 階段 | 內容 | 完成定義 |
| --- | --- | --- |
| 0 | `Shopping.sln`、兩個 src 專案、測試專案、Docker 測試依賴、CI 工作流程、覆蓋率腳本、空的 `DatabaseMigrator` 與一條無作用 SQL | 空方案的手寫 C# 已達 100% 行與分支。CI 綠。舊站部署未被修改 |
| 1 | SQL 結構、EF 對應、種子（分類、三個示範帳號、示範商品）、設計權杖、版面、在地化、目錄／分類／商品詳情唯讀 | FR-01 到 FR-18、FR-22 到 FR-24、FR-100、FR-109 的唯讀頁。覆蓋率 100% |
| 2 | 註冊、登入、登出、鎖定、忘記密碼、重設、個人資料 | FR-50 到 FR-72。覆蓋率 100% |
| 3 | 購物車與結帳交易、訂單歷史 | FR-19 到 FR-21、FR-30 到 FR-39、FR-42。覆蓋率 100% |
| 4 | PDF、上架、Blob、賣家維護、管理者授予 | FR-40、FR-41、FR-80 到 FR-92。覆蓋率 100% |
| 5 | 聯絡、錯誤頁、安全標頭、CSP、防偽造的負面測試 | FR-101 到 FR-108。覆蓋率 100% |
| 6 | 補齊全部 E2E 劇本與 FR 追溯測試。修測試找出的缺陷 | 第 9 節每個 FR 都有測試名稱。E2E 清單全綠。覆蓋率仍是 100% |
| 7 | 切換 PR：Linux App、Front Door、文件 | 第 15 節的暫用主機名稱冒煙通過後才改來源 |

階段 1 起，每個使用者看得到的 PR 都在桌面 1440 與 375 走完該階段的新流程，再合併。自動化 E2E 從該流程第一次出現就加進來，不等到階段 6 才第一次點頁面。階段 6 是補齊矩陣與追溯，不是第一次寫 E2E。

ADR 在對應階段寫入 `docs/adr/`，至少包括：

- 以 `net10.0` MVC 重寫，舊站保留到切換。
- 新表與 `violet_*` 並存，啟動時由 App 套用 SQL。
- 賣家由管理者授予。
- 訂單 PDF 改用 QuestPDF Community License，取代 ADR 0003 對新站的效力。ADR 0003 在舊站刪除前仍描述舊站。
- 介面 `zh-Hant` 與 `en`。

## 19. 風險

| 風險 | 處理 |
| --- | --- |
| 單一環境沒有預演場 | 新站先用暫用主機名稱驗證。Front Door 切換是最後一步。舊表不改，所以舊站還能啟動 |
| 切換後的新訂單在舊站看不見 | 一旦有非示範訂單，回滾改為修新站，不把流量指回舊站 |
| QuestPDF 資格 | 以 MIT 開源專案資格使用。閉源前必須換工具或買授權 |
| 100% 分支迫使程式保持小而可測 | 這是本計畫的設計約束。邏輯放在服務裡，不放在 Razor |
| Playwright 在 CI 不穩定 | 失敗保留 trace。不把失敗測試改成 Skip。不穩定測試修到可重複 |
| 中文姓名與現有英文示範資料並存 | 顯示名稱允許 CJK。種子商品名稱維持英文，不算翻譯缺陷 |
| 覆蓋率腳本只看總計 | 第 16.6 節要求兩個套件分別達標 |
| 舊站 PR 與新站 PR 互相衝突 | 新站不修改 `Website/**`。舊站的安全修補若發生，不移植行為到已定案的結構修正上 |

## 20. 完成定義

以下全部為真時，本計畫才算做完：

- 第 4.1 節的能力在新站可用，第 4.2 節的項目沒有被加進去。
- 第 4.3 節的結構修正都有測試鎖定。
- Coverlet 對 `Shopping.Web` 與 `Shopping.Data` 的行覆蓋率與分支覆蓋率都是 100%。
- 第 9 節每個 FR 都能在測試名稱裡找到。使用者可見的 FR 能在 E2E 專案找到。
- 第 16.4 節的路由在兩語系與兩個寬度都有 E2E。
- 舊站在切換前仍由現有工作流程部署。切換後 Front Door 指向新站，舊 App 停止但未刪除。
- README、QuickStart、`CONTEXT.md` 與 ADR 在切換 PR 與程式一致。
