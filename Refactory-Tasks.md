# Shopping Website 重構工作細項

本文件把 [Refactory-Plan.md](Refactory-Plan.md) 拆成可以逐項完工的工作。規格以該計畫為準。本文件補的是檔案位置、實作選擇、測試名稱與完成標準，讓下一個 Session 不必再做產品決策。

| 項目 | 內容 |
| --- | --- |
| 檔案 | `Refactory-Tasks.md` |
| 狀態 | 工作細項已寫好，程式尚未開工 |
| 日期 | 2026-10-04 |
| 必須先讀 | 本文件第 1 節、[Refactory-Plan.md](Refactory-Plan.md)、[CONTEXT.md](CONTEXT.md) |
| 不要先讀來改範圍 | [Plan.md](Plan.md) 是舊站 CI/CD 里程碑，不是這次重構的範圍 |

## 1. 下一個 Session 怎麼開工

1. 確認工作區含有 `Refactory-Plan.md` 與 `Refactory-Tasks.md`。沒有這兩份文件就不要從更舊的 `main` 開始寫新站。
2. 讀完本文件第 2 節與第 3 節。不要先改 `Website/**`。
3. 從第 4 節的任務總表由上往下做。一次只做一個任務編號，做完再進下一號。
4. 每個任務用自己的分支與一個 PR。分支名 `feat/mvc-tNN-簡短英文`，例如 `feat/mvc-t00-solution`。PR 標題用繁體中文，因為標題會變成 Squash commit。
5. 任務寫「完成」之前，該任務列出的指令都要在本機跑過，而且覆蓋率仍是 100%。不要把測試留到最後一個任務。
6. 計畫沒有寫、本文件也沒有寫的產品行為，不要自行加上。把缺口寫在 PR 說明，停下來問維護者。

本機在 macOS 上不能建置舊的 `Website.sln`。新站只用 `Shopping.sln`。

## 2. 全程都遵守的規則

- 目標框架只有 `net10.0`。不要建立 .NET Framework 或 `net8.0` 專案。
- 新方案 `Shopping.sln` 不加入 `Website/Website.csproj`。
- 不要修改 `.github/workflows/deploy.yml` 與 `.github/workflows/build.yml` 的部署目標。舊站持續部署到原來的 Windows Web App。Front Door 只負責商品圖片，不要把網站來源改到新站。
- 不要刪除或改寫 `Website/**` 的頁面與 C#。舊圖片可以複製到新站的 `wwwroot`，複製後兩邊各有一份。
- 不要讀寫 `violet_*`。不要把舊密碼雜湊搬進新表。
- 不要加金流、訂單狀態、訪客購物車、商品多圖、評價、優惠券、分類後台、訂單管理後台。
- 公開註冊只建立 `Customer`。沒有賣家註冊頁，也沒有獨立賣家登入頁。
- 購物車不改 `Stock`。只有下單交易可以減庫存。
- 介面預設 `zh-Hant`，可切 `en`。會員輸入的商品名稱、關鍵字、地址、留言不翻譯。
- 新的 `.cs` 沿用根目錄 `.editorconfig`：UTF-8 含 BOM、CRLF。公開型別要有繁體中文 `/// <summary>`。`TreatWarningsAsErrors` 為 true。nullable enable、implicit usings、file-scoped namespace。
- Markdown 維持 LF。改完 Markdown 後執行 `npx markdownlint-cli2 <檔案>`。
- 禁止 `[ExcludeFromCodeCoverage]`、Coverlet `Exclude`、測試 `Skip`。環境沒有 Docker 時讓測試失敗。
- 覆蓋率只承認 `tests/Shopping.Web.Tests` 在行程內打到的程式。Playwright 不拿來湊百分比。
- `Shopping.Web` 與 `Shopping.Data` 要分開計算。兩個套件的 line-rate 與 branch-rate 都必須是 `1`。合計達標但單一套件未達標，算失敗。
- 業務判斷裡不要呼叫 `DateTime.Now`、`DateTime.UtcNow` 或 `Random`。時間用注入的 `TimeProvider`。訂單編號用注入的產生器。
- Controller 不寫 SQL、不呼叫 Blob SDK、不寄信、不組 PDF。Razor 不做業務判斷。
- 秘密不進 git。示範密碼 `Demo@1234` 可以出現在種子與文件，因為它本來就是公開的假密碼。
- Commit 與 PR 標題使用繁體中文。

覆蓋率與 E2E 的定義在計畫第 16 節。本文件第 6 節把「哪些 FR 一定要出現在 E2E 測試名稱」列死，追溯測試依那份清單，不要各自解讀表格。

## 3. 已經定下來的實作選擇

計畫允許兩種放法的地方，這裡只留一種。

| 主題 | 採用 | 原因 |
| --- | --- | --- |
| Azure 網站 | 同一個 `rg-shopping`、同一個 Azure SQL。新 Web App 名為 `app-shopping-mvc-<suffix>`，跑在新的 Linux 方案。舊 Web App 保持運行 | 舊方案是 Windows，不能順便跑 Linux。兩個網址用來對照、審查與 Demo。Front Door 只服務圖片，網站不進 Front Door |
| SQL 檔位置 | `src/Shopping.Data/Migrations/*.sql`，編成 `Shopping.Data` 的內嵌資源 | `Shopping.Data` 不能參考 Web。計畫樹狀圖寫在 Web 底下，那會讓 migration 執行器讀不到檔 |
| ORM migration | 不用 EF Core Migrations，不提交 `*.Designer.cs` 與 `*ModelSnapshot.cs` | 產生檔會破壞 100% 覆蓋率 |
| 使用者主鍵 | Identity 預設字串鍵，`nvarchar(450)` | 少改框架資料表。商品、訂單、分類用 `int` 識別碼 |
| 使用者資料表 | `shop_user` | 計畫點名的表 |
| 其餘 Identity 表 | `AspNetRoles`、`AspNetUserRoles`、`AspNetUserClaims`、`AspNetUserLogins`、`AspNetUserTokens`、`AspNetRoleClaims` | 計畫寫明其餘表用框架預設名稱 |
| 訂單編號 | `V` 接 26 碼 ULID，全部大寫，總長 27，欄位 `varchar(32)` | 計畫的 C5 |
| 畫面上的訂單日期 | 以 `Asia/Taipei` 的日曆日顯示，資料庫存 UTC 的 `datetimeoffset` | CI 是 UTC。用伺服器當地時間會讓測試隨機器改變 |
| 刪除確認 | 帳戶頁的刪除表單含必填核取方塊「我確定刪除」。未勾選就回同一頁且不刪除 | 不能用內嵌 `window.confirm`，否則 CSP 不能拿掉 `unsafe-inline` |
| 寄信失敗 | 寄信拋出時更新該使用者的 `SecurityStamp`，讓剛剛產生的重設權杖失效。失敗不計入每小時 5 封 | 計畫要求失敗時不留下可用權杖 |
| 500 測試端點 | 只有設定 `Testing:ExposeFault` 為 `true` 才註冊 `GET /test/fault`。正式環境沒有這個鍵 | 正式組態不提供拋出點 |
| 缺設定 | 正式環境在 `Run` 之前拋 `InvalidOperationException`，訊息含缺少的變數名稱、不含值 | 行程起不來，就不另外做一個回 500 的健康檢查 |
| Cookie | 正式環境 `Secure`。測試主機用 HTTPS。E2E 的 Playwright 設定 `IgnoreHTTPSErrors` | HTTP 網站不會送出 `Secure` Cookie，FR-107 會假失敗 |
| 顯示名稱 | 去掉前後空白後，符合 `^[\p{L} ]{1,50}$` | 允許中文，拒絕數字。對應 C10 |
| 使用者名稱 | `^[A-Za-z0-9]{1,20}$` | 沿用長度上限，並排除空白與符號 |
| Email | 去除前後空白後長度 1 到 50，恰好一個 `@`，`new MailAddress` 成功，且 `Address` 與輸入相同 | 拒絕 `"名字" <a@b.com>`。頂層網域可以長於 3 個字母 |
| 密碼 | 沿用舊規則：至少 8 碼，含大寫、小寫、數字，以及 `@$!%*?&` 其中一個特殊字元。字元集只允許這些字元 | 計畫第 3.3 節 |
| 電話 | 恰好 10 位數字，唯一，存 `nvarchar(10)` | 不再用 `decimal` |

## 4. 任務總表

依賴指的是必須先合併的任務。同一列的「FR」是這項要新加上測試名稱的編號。先前任務的測試名稱要保留。

| 任務 | 內容 | 依賴 | 新的 FR 測試 |
| --- | --- | --- | --- |
| T00 | 新方案與專案骨架 | 無 | 無 |
| T01 | 測試用 SQL 與 Azurite | T00 | 無 |
| T02 | Migration 執行器與覆蓋率門檻 | T01 | 無 |
| T03 | 新站 CI，不部署 | T02 | 無 |
| T04 | ADR：採用 net10.0 MVC | T00 | 無 |
| T05 | 資料表、EF 對應、Identity | T02 | 無 |
| T06 | 種子與示範資料清除腳本 | T05 | 無 |
| T07 | Tailwind、字體、版面、語系 | T05 | FR-01、FR-05、FR-07 的程式路徑 |
| T08 | 目錄、分類、商品詳情 | T06、T07 | FR-10 至 FR-18、FR-22 至 FR-24、FR-100 |
| T09 | 上述頁面的第一批 E2E 與兩份 ADR | T08 | FR-01、FR-02 匿名、FR-06、FR-07、FR-08、FR-10 至 FR-18、FR-22 至 FR-24、FR-100、FR-109 的已存在路由 |
| T10 | 註冊 | T08 | FR-50 至 FR-58 |
| T11 | 登入、登出、鎖定 | T10 | FR-60 至 FR-62 |
| T12 | 忘記密碼與重設 | T11 | FR-63 至 FR-69 |
| T13 | 個人資料 | T11 | FR-70 至 FR-72 |
| T14 | 帳戶 E2E | T12、T13 | 第 6 節帳戶類的 E2E 名稱 |
| T15 | 購物車 | T11、T08 | FR-19 至 FR-21、FR-30 至 FR-33、FR-04 |
| T16 | 結帳與訂單 | T15 | FR-34 至 FR-39、FR-42 |
| T17 | 購物 E2E | T16 | 第 6 節購物類的 E2E 名稱 |
| T18 | 訂單 PDF | T16 | FR-40、FR-41 |
| T19 | 圖片與上架 | T14、T06 | FR-80 至 FR-84 |
| T20 | 修改與刪除自己的商品 | T19、T15 | FR-85 至 FR-88、FR-03 的賣家連結 |
| T21 | 管理者授予賣家 | T20 | FR-89 至 FR-92、FR-03 的管理者連結 |
| T22 | 賣家與管理者 E2E | T18、T21 | 第 6 節賣家類的 E2E 名稱 |
| T23 | 聯絡我們 | T07 | FR-101、FR-102 |
| T24 | 錯誤頁、安全標頭、啟動檢查 | T11、T15 | FR-103 至 FR-108 |
| T25 | 追溯測試與剩餘 E2E 矩陣 | T09、T14、T17、T22、T23、T24 | 補齊第 6 節尚未出現的編號 |
| T26 | 覆蓋率收斂 | T25 | 無新 FR。修到兩個套件都是 100% |
| T27 | 上線前核對 | T26 | 無 |
| T28 | 第二個 Web App，舊站保持運行 | T27 | 無 |
| T29 | 兩個網址寫進文件 | T28 | 無 |

T04 可以跟 T00 放在同一個 PR。除此之外不要合併任務。

## 5. 任務說明

### T00 — 建立新方案

要做的事：

- 在儲存庫根目錄建立 `Shopping.sln`。
- 建立 `src/Shopping.Web/Shopping.Web.csproj`、`src/Shopping.Data/Shopping.Data.csproj`、`tests/Shopping.Web.Tests/Shopping.Web.Tests.csproj`、`tests/Shopping.E2E/Shopping.E2E.csproj`。
- Web SDK 用 `Microsoft.NET.Sdk.Web`。Data 與兩個測試專案用 `Microsoft.NET.Sdk`。
- 四個專案都是 `net10.0`，`Nullable` 為 enable，`ImplicitUsings` 為 enable，`TreatWarningsAsErrors` 為 true。
- Web 參考 Data。Data 不參考 Web。兩個測試專案參考 Web。
- Web 的 `Program.cs` 只呼叫 `AppBootstrap`。建立 `src/Shopping.Web/AppBootstrap.cs`，裡頭有 `CreateBuilder` 與 `ConfigurePipeline`。這個階段的 pipeline 只需要一個 `GET /` 回傳純文字 `ok`，讓測試有分支可走。
- `AppBootstrap` 至少要有一個由環境決定的分支：測試與開發環境回傳詳細錯誤，其他環境不回傳詳細錯誤。兩邊都要有測試。
- E2E 專案這個階段可以只有一個被 `[Ignore]` 以外的空測試類別，但不要用 `Skip`。若還沒有 Playwright 套件，T09 再加。T00 的 E2E 專案要能 `dotnet test` 通過，可以用一個直接 `Assert.Pass` 的測試，測試名稱不要以 `FR-` 開頭。
- 在 `src/Shopping.Web/appsettings.json` 放空的設定結構，不要放密碼。`appsettings.Development.json` 只放本機預設：SQL 指向 `127.0.0.1,14333`、資料庫 `ShoppingTests`、`SQL_ENCRYPT` 為 `False`、Azurite 端點 `http://127.0.0.1:10000/devstoreaccount1`、容器 `products`、`IMAGE_BASE_URL` 為空字串。帳號密碼留空，改讀環境變數。

不要做的事：

- 不要加入 Tailwind、Identity、EF 套件以外的東西。T00 連 EF 都先不要加，EF 在 T05。
- 不要建立頁面。

測試：

- `AppBootstrap` 的兩個環境分支各一個測試。用 `WebApplicationFactory<Program>`。Web 專案把 `Program` 類別改成可存取，做法是在 Web 的 csproj 以外、測試可見的 `internal`，並加 `public partial class Program { }`。
- 跑 `dotnet test Shopping.sln --configuration Release`。這個階段還沒有覆蓋率腳本，但新增的 C# 分支都要被打到。T02 會把門檻接上，所以不要留下沒有測試的 `if`。

完成標準：

- `dotnet build Shopping.sln --configuration Release` 沒有警告。
- `Website.sln` 的專案清單沒有變。
- `git status` 沒有 `Website/**` 的修改。

### T01 — 測試用 SQL 與 Azurite

要做的事：

- 新增 `docker-compose.tests.yml`。只含兩個服務，不含網站。
- SQL 使用 `mcr.microsoft.com/mssql/server:2022-latest`，主機埠 `14333` 對應容器 `1433`，`ACCEPT_EULA=Y`。SA 密碼只給測試用，寫在 compose 檔，值固定為 `Test_only_9xQ`。這不是 Azure 密碼。
- Azurite 使用 `mcr.microsoft.com/azure-storage/azurite`，主機埠 `10000`。
- 新增 `docker/init-test-users.sql`。測試 fixture 在資料庫建立後執行它，建立 `shopping_migrator` 與 `shopping_app`。migrator 可以在 `ShoppingTests` 建立與修改資料表。app 只有該資料庫的讀寫，沒有 `CREATE TABLE`。密碼可以與 SA 相同，並在檔案開頭註明只用於本機測試。
- 在 `tests/Shopping.Web.Tests` 加一個集合 fixture：若連不上 `14333`，測試失敗並說明要先執行 `docker compose -f docker-compose.tests.yml up -d`。不要 `Skip`。

測試：

- 一個測試用 `shopping_app` 執行 `CREATE TABLE`，斷言失敗。
- 一個測試用 `shopping_migrator` 建立暫存表再刪除，斷言成功。

完成標準：

- `docker compose -f docker-compose.tests.yml up -d` 之後，上述兩個測試通過。
- compose 檔沒有 Azure 連線字串。

### T02 — Migration 執行器與覆蓋率門檻

要做的事：

- 在 `Shopping.Data` 實作 `DatabaseMigrator`。建構式接收 migrator 連線字串與一組已排序的腳本（名稱、SQL 文字）。正式宿主從組件內嵌資源載入 `src/Shopping.Data/Migrations/*.sql`。
- 腳本依檔名排序套用。紀錄表名 `shop_schema_version`，欄位 `ScriptName` 主鍵、`AppliedAt` 為 `datetimeoffset`。
- 套用全程先取 SQL Server 應用程式鎖，資源名稱 `shop_migrations`，獨占，擁有者 `Session`，逾時 60 秒。結束時釋放。連線中斷時鎖會自己釋放。
- 這個階段只放 `0001_schema_version.sql`：若紀錄表不存在就建立。腳本本身可重複執行。
- `AppBootstrap` 在 pipeline 建立前呼叫 migrator。測試宿主注入測試資料庫的 migrator 連線字串。DbContext 尚未存在，所以這個階段 Web 的請求仍不碰資料庫；只有啟動路徑會呼叫 migrator。
- 新增 `tests/Shopping.Web.Tests/CoverageGate.cs` 裡的純函式，輸入是 Cobertura XML 字串，輸出是否通過。規則：必須同時存在套件名稱結尾為 `Shopping.Web` 與 `Shopping.Data` 的節點；每個節點的 `line-rate` 與 `branch-rate` 都要能解析，而且數值等於 `1`。缺套件、缺屬性、`0.999` 都是失敗。
- 新增 `tests/check-coverage.sh`。它找 `TestResults` 底下的 `coverage.cobertura.xml`，把內容餵給同一套規則。為了不讓 shell 變成未覆蓋的第二實作，shell 只負責呼叫 `dotnet test` 與一個小程式 `tests/Shopping.Web.Tests` 已涵蓋的函式。做法：測試專案產出後，用 `dotnet run --project tests/CoverageGate --` 不合適，因為那是另一個未列入門檻的專案。改成把檢查函式放在 `Shopping.Data` 的 `CoverageReport` 類別，讓它屬於覆蓋率分母，並由單元測試覆蓋所有失敗分支。`tests/check-coverage.sh` 用 `dotnet test` 產生的組件無法輕易呼叫。實務做法限定如下：檢查函式放在 `Shopping.Data`，單元測試覆蓋它；`tests/check-coverage.sh` 用 Python 或 PowerShell 再實作一次相同規則，並在 `Shopping.Web.Tests` 用一段固定 XML 樣本鎖定規則。shell 與 C# 規則若分歧，以 C# 測試為準，PR 裡必須讓 shell 的判斷與 C# 一致。CI 執行 shell。
- 不要把 `CoverageReport` 做成只為測試而存在、正式程式用不到的大型框架。它可以是 `Shopping.Data` 的一個小類別，Web 不呼叫它。這是允許的，因為門檻工具必須在覆蓋率分母內。

測試：

- migrator：空資料庫執行兩次，`shop_schema_version` 只有一列。
- migrator：兩個並行工作同時執行，仍然只有一列，且不丟錯。
- `CoverageReport`：兩個套件都是 1 的 XML 通過。
- 缺一個套件、line-rate 為 0.999、branch-rate 缺漏、XML 不是 Cobertura，這四個分支都失敗。
- 跑覆蓋率收集，再跑檢查腳本，結束碼為 0。

完成標準：

- `bash tests/check-coverage.sh` 結束碼為 0。
- 原始碼沒有 `ExcludeFromCodeCoverage`。

### T03 — 新站 CI

要做的事：

- 新增 `.github/workflows/build-mvc.yml`。觸發條件是對 `main` 的 pull request 與 push，路徑包含 `src/**`、`tests/**`、`Refactory-Plan.md`、`Refactory-Tasks.md`、`docker-compose.tests.yml`、`docker/**`、`.github/workflows/build-mvc.yml`。
- 工作系統是 `ubuntu-latest`。步驟順序：取出原始碼、啟動 compose、還原、`dotnet build Shopping.sln --configuration Release`、執行 `tests/check-coverage.sh`。T09 之後才加入 Playwright。這個任務先把工作流程的 Playwright 步驟留成會檢查 `tests/Shopping.E2E` 是否已有 `Microsoft.Playwright` 套件參考；還沒有就略過瀏覽器安裝。略過必須是工作流程裡看得到的條件，不是測試裡的 `Skip`。
- `actions/checkout` 使用 `.github/workflows/lint.yml` 裡同一個 commit SHA。其他 action 也用 SHA 釘選。工作流程要通過 actionlint。
- 修改 `.github/workflows/lint.yml` 的 sqlfluff 步驟，讓它同時檢查 `src/Shopping.Data/Migrations` 與 `db`。不要拿掉既有的 `Website/Migrations`。目前若目錄不存在，條件要能略過，避免 T03 在 T05 之前就因為空目錄失敗。T05 之後該目錄必須真的被 lint。

不要做的事：

- 不要改 `deploy.yml` 與 `build.yml`。

完成標準：

- 新工作流程的 YAML 被 actionlint 接受。
- 本機 `bash tests/check-coverage.sh` 通過。

### T04 — ADR：執行環境

要做的事：

- 新增 `docs/adr/0005-net10-mvc-rewrite.md`。
- 寫下決定：新站是 `net10.0` ASP.NET Core MVC。舊 Web Forms 與新站以兩個 Web App 同時運行。不採用 .NET 8，因為支援在 2026-11-10 結束。不採用 .NET Framework 4.8。
- 後果：macOS 可以建置新站。Azure 上會有兩個網站網址。舊站的 Windows Web App 不停止。

完成標準：

- ADR 檔通過 markdownlint。
- 沒有改 README 的舊站描述。

### T05 — 資料表與 EF 對應

要做的事：

- 依計畫第 6.2 節建立手寫 SQL：`0002_identity_and_shop.sql`。先在本機暫存目錄用 `dotnet ef migrations script` 產生 Identity 10 的資料表 SQL，複製進這個檔，然後刪掉暫存目錄與任何 Designer 檔。複製後把使用者表改名為 `shop_user`，並加上計畫列出的欄位。其餘 Identity 表保持預設名稱。
- 同一檔或下一個檔建立 `shop_category`、`shop_product`、`shop_cart_line`、`shop_order`、`shop_order_line`、`shop_contact_message`、`shop_password_reset_quota`。欄位、長度、唯一鍵與外鍵依計畫。`shop_order.Number` 是 `varchar(32)` 唯一。`shop_cart_line` 的 `(UserId, ProductId)` 唯一。商品刪除時，資料庫用外鍵 `ON DELETE CASCADE` 刪掉該商品的購物車列。訂單明細的 `ProductId` 可空，商品刪除時把它設成 NULL，保留 `ProductName`。
- 不建立任何指向 `violet_*` 的外鍵。
- `ShoppingDbContext` 對應這些表。`ApplicationUser : IdentityUser` 放在 `Shopping.Data`，資料表名 `shop_user`。
- 金額 `decimal(18,2)`。時間 `datetimeoffset`。字串 `nvarchar`，訂單編號除外。
- Web 啟動時註冊 Identity：密碼規則依第 3 節、鎖定 5 次、鎖定 15 分鐘、要求唯一 Email、使用者名稱最長 20。角色 `Customer`、`Seller`、`Operator` 由種子建立，不在這個任務塞使用者。
- 重設權杖壽命 30 分鐘，設定 `DataProtectionTokenProviderOptions.TokenLifespan`。
- DbContext 使用 `shopping_app` 連線。Migrator 使用 `shopping_migrator` 連線。測試要證明請求路徑的 DbContext 連線字串不是 migrator 那條。

測試：

- 空資料庫套用全部腳本兩次，資料列數不增加，且不丟錯。
- `shopping_app` 不能刪除 `shop_product` 的資料表。
- 插入兩筆相同 `shop_order.Number` 時，第二筆失敗。
- 插入相同使用者名稱或相同電話時失敗。
- 刪除商品後，對應購物車列消失，訂單明細的品名還在、`ProductId` 為 NULL。

完成標準：

- 方案裡搜尋不到 `ModelSnapshot` 與 `Designer.cs`。
- sqlfluff 對新 SQL 沒有錯誤。必要時沿用舊 migration 的 `-- noqa` 寫法，但先試著寫到不需要 noqa。

### T06 — 種子與清除腳本

要做的事：

- 新增 `0003_seed.sql`。只在資料不存在時插入。
- 分類：`Computer`，圖片 `/images/categories/desktop.png`。`Computer Accessories`，圖片 `/images/categories/laptop.png`。不要寫入 `Computer Accesories`。
- 使用者三個，密碼都是 `Demo@1234`。密碼必須用 Identity 雜湊後再寫入，不要在 SQL 裡放明碼然後指望應用程式以後會補雜湊。做法：種子改由 `DemoSeeder` 類別在 migration 之後執行，用 `UserManager` 與 `RoleManager` 建立使用者。SQL 只種分類。使用者與商品若依賴使用者外鍵，也放在 `DemoSeeder`，讓密碼雜湊與外鍵一次做對。`DemoSeeder` 可重複執行。
- 使用者資料：

| 使用者名稱 | Email | 顯示名稱 | 電話 | 生日 | 性別 | 地址 | 角色 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `demo_customer` | `demo_customer@example.com` | Demo Customer | `9000000001` | 1995-05-05 | Female | Demo customer address | Customer |
| `demo_seller` | `demo_seller@example.com` | Demo Seller | `9000000002` | 1990-01-01 | Male | Demo seller address | Customer、Seller |
| `demo_operator` | `demo_operator@example.com` | Demo Operator | `9000000003` | 1985-01-01 | Others | Demo operator address | Customer、Operator |

國家、州／省、城市三個帳號都是 `Taiwan`、`Taipei`、`Taipei`。

- 商品四筆，賣家是 `demo_seller`。圖片在測試與本機使用網站相對路徑。`IMAGE_BASE_URL` 有值時，改存 `{IMAGE_BASE_URL}/products/demo_seller/<檔名>`，與舊站 Blob 路徑相同。

| 名稱 | 價格 | 分類 | 庫存 | 關鍵字 | 檔名 |
| --- | --- | --- | --- | --- | --- |
| Demo Smart Watch | 2999.00 | Computer Accessories | 20 | watch smart wearable demo | watch.png |
| Demo Laser Printer | 8999.00 | Computer Accessories | 5 | printer office demo | printer.png |
| Demo Laptop | 32999.00 | Computer | 10 | laptop notebook demo | laptop.png |
| Demo Arcade Machine | 15999.00 | Computer | 0 | arcade game demo | arcade.png |

- 把 `Website/img/categories/desktop.png`、`Website/img/categories/laptop.png` 與 `Website/img/products/demo_seller/` 的四張 png 複製到 `src/Shopping.Web/wwwroot/images/` 的對應位置。不要修改來源檔。
- 新增 `db/cleanup/remove_shop_demo_data.sql`。刪除這三個使用者的購物車、訂單明細、訂單、商品，再刪除使用者。不刪分類。檔案開頭寫明：不會在應用程式啟動時執行，正式營運前由人執行。它不修改 `violet_*`。

測試：

- 執行 seeder 兩次，使用者仍是三個，商品仍是四筆。
- `demo_customer` 的角色集合恰好是 `Customer`。
- `demo_seller` 同時有 `Customer` 與 `Seller`，沒有 `Operator`。
- `demo_operator` 沒有 `Seller`。
- 清除腳本在測試資料庫可執行。執行後三個示範使用者不見，分類還在。測試結束要重新種子，避免影響後續測試。更好的做法是清除測試使用獨立資料庫或在交易外重建種子。

完成標準：

- 明碼 `Demo@1234` 沒有出現在 `shop_user` 的密碼欄。測試讀取該欄，斷言它不等於 `Demo@1234`。

### T07 — 視覺基礎與語系

要做的事：

- 在 `src/Shopping.Web` 加入 Tailwind CSS 4 的建置。原始檔是 `src/Shopping.Web/Styles/site.css`，產出 `src/Shopping.Web/wwwroot/css/site.css`，產出檔納入版控。
- `package.json` 的 script 名稱是 `build:css`。CI 在 T09 的工作流程更新時執行它，然後 `git diff --exit-code -- src/Shopping.Web/wwwroot/css/site.css`。這個任務先讓本機能產出，並把產出檔提交。
- 權杖依計畫第 10 節：Primary `#3B82F6`、Secondary `#60A5FA`、CTA `#F97316`、背景 `#F8FAFC`、表面 `#FFFFFF`、文字 `#1E293B`。內文不要用 `#94A3B8`。成功色用綠色，而且售完不能只靠顏色。
- 字體使用 Rubik 與 Nunito Sans 的 SIL OFL `woff2`，放在 `wwwroot/fonts`。CSS 用 `@font-face` 指到站內檔。執行時不要請求 `fonts.googleapis.com` 或 `fonts.gstatic.com`。
- 全站容器使用同一個最大寬度，對應 Tailwind 的 `max-w-7xl`。卡片圓角 16px。連結與按鈕的觸控尺寸至少 44×44 CSS 像素。動效 150 到 300 毫秒，只動顏色、透明度或陰影。`prefers-reduced-motion: reduce` 時拿掉這些動效。
- 圖示使用 Lucide 的 SVG 檔，放在 `wwwroot/icons`，尺寸 24×24。不要用 emoji 當圖示，也不要裝 React 圖示套件。
- `_Layout.cshtml` 含：跳到 `#main` 的連結、標誌連到 `/`、主要連結、搜尋表單、語系切換、匿名時的登入與註冊、已登入時的帳戶與登出表單、購物車連結。賣家連結與管理者連結先不要出現，T20 與 T21 再加，避免這個任務的畫面超出測試範圍。登出表單是 POST，含防偽造權杖。
- 375px 時導覽預設收合。開關用 `wwwroot/js/nav.js`，不要把腳本寫在 cshtml 裡。
- 資源檔：`src/Shopping.Web/Localization/SharedResources.resx`（繁中，預設）與 `SharedResources.en.resx`。Razor 用 `IViewLocalizer`。這個階段先放版面與關於、部落格會用到的鍵。
- 語系：支援 `zh-Hant` 與 `en`，預設 `zh-Hant`。頁首 POST 到 `POST /culture`，寫入 Cookie。`GET` 請求若帶 `culture=zh-Hant` 或 `culture=en`，也寫入同一個 Cookie 再顯示該頁。不支援的值落到 `zh-Hant`，狀態仍是 200。
- 關於與部落格的標題和段落放在資源檔。路由 `GET /about`、`GET /blog`。
- 每個可輸入控制項使用 `<label for>`。錯誤訊息的容器使用 `role="alert"`。

測試：

- 資源鍵測試：兩份 resx 的鍵集合相等。這個測試從 T07 開始就存在，之後每加鍵都要兩邊一起加。
- 不支援的 `culture=fr` 回應是 200，而且 HTML 含繁中版面字串。
- `culture=en` 之後重新請求 `/about`，標題是英文。
- 回應的 HTML 不含 `fonts.googleapis.com`。
- 跳到主要內容的連結存在，目標是 `main`。

完成標準：

- `npm run build:css --prefix src/Shopping.Web` 之後，`site.css` 沒有未提交的差異。
- 版面在這個任務尚未要求 E2E。T09 會補。

### T08 — 目錄、分類、商品詳情

要做的事：

- 實作 `CatalogService`。關鍵字比對商品名稱與 `Keywords`，不分大小寫，忽略前後空白。空白關鍵字不加條件。排序只接受 `random`、`price-asc`、`price-desc`，預設 `random`。隨機用資料庫排序，不要先全部載進記憶體再打亂。同價格時名稱升冪。不存在的分類 id 回傳空清單，不丟例外。
- 路由：`GET /`、`GET /categories`、`GET /products/{id}`。不存在的商品是 404，使用稍後 T24 會換成正式錯誤頁的暫時檢視即可，但狀態碼必須已經是 404。T24 只換版面，不要改狀態碼。
- 目錄顯示圖、名稱、價格、庫存、賣家顯示名稱。圖片的 `alt` 是商品名稱。名稱最多兩行，用 CSS line clamp，不要用腳本。
- 庫存 0 顯示售完文字，加入按鈕停用，而且按鈕不要只靠灰色表達停用，旁邊要有售完文字。
- 數量選項是 1 到 `min(10, 庫存)`。庫存 0 沒有數量選項。
- 分類頁列出全部分類，連結到 `/?category={id}`。
- 目錄頁含信任列與一段「如何成為賣家」：說明由管理者授予，不要放註冊成賣家的連結。
- 商品詳情顯示圖、名稱、價格、庫存、分類名稱、賣家顯示名稱、關鍵字。
- 這個任務的加入購物車按鈕可以先渲染。未登入點擊的行為在 T15 完成。T08 不要寫一個會改庫存的 POST。

測試：

測試名稱以對應的 `FR-` 開頭，多個編號就都寫進名稱。

- FR-10：種子商品出現名稱、價格、庫存、賣家 `Demo Seller`。
- FR-11：以固定的四筆資料查詢兩次，名稱集合相同。不要斷言隨機順序。
- FR-12：兩種價格排序。另插兩筆同價商品，斷言名稱升冪。
- FR-13：關鍵字 `WATCH` 找得到手錶。前後空白不影響。
- FR-14：沒有命中時狀態 200，且有空狀態資源字串。繁中與英文各一次。
- FR-15：分類頁含 `Computer`。點進該分類後只有筆電與街機。
- FR-16：不存在的分類 id 是 200 與空目錄。
- FR-17：街機顯示售完，加入控制項是停用。
- FR-18：庫存 20 的商品有 10 個數量選項。把一筆測試商品庫存改成 3，選項是 1、2、3。測完還原或使用自己建立的商品。
- FR-22：詳情頁含關鍵字與分類。
- FR-23：不存在的 id 是 404。
- FR-24：圖片標籤有商品名稱 alt。
- FR-100：`/about` 與 `/blog` 在兩語系都有標題與至少一段內文。

完成標準：

- `bash tests/check-coverage.sh` 通過。
- 服務層沒有參考 `HttpContext`。

### T09 — 第一批 E2E 與資料、語系 ADR

要做的事：

- E2E 專案加入 `Microsoft.Playwright.NUnit`。集合 fixture 啟動測試宿主：HTTPS、測試資料庫、Azurite、`Testing:ExposeFault` 先不要開。Playwright 只裝 Chromium，`IgnoreHTTPSErrors` 為 true。
- 測試名稱含 FR 編號。這個任務要覆蓋：FR-01、FR-02 的匿名狀態、FR-06、FR-07、FR-08、FR-10 至 FR-18、FR-22 至 FR-24、FR-100。FR-02 的已登入狀態留到 T14。FR-109 這個任務只跑已經存在的路由：`/`、`/categories`、`/products/{存在的 id}`、不存在的商品、`/about`、`/blog`。寬度 375 與 1440，語系兩種。斷言沒有水平捲軸、主要標題可見。768 與 1024 至少各開一次 `/`。
- 更新 `build-mvc.yml`：安裝 Chromium、跑 E2E、失敗時上傳 `playwright-report` 或 trace。覆蓋率腳本不要把 E2E 專案算進百分比。
- 新增 `docs/adr/0006-shop-tables-beside-violet.md`：新表與 `violet_*` 並存，啟動時由 App 套用 SQL，GitHub runner 不連 SQL。
- 新增 `docs/adr/0007-zh-hant-and-en.md`：預設繁中，可切英文，使用者輸入不翻譯。

完成標準：

- `dotnet test tests/Shopping.E2E/Shopping.E2E.csproj --configuration Release` 通過。
- CI 檔含 Playwright，且沒有改 `deploy.yml`。

### T10 — 註冊

要做的事：

- `GET/POST /account/register`。已登入者導向 `/`。
- 欄位：顯示名稱、Email、使用者名稱、密碼、確認密碼、電話、生日、性別、國家、州／省、城市、地址。全部必填。性別的值是 `Male`、`Female`、`Others`，標籤走資源檔。
- 驗證規則用第 3 節。生日不可晚於 `TimeProvider` 給出的 UTC 日期。不要設最低年齡。
- 重複的 Email、使用者名稱、電話各有自己的資源鍵。
- 成功後使用者恰好有 `Customer`。沒有 `Seller`，沒有 `Operator`。成功後導向登入頁，不自動登入。
- 輸入模型只用明確屬性。不要用 `ApplicationUser` 當表單模型，避免多綁密碼雜湊或角色。

測試：

- FR-50：空白送出時每個欄位旁都有錯誤，且有 `role="alert"`。
- FR-51：密碼 `password` 被拒絕。
- FR-52：確認密碼不同被拒絕。
- FR-53：電話 `123` 被拒絕。
- FR-54：明天的生日被拒絕。用固定的 `TimeProvider`。
- FR-55：顯示名稱 `王小明` 可以註冊並登入。
- FR-56：重複 Email、重複使用者名稱、重複電話、`not-an-email`、以及 `person@example.museum` 可以註冊。各自分開斷言。
- FR-57：新使用者的角色只有 `Customer`。登入後的 HTML 不含上架連結。此時上架頁可以尚未存在，斷言版面沒有「上架商品」文字即可。
- FR-58：已登入時 `GET /account/register` 被導離該頁。

完成標準：

- 資料庫沒有安全問題欄位。
- 覆蓋率腳本通過。

### T11 — 登入、登出、鎖定

要做的事：

- `GET/POST /account/login`。已登入者導向 `/`。
- 帳號欄接受使用者名稱或 Email。錯誤密碼時留在登入頁，使用同一句失敗文案，不要透露帳號是否存在。
- 啟用鎖定：5 次失敗鎖 15 分鐘。測試宿主把鎖定時間設成 2 秒，正式設定維持 15 分鐘。這兩個數字都放在設定裡，不要寫死在如果是測試就特殊判斷的散落程式。正式設定檔寫 15 分鐘。測試宿主覆寫為 2 秒。
- `POST /account/logout` 需要防偽造權杖。登出後 Cookie 清除。不要在登出時刪除購物車。購物車表還沒有寫入路徑時，先測登出不會呼叫任何刪除購物車的方法；T15 再補「購物車列還在」。
- 未登入開啟未來的會員頁時要帶回來源。這個任務先讓登入頁讀取 `returnUrl`，只允許站內相對路徑。`//evil.example` 與絕對網址都忽略，改導向 `/`。

測試：

- FR-60：使用者名稱成功、Email 成功、錯誤密碼失敗。
- FR-61：第 5 次失敗後，正確密碼在鎖定期間失敗。測試宿主等待超過 2 秒後，正確密碼成功。
- FR-62：登出是 POST。GET `/account/logout` 不是成功登出。登出後 `/` 顯示登入連結。

完成標準：

- 身分 Cookie 在 HTTPS 測試裡是 `HttpOnly`、`Secure`、`SameSite=Lax`。這條也供 T24 的 FR-107 使用，T11 就先斷言，T24 保留同一個測試名稱。

### T12 — 忘記密碼與重設

要做的事：

- `GET/POST /account/forgot` 與 `GET/POST /account/reset`。已登入者導向 `/`。
- 畫面文案不論帳號是否存在都使用同一個資源鍵。
- 帳號存在時才寄信。信裡是站內重置連結，含權杖。資料庫不存原始權杖。
- `PasswordResetService` 使用 `shop_password_reset_quota`。同一使用者從 `WindowStart` 起 1 小時內最多成功寄出 5 封。第 6 次不寄信，畫面仍是同一句話。超過 1 小時後視窗重設。不存在的帳號不寫配額。
- 寄信成功才把次數加 1。寄信拋出時更新 `SecurityStamp`，回傳「暫時無法寄信」，次數不加。
- 再次申請使上一封連結失效。做法是在發出新權杖前更新 `SecurityStamp`，舊權杖因此失效。寄信失敗時也更新，所以失敗那次不會留下可用權杖。若成功路徑在寄出前更新 stamp，再產生權杖，順序要讓新信裡的權杖仍然有效。測試把這個順序鎖住。
- 重設頁驗證新密碼規則與確認密碼。過期、已使用、空權杖都不改密碼。
- Web 測試與 E2E 使用記憶體 `Mailbox`。`SmtpEmailSender` 仍要存在，使用 MailKit，正式環境才註冊它。

測試：

- FR-63：存在與不存在的帳號，繁中與英文的可見文案相同。
- FR-64：存在的帳號使 Mailbox 有一封信。不存在則 Mailbox 是空的。
- FR-65：第 5 次 Mailbox 有 5 封。第 6 次仍是 5 封。
- FR-66：寄信器丟例外時，回應含暫時無法寄信。測試用的寄信器若看到權杖，該權杖不能用來重設。
- FR-67：有效權杖可設新密碼。舊密碼不能登入，新密碼可以。
- FR-68：過期權杖、用過的權杖、空權杖都不改密碼。過期測試把權杖壽命在測試宿主設成 1 秒，或用可控制的時鐘。不要在正式設定使用 1 秒。
- FR-69：第二封信發出後，第一封的權杖失敗。
- `SmtpEmailSender`：本機測試 SMTP 成功、認證失敗、連線埠沒人聽。三個分支都在 `Shopping.Web.Tests`，不放到 E2E。

完成標準：

- 日誌斷言不包含完整 Email。測試一個失敗寄信，檢查記錄的訊息。
- 畫面沒有安全問題欄位。

### T13 — 個人資料

要做的事：

- `GET/POST /account`。未登入導向登入並帶回 `/account`。
- 唯讀顯示：顯示名稱、Email、使用者名稱、生日、性別。
- 可改：電話、地址、國家、州／省、城市。POST 模型只有這五個欄位。多送顯示名稱或 Email 時，資料庫那些欄位不變。
- 電話規則與註冊相同。重複電話拒絕。
- 沒有訂單時顯示訂單空狀態。訂單列表的資料在 T16 接上，這個任務先讓空狀態是真的去查 `shop_order`。

測試：

- FR-70：五個唯讀欄位出現，且沒有對應的可編輯輸入。
- FR-71：合法更新會保留。電話 `12345` 拒絕。改成另一個使用者已有的電話時拒絕。
- FR-72：新註冊使用者看到訂單空狀態。

### T14 — 帳戶 E2E

要做的事：

依計畫第 16.4 節的劇本 4、5、6、7，以及 FR-02 的已登入狀態、FR-58、FR-62。E2E 測試名稱含 FR 編號。

- 註冊驗證走瀏覽器：空白、密碼不符、電話錯誤、未來生日、重複 Email。英文再跑一次空白表單，斷言英文必填句。
- 註冊 `王小明` 類似的唯一名字，登入，確認看不到上架連結。
- 錯誤密碼與鎖定。鎖定使用測試宿主的 2 秒設定。
- 忘記密碼的兩種帳號文案相同。從 Mailbox 取出連結完成重設。舊連結失效。
- 個人資料改地址成功。改成 `demo_customer` 的電話時失敗。
- 登出後是匿名版面。

完成標準：

- 上述劇本在 Chromium 通過。
- 沒有用 `Skip` 忽略鎖定測試。

### T15 — 購物車

要做的事：

- `CartService`：加入、改數量、移除、讀取、總額。總額用商品現價乘數量，不信任表單上的金額。
- `shop_cart_line` 不存單價。
- 加入時若已有該列，數量相加。結果必須同時小於等於 10 且小於等於目前庫存，否則整次拒絕，原數量不變。
- 任何購物車操作之後，`shop_product.Stock` 與操作前相同。
- 路由：`POST /cart` 加入，`POST /cart/{productId}` 更新或移除，`GET /cart` 顯示。未登入都導向登入並帶回來源。
- 空購物車顯示空狀態與回目錄的連結，沒有結帳按鈕。
- 版面徽章是該會員購物車數量加總。未登入不顯示徽章。
- 目錄與商品詳情的加入表單改成真正 POST。售完按鈕維持停用，而且伺服器再擋一次庫存 0 的加入。

測試：

- FR-19：未登入 POST 加入，導向登入，庫存不變，購物車表沒有新列。
- FR-20：登入後加入，購物車有該列，庫存不變。
- FR-21：8 再加 3，若上限 10 則拒絕且仍是 8。庫存 2 時把數量改成 3 被拒絕。
- FR-04：徽章等於數量加總。這個任務的整合測試先斷言 HTML。登出後徽章消失、資料庫列還在，放在同一個測試。
- FR-30：未登入開 `/cart` 與 `/checkout`。`/checkout` 頁面可以尚未做完，但未登入必須已經被授權擋下。若 T15 還沒有結帳檢視，仍要先放一個需要授權的 `GET /checkout`。
- FR-31：空購物車。
- FR-32：賣家把價格改掉之後，購物車小計用新價格。這個案例在 T20 之前，測試直接改資料庫價格。
- FR-33：移除一列後總額與徽章更新。

完成標準：

- 搜尋新站 C#，加入購物車的路徑沒有更新 `Stock`。

### T16 — 結帳與訂單

要做的事：

- `CheckoutService` 用單一資料庫交易。對涉及的商品列使用更新鎖。每一列都要 `Stock >= 購買量`，然後扣庫存、寫訂單與明細、刪除該會員全部購物車列。任一步失敗就整筆回滾。
- 明細寫入當下的 `ProductName`、`UnitPrice`，`LineTotal` 由服務計算。忽略表單送來的價格。
- 訂單編號由 `IOrderNumberFactory` 產生，格式 `V` 加 ULID。插入若撞唯一鍵，換一個編號再試一次。第二次仍失敗就回滾，購物車保留，對使用者顯示暫時無法建立訂單。
- `GET /checkout`：空購物車導回 `/cart`。有資料時列出品項、總額、以台北時區顯示的日期，以及沒有線上付款的句子。
- `POST /checkout`：成功後導向一個完成頁或在結帳頁顯示訂單編號。購物車是空的。訂單歷史看得到這筆。
- 庫存不足時整筆失敗，畫面指出商品名稱，購物車與庫存都維持失敗前的值。
- 並行測試用兩個獨立的 DbContext 同時結帳同一件庫存 1 的商品。恰好一筆成功。

測試：

- FR-34：結帳頁含總額、日期與沒有付款的句子。繁中與英文各一次。
- FR-35：空購物車 POST 不產生訂單。
- FR-36：成功後庫存減少購買量，購物車空，畫面有編號，`/account` 看得到。
- FR-37：下單後改商品價格，訂單明細仍是舊單價。
- FR-38：結帳前把庫存改成 0，結帳失敗，購物車還在，庫存仍是 0，畫面有商品名稱。
- FR-39：並行恰好一筆成功。測試名稱含 FR-39。不要寫 E2E。
- 編號產生器：第一次碰撞後第二次成功。連續兩次碰撞則失敗且購物車還在。ULID 格式測試：長度 27、以 `V` 開頭。
- FR-42 的刪除商品部分若刪除功能尚未做好，T20 必須補上「刪除後訂單品名還在」。T16 先用直接刪除資料列或服務方法覆蓋這個不變量。若 T16 還沒有刪除服務，就在測試裡移除商品列並斷言明細品名仍在，測試名稱含 FR-42。T20 保留這個編號。

完成標準：

- 沒有用 EF InMemory。
- 覆蓋率腳本通過。

### T17 — 購物 E2E

要做的事：

瀏覽器劇本：

- 匿名看目錄、兩種排序、搜尋命中、搜尋落空、打開分類、打開商品、售完不能按、未登入加入會去登入。這些若 T09 已覆蓋，這個任務補上「加入」。
- 註冊新會員、登入、加入、再加入造成合併、改數量、移除、再加入、結帳前再留一項在購物車、結帳成功、訂單歷史、登出、再登入後那項仍在。PDF 留到 T18 與 T22。
- 另一條：把庫存改到不夠時結帳失敗，購物車還在。E2E 可以呼叫測試專用的方式改庫存。不要在正式環境開一個改庫存的匿名端點。做法是用賣家帳號登入後的維護功能；若 T20 還沒做，這條 E2E 就排到 T22，但 T17 的整合測試已經覆蓋 FR-38。T17 的 E2E 至少走成功結帳。

完成標準：

- 成功結帳的 E2E 通過，而且商店庫存有減少。測試要恢復種子或使用自己建立的商品，避免下一條測試被庫存 0 影響。

### T18 — 訂單 PDF

要做的事：

- `InvoiceService` 只讀取目前使用者的訂單。別人的編號回傳找不到。
- `GET /account/orders/{number}/invoice`。回應是附件，檔名 `OrderInvoice.pdf`。
- 使用 QuestPDF 畫一頁 A4。內容含訂單編號、台北時區的日期、品名、單價、數量、小計、總額。
- 新增 `docs/adr/0008-questpdf-community-license.md`。寫明 Community License 3.0 不是 MIT，本儲存庫以 MIT 開源專案資格使用。若日後閉源或資格不符，要改畫法或購買授權。不要寫 QuestPDF 是 MIT。
- 文字抽取套件只加在測試專案。

測試：

- FR-40：位元組以 `%PDF` 開頭，抽出的文字含編號、品名、數量、總額。E2E 也下載一次並檢查檔頭。文字檢查可以留在整合測試，E2E 至少檢查檔頭與狀態 200。
- FR-41：另一個會員請求同一編號得到 404。E2E 也做一次。

完成標準：

- Web 與 Data 的 csproj 沒有 iTextSharp。
- ADR 已提交。

### T19 — 圖片與上架

要做的事：

- `ImageStore` 放在 `Shopping.Data`。允許的副檔名是 `.jpg`、`.jpeg`、`.png`、`.gif`、`.webp`，不分大小寫。上限 2 MB，常數與舊站相同，恰好 2 MB 可以，多 1 byte 不行。
- magic bytes：JPEG 是 `FF D8 FF`。PNG 是 `89 50 4E 47 0D 0A 1A 0A`。GIF 是 `GIF87a` 或 `GIF89a`。WebP 是開頭 `RIFF` 且位移 8 為 `WEBP`。空串流、短於檔頭、副檔名與檔頭不符、不支援的副檔名都拒絕。
- 上傳路徑是 `{使用者名稱}/{GUID}{副檔名}`。回傳的網址是 `IMAGE_BASE_URL` 去掉結尾斜線後，接 `/products/` 與該路徑。不要採用原始檔名。
- 正式環境用 `AZURE_CLIENT_ID` 的受控識別連 `STORAGE_BLOB_ENDPOINT`。測試用 Azurite。
- `GET/POST /seller/products/new`。未登入導向登入。已登入但沒有 `Seller` 是 403，不是導向登入。
- 驗證：必須有檔、副檔名、大小、magic bytes、名稱 1 到 50 且唯一、關鍵字最多 500、價格大於等於 0 且最多兩位小數、分類必須存在。失敗時不上傳，也不寫商品。
- 成功後庫存是 0，導向 `/account`。帳戶頁這個任務就要列出該賣家的商品，即使編輯還在 T20。

測試：

- FR-83：五種副檔名大小寫都接受。不支援的 `.bmp` 拒絕。每種格式的合法檔頭與錯檔頭都是獨立斷言，不要只靠一個迴圈讓人看不出缺了哪種。
- 短檔、空串流、恰 2 MB、2 MB 加 1 byte。
- FR-80：會員是 403，匿名被導向登入。E2E 兩種都做。
- FR-81：上架成功後庫存 0，帳戶頁看得到，目錄顯示售完。
- FR-82：未選檔、副檔名不允許、超過大小、magic bytes 不符、名稱 51 字、關鍵字 501 字、名稱重複、價格 `-1`、價格 `1.234`。每種失敗後 Azurite 容器的 blob 數量不增加。
- FR-84：路徑含使用者名稱與 GUID，資料庫存完整網址。

完成標準：

- 失敗案例沒有多的 blob。測試前後列出容器物件名稱。

### T20 — 修改與刪除自己的商品

要做的事：

- 帳戶頁列出目前賣家的商品，可改價格、庫存、關鍵字。
- `POST /seller/products/{id}` 帶動作 `update` 或 `delete`。
- 更新：價格與庫存大於等於 0，關鍵字最多 500。非法值不寫入。
- 庫存改成大於 0 後，目錄可以購買。
- 刪除表單含核取方塊。未勾選不刪除。勾選後刪除商品，其他會員購物車裡的該列一併消失。訂單品名還在。
- 不是貨主，或 id 不存在，都回 404，不寫「這不是你的商品」。
- 版面在賣家登入時顯示上架連結。一般會員不顯示。

測試：

- FR-03：三種角色的版面。管理者連結仍可沒有，但賣家連結要有。T21 補管理者連結時保留 FR-03 這個名稱。
- FR-85：改庫存為 2 後，目錄的加入按鈕可用。
- FR-86：價格小於 0、庫存小於 0、關鍵字 501 字，三個都不寫入。
- FR-87：未勾選不刪。勾選後商品不見，另一個會員的購物車列不見。
- FR-88：賣家 A 對賣家 B 的 id 得到 404，商品不變。
- FR-42：刪除後訂單仍顯示原品名。

### T21 — 管理者授予賣家

要做的事：

- `GET/POST /operator/sellers`。只有 `Operator` 可以。會員或賣家是 403。未登入導向登入。
- 以使用者名稱或 Email 搜尋。找不到就顯示空結果，不要建立使用者。
- 可以授予或收回 `Seller`。不能授予 `Operator`，不能收回 `Operator`，也不能收回自己的 `Operator`。表單不要提供這些動作。若有人直接 POST 角色名稱 `Operator`，拒絕且角色不變。
- 收回 `Seller` 後，該使用者不能上架，也不能更新。既有商品仍在目錄，仍可被購買。
- 版面只有 `Operator` 看得到「賣家資格」。
- 新增 `docs/adr/0009-seller-granted-by-operator.md`。寫明與 `CONTEXT.md` 一致：不能自行註冊成賣家。這個任務不要改 `CONTEXT.md` 的全文，T29 才把文件與已上線行為一起更新。ADR 可以先寫決定。

測試：

- FR-89：用使用者名稱找得到，用 Email 找得到，未知帳號是空結果。
- FR-90：授予後該會員可以開啟上架頁。收回後上架頁 403。收回前建立的商品仍在目錄。
- FR-91：會員與賣家開這個頁面都是 403。
- FR-92：POST 收回自己的 `Operator` 失敗。POST 授予別人 `Operator` 失敗。沒有 E2E。

### T22 — 賣家與管理者 E2E

要做的事：

- 賣家上架一張真正的小 png。目錄顯示售完。把庫存改成 2。會員買走 1。賣家刪除時必須勾選確認。
- 會員開上架頁是 403。賣家對別人的商品 id POST 是 404。
- 管理者授予一個新會員，該會員看得到上架並完成一次上架。收回後上架頁是 403，商品仍在目錄。
- 補上 T18 的 PDF 下載 E2E，若 T18 還沒在瀏覽器下載。
- 補 FR-03 的三種角色瀏覽器斷言。

完成標準：

- 這些測試不依賴執行順序。每個測試自己準備使用者。

### T23 — 聯絡我們

要做的事：

- `GET/POST /contact`。匿名可以送。
- 欄位：姓名最長 50、Email 用第 3 節的規則且最長 50、訊息必填且最長 500。
- 成功後顯示已送出，並寫入 `shop_contact_message`。不要在畫面上列出別人的留言。

測試：

- FR-101：三欄空白、Email 不合法、訊息 501 字。
- FR-102：匿名成功後資料庫多一筆，畫面有成功文案。

### T24 — 錯誤頁、安全標頭、啟動檢查

要做的事：

- `GET /error/404` 與 `GET /error/500` 是一般文案，不讀資料庫，不含例外型別、堆疊、SQL 或連線字串。
- 未知路由重新執行到 404 頁，狀態碼維持 404。
- `Testing:ExposeFault` 為 true 時才有 `GET /test/fault`，它丟出一個例外。正式環境的測試宿主不設定這個鍵，請求該路徑得到 404。
- 例外處理器把未處理例外轉成 500 頁。
- 每個一般回應加上：`X-Frame-Options: DENY`、`X-Content-Type-Options: nosniff`、`Referrer-Policy: strict-origin-when-cross-origin`、`Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=()`。
- CSP 為：`default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data: https:; font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'`。不要加入 `unsafe-inline` 或 `unsafe-eval`。
- 移除 `X-Powered-By`。Kestrel 不要送 `Server` 標頭。
- HSTS 只在正式環境。開發與測試宿主的回應沒有 HSTS。
- 所有 POST 維持防偽造權杖。缺權杖或權杖錯誤時是 400，不執行業務。
- 正式環境缺少任一變數就啟動失敗。變數是：`SQL_SERVER`、`SQL_DATABASE`、`SQL_USER`、`SQL_PASSWORD`、`SQL_ENCRYPT`、`SQL_MIGRATOR_USER`、`SQL_MIGRATOR_PASSWORD`、`SMTP_HOST`、`SMTP_PORT`、`SMTP_USER`、`SMTP_PASSWORD`、`SMTP_FROM`、`STORAGE_BLOB_ENDPOINT`、`STORAGE_CONTAINER`、`IMAGE_BASE_URL`、`AZURE_CLIENT_ID`。測試逐一拿掉一個，訊息含該名稱。開發環境允許 `appsettings.Development.json` 提供本機非秘密預設，不把這 16 個檢查套到開發環境而讓本機無法啟動。開發環境仍不放真實密碼。

測試：

- FR-103：未知網址是 404 頁，HTML 不含 `SqlException` 或 `at Shopping`。
- FR-104：測試宿主的 `/test/fault` 是 500 頁，HTML 不含例外型別名稱。正式設定的宿主沒有這個路徑。
- FR-105：代表頁的標頭符合上面的值，CSP 不含 `unsafe-inline` 與 `unsafe-eval`。
- FR-106：正式宿主有 HSTS，測試宿主沒有。
- FR-107：登入的 `Set-Cookie` 含 `httponly`、`secure`、`samesite=lax`。大小寫以實際標頭為準，斷言時不區分大小寫。
- FR-108：沒有權杖的 POST `/cart` 是 400，購物車數量與庫存不變。

完成標準：

- 用瀏覽器查看目錄的開發者工具不是這個任務的門檻。自動化測試要檢查標頭。
- 覆蓋率腳本通過。

### T25 — 追溯與剩餘矩陣

要做的事：

- 在 `Shopping.Data` 或 `Shopping.Web` 放純函式 `RequirementTrace`。輸入是計畫 Markdown、單元測試原始碼字串、E2E 原始碼字串。輸出缺少的單元編號與缺少的 E2E 編號。
- 函式從計畫抽出 `FR-` 編號。第 6 節列出的「要 E2E」編號必須出現在 E2E 原始碼。全部 FR 都必須出現在單元或整合測試原始碼。`RequirementTraceTests` 這個類別名稱與它的測試方法名稱不要含 `FR-`。
- 純函式的失敗分支用假字串測試：計畫文字打不開由呼叫端轉成空字串或錯誤旗標、編號缺少、E2E 缺少。檔案讀取失敗是另一個分支，測試傳入不存在的路徑給薄包裝。那個包裝的兩條路徑都要測到。
- 補齊第 6 節還沒有 E2E 的項目。FR-109 對第 6 節的路由表全部組合跑 375 與 1440、兩語系。768 與 1024 至少開 `/` 與 `/checkout`。
- 計畫第 16.4 節的 14 個劇本都有對應測試。劇本 2 的 PDF 在這裡必須已經包含。

測試：

- 假計畫只有 `FR-01`，單元原始碼沒有它，結果列為缺少。
- 假計畫的 `FR-01` 在第 6 節的對應規則用一份測試用清單注入，不要讓純函式去猜中文欄位。正式呼叫時傳入本文件第 6 節的編號集合，集合以程式裡的靜態清單維護，並由一個測試比對本文件第 6 節的程式碼區塊，避免清單漂移。做法：第 6 節使用一個 fenced code block，語言標記為 `text`，每行一個編號。測試讀取 `Refactory-Tasks.md` 那個區塊。

完成標準：

- 第 6 節每個要 E2E 的編號都出現在 `tests/Shopping.E2E`。
- 計畫第 9 節每個 FR 都出現在 `tests/Shopping.Web.Tests`。

### T26 — 覆蓋率收斂

要做的事：

- 跑 `bash tests/check-coverage.sh`。若失敗，補測試或刪掉達不到的死分支。不要用排除屬性、不要把程式碼包進正式環境永遠不編譯的條件。
- 兩個套件分開都是 `1`。
- 再跑全部 E2E。

完成標準：

- 腳本結束碼 0。
- 全方案測試結束碼 0。
- `git grep ExcludeFromCodeCoverage` 與 `git grep ExcludeByFile` 在新站沒有結果。

### T27 — 上線前核對

要做的事：

這是核對任務，不改產品行為。PR 只在發現偏差時修正。

- 對照計畫第 4.2 節，確認新站沒有那些功能。
- 對照計畫第 4.3 節的 C1 到 C15，每項都能指出測試名稱。
- 確認 `deploy.yml` 仍部署舊站，而且沒有人把舊 Web App 改成會停用。
- 確認新站啟動不會更新 `violet_*`。用測試資料庫裡預先放入的 `violet_products` 一列，跑完種子與一輪購物測試後，該列仍在。
- 列出新網址冒煙要打的路徑，寫進 PR 說明，不要寫進 README：`/` 看得到 `Demo Laptop`、`/account/login`、`/categories`、一張示範圖、安全標頭、未知路徑 404。舊網址要同時仍是舊站。

完成標準：

- PR 說明含上述核對結果。沒有偏差時，PR 可以只含這段說明與一個不改行為的註記。若儲存庫不接受空 PR，就在 `Refactory-Tasks.md` 的本節加一行「T27 已於該 PR 核對」，並寫上日期。

T27 已於 2026-10-04 核對：新站沒有第 4.2 節排除的付款、訂單狀態、訪客購物車與完整後台；C1 到 C15 分別由購物車結帳、角色、賣家授予、訂單快照、ULID、忘記密碼、版面、刪除確認、中文姓名、Email、CSP、鎖定、商品網址與分類拼字的測試覆蓋；deploy.yml 仍用 site.zip 部署舊站，且查詢排除 app-shopping-mvc-，不會停用舊 Web App；新站內嵌 SQL 沒有 violet_。

### T28 — 第二個 Web App，舊站保持運行

要做的事：

- 讀 `infra/modules/appservice.bicep`、`infra/modules/frontdoor.bicep` 與 `docs/adr/0004-pre-provisioned-managed-identities.md`。Front Door 的 origin 是 Blob，不是網站。不要改那個 origin。
- 在同一個 `rg-shopping` 新增 Linux App Service 方案，以及 Web App `app-shopping-mvc-<suffix>`，執行 `net10.0`。方案規格先用 B1。不要刪除、不要改寫現有 Windows App `app-shopping-<suffix>` 與其方案 `asp-shopping`。不要新建資源群組，不要新建資料庫。
- 新 App 掛既有的 `id-shopping-web`。不在 Bicep 裡指派角色。
- 應用程式設定沿用現有的 `SQL_*`、`SQL_MIGRATOR_*`、`SMTP_*`、`STORAGE_*`、`IMAGE_BASE_URL`、`AZURE_CLIENT_ID`，指向同一個 SQL、同一個 Blob。秘密繼續用 Key Vault 參考。
- 新增 `.github/workflows/deploy-mvc.yml`。它只對新 Web App 做 `dotnet publish src/Shopping.Web -c Release`，並對新網址冒煙。不要從 `deploy.yml` 移除 `site.zip`。
- 舊站的可用性測試維持打舊主機名稱。新站另加一組，不取代舊的。
- 對新網址執行 T27 列出的冒煙，同時打開舊網址確認它仍是舊站。新站冒煙失敗就停止，不要進 T29。

不要做的事：

- 不要停止或刪除 Windows App。
- 不要把 Front Door 改成轉送網站。
- 不要執行 `db/cleanup/remove_shop_demo_data.sql`。
- 不要執行舊的 `remove_demo_data.sql`。

完成標準：

- 舊網址仍顯示舊站，而且該 Web App 是運行中。
- 新網址顯示新站目錄，看得到 `Demo Laptop`。
- 兩個網址可以同時開啟。在新站下的訂單不會出現在舊站。
- `violet_*` 沒有被新 migration 修改。上線後用只讀查詢確認舊分類 `Computer Accesories` 那列還在。

### T29 — 兩個網址寫進文件

要做的事：

- 只有 T28 的兩個網址都檢查過才做。
- 更新 README、QuickStart、`CONTEXT.md`。並列舊網址與新網址，並寫明兩者在同一個資源群組、同一個資料庫、資料表分開。賣家的定義改為由管理者授予，角色來源不再是 `uid`。這段角色說明標明它描述的是新站。
- QuickStart 要能在 macOS 用 `dotnet run` 啟動新站，並保留舊站只在 Windows 建置的說明。舊站刪除不在本任務。
- 新站冒煙維持打新網址：`/`、`/account/login`、`/categories`、示範圖片、安全標頭、未知路徑 404。舊站冒煙維持打舊網址，不要改成新路由。
- README 的套件表不要把 QuestPDF 寫成 MIT。指向 ADR 0008。
- PR 說明寫上回滾方式：新站有問題時只重新部署或停止新 Web App。不要把舊站停掉來完成回滾。

不要做的事：

- 不要停止舊 Web App。
- 不要移除舊站的 `site.zip` 部署。

完成標準：

- README 同時列出兩個網址。新網址的註冊頁沒有「註冊成賣家」。舊網址仍是舊站。
- 舊 Windows App 是運行狀態。
- 計畫第 20 節的完成定義逐項在 PR 說明打勾。

## 6. E2E 必要編號

下面區塊給 T25 的追溯測試讀取。每行一個編號。不在這個區塊裡的 FR 仍必須出現在 `tests/Shopping.Web.Tests`，但不要求出現在 E2E。FR-39、FR-66、FR-83、FR-92、FR-106 故意不在這裡。

```text
FR-01
FR-02
FR-03
FR-04
FR-05
FR-06
FR-07
FR-08
FR-10
FR-11
FR-12
FR-13
FR-14
FR-15
FR-16
FR-17
FR-18
FR-19
FR-20
FR-21
FR-22
FR-23
FR-24
FR-30
FR-31
FR-32
FR-33
FR-34
FR-35
FR-36
FR-37
FR-38
FR-40
FR-41
FR-42
FR-50
FR-51
FR-52
FR-53
FR-54
FR-55
FR-56
FR-57
FR-58
FR-60
FR-61
FR-62
FR-63
FR-64
FR-65
FR-67
FR-68
FR-69
FR-70
FR-71
FR-72
FR-80
FR-81
FR-82
FR-84
FR-85
FR-86
FR-87
FR-88
FR-89
FR-90
FR-91
FR-100
FR-101
FR-102
FR-103
FR-104
FR-105
FR-107
FR-108
FR-109
```

## 7. 測試指令

單元、整合與覆蓋率：

```bash
docker compose -f docker-compose.tests.yml up -d
bash tests/check-coverage.sh
```

`tests/check-coverage.sh` 內部執行 Release 組態的 `dotnet test Shopping.sln`，但排除 E2E 專案的覆蓋率收集，再檢查兩個產品套件。T02 建立這個腳本時就把排除 E2E 的方式寫死，避免 T09 之後百分比被瀏覽器測試稀釋或根本量不到。

E2E：

```bash
dotnet test tests/Shopping.E2E/Shopping.E2E.csproj --configuration Release
```

Playwright 瀏覽器只裝 Chromium。CI 與本機使用同一條安裝指令，寫在 E2E csproj 的說明註解與 `build-mvc.yml`。

## 8. 做完一個任務時的檢查

- 這個任務的測試名稱含它表上的 FR 編號。
- `bash tests/check-coverage.sh` 結束碼是 0。若該任務已經引入 E2E，E2E 專案也是 0。
- 沒有修改任務範圍以外的產品行為。
- 沒有把真實連線字串、SMTP 密碼或 Azure 金鑰加入檔案。
- PR 說明寫了驗證指令與還沒做的下一個任務編號。
