-- =====================================================================
-- 原始資料庫腳本（保留作為歷史參考）
-- 注意：此腳本已與程式碼實際使用的結構不一致，請勿直接執行。
--   1. 第一行 CREATE DATABSE 為拼字錯誤（應為 CREATE DATABASE）。
--   2. violet_user_login 缺少 uid 欄位（register.aspx.cs 會寫入第 14 欄 uid）。
--   3. violet_products 的賣家欄位在程式中為 uname（非 sname），且缺少 keywords、stock。
--   4. violet_cart 程式實際使用 8 欄：uname, sno, pimage, pname, price, quantity, total, sname。
--   5. 缺少 violet_order 與 violet_categories 兩張資料表。
--   6. violet_seller_login 未被任何程式碼使用（賣家同樣存在 violet_user_login）。
--   7. 檔尾 DROP 敘述前單獨一行的單引號 ' 會讓後續敘述被視為未結束字串。
-- 與程式碼相符的完整結構請見 QuickStart.md「建立資料庫」章節。
-- =====================================================================

CREATE DATABSE website;
USE website;

-- violet_user_login（原腳本）：會員帳號表，程式實際也拿來存賣家；此定義缺少程式需要的 uid 欄位。
--   欄位 uname：姓名，原腳本設為主鍵；程式用它關聯購物車、訂單與商品賣家。
--   欄位 email：Email，唯一且不可為 NULL；可作為登入識別。
--   欄位 username：使用者名稱，唯一且不可為 NULL；可作為登入識別。
--   欄位 password：明碼密碼；程式直接比對與寄送，未做雜湊。
--   欄位 phone：10 位數電話，原腳本設唯一且不可為 NULL。
--   欄位 dob：生日。
--   欄位 country/state/city/gender/address：個人資料欄位。
--   欄位 secq/seca：忘記密碼安全問題與答案。
--   差異：register.aspx.cs 寫入第 14 欄 uid；profile.aspx.cs 以 uid > 5000 顯示賣家區塊。
CREATE TABLE violet_user_login (uname VARCHAR(50) PRIMARY KEY, email VARCHAR(50) NOT NULL UNIQUE, username VARCHAR(20) NOT NULL UNIQUE, password VARCHAR(20) NOT NULL, phone DECIMAL(10, 0) NOT NULL UNIQUE, dob DATE NOT NULL, country VARCHAR(20) NOT NULL, state VARCHAR(20) NOT NULL, city VARCHAR(20) NOT NULL, gender VARCHAR(10) NOT NULL, address VARCHAR(500) NOT NULL, secq VARCHAR(100) NOT NULL, seca VARCHAR(20) NOT NULL);

-- violet_seller_login（原腳本）：獨立賣家表；目前程式碼沒有任何頁面讀寫此表。
--   欄位 sname/email/username/password/phone/dob/country/state/city/gender/address/secq/seca：結構仿照 violet_user_login。
--   差異：sellerRegister.aspx.cs 實際寫入 violet_user_login；賣家身分由 violet_user_login.uid 判斷。
CREATE TABLE violet_seller_login (sname VARCHAR(50) PRIMARY KEY, email VARCHAR(50) NOT NULL UNIQUE, username VARCHAR(20) NOT NULL UNIQUE, password VARCHAR(20) NOT NULL, phone DECIMAL(10, 0) NOT NULL UNIQUE, dob DATE NOT NULL, country VARCHAR(20) NOT NULL, state VARCHAR(20) NOT NULL, city VARCHAR(20) NOT NULL, gender VARCHAR(10) NOT NULL, address VARCHAR(500) NOT NULL, secq VARCHAR(100) NOT NULL, seca VARCHAR(20) NOT NULL);

-- violet_products（原腳本）：商品表；程式實際需要更多欄位且賣家欄位名稱不同。
--   欄位 pname：商品名稱，主鍵；程式以商品名稱作為查詢、購物車與刪除識別。
--   欄位 price：商品價格。
--   欄位 pimage：商品圖片相對路徑，原腳本設唯一。
--   欄位 category：商品分類；原腳本 VARCHAR(15) 放不下程式寫死的 "Computer Accesories"。
--   欄位 sname：原腳本連到 violet_seller_login；程式實際使用 uname 且對應 violet_user_login.uname。
--   差異：程式還會讀寫 keywords 與 stock；addProducts.aspx.cs 的 INSERT 只提供 pname, price, pimage, category, uname, keywords 六值。
CREATE TABLE violet_products (pname VARCHAR(50) PRIMARY KEY, price DECIMAL(20,2) NOT NULL, pimage VARCHAR(250) NOT NULL UNIQUE, category VARCHAR(15), sname VARCHAR(50) REFERENCES violet_seller_login(sname));

-- violet_cart（原腳本）：購物車表；原腳本只有 3 欄，與 cart.aspx.cs 的位置式 INSERT 不相容。
--   欄位 uname：買家姓名，原腳本外鍵到 violet_user_login.uname。
--   欄位 pname：商品名稱，原腳本外鍵到 violet_products.pname。
--   欄位 quantity：購買數量。
--   差異：程式實際需要 uname, sno, pimage, pname, price, quantity, total, sname 八欄，且會刪除後重新編號 sno。
CREATE TABLE violet_cart (uname VARCHAR(50) REFERENCES violet_user_login(uname), pname VARCHAR(50) REFERENCES violet_products(pname), quantity INT NOT NULL);

-- violet_contact（原腳本）：聯絡我們留言表，與 contact.aspx.cs 的三欄位置式 INSERT 相符。
--   欄位 uname：留言者姓名。
--   欄位 email：留言者 Email。
--   欄位 message：留言內容。
CREATE TABLE violet_contact (uname VARCHAR(50), email VARCHAR(50), message VARCHAR(500));

SELECT * FROM violet_user_login;
SELECT * FROM violet_seller_login;
SELECT * FROM violet_products;
SELECT * FROM violet_cart;

'
drop table violet_cart;
drop table violet_products;
drop table violet_seller_login;
drop table violet_user_login;
'