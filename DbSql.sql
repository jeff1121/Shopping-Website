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

CREATE TABLE violet_user_login (uname VARCHAR(50) PRIMARY KEY, email VARCHAR(50) NOT NULL UNIQUE, username VARCHAR(20) NOT NULL UNIQUE, password VARCHAR(20) NOT NULL, phone DECIMAL(10, 0) NOT NULL UNIQUE, dob DATE NOT NULL, country VARCHAR(20) NOT NULL, state VARCHAR(20) NOT NULL, city VARCHAR(20) NOT NULL, gender VARCHAR(10) NOT NULL, address VARCHAR(500) NOT NULL, secq VARCHAR(100) NOT NULL, seca VARCHAR(20) NOT NULL);
CREATE TABLE violet_seller_login (sname VARCHAR(50) PRIMARY KEY, email VARCHAR(50) NOT NULL UNIQUE, username VARCHAR(20) NOT NULL UNIQUE, password VARCHAR(20) NOT NULL, phone DECIMAL(10, 0) NOT NULL UNIQUE, dob DATE NOT NULL, country VARCHAR(20) NOT NULL, state VARCHAR(20) NOT NULL, city VARCHAR(20) NOT NULL, gender VARCHAR(10) NOT NULL, address VARCHAR(500) NOT NULL, secq VARCHAR(100) NOT NULL, seca VARCHAR(20) NOT NULL);
CREATE TABLE violet_products (pname VARCHAR(50) PRIMARY KEY, price DECIMAL(20,2) NOT NULL, pimage VARCHAR(250) NOT NULL UNIQUE, category VARCHAR(15), sname VARCHAR(50) REFERENCES violet_seller_login(sname));
CREATE TABLE violet_cart (uname VARCHAR(50) REFERENCES violet_user_login(uname), pname VARCHAR(50) REFERENCES violet_products(pname), quantity INT NOT NULL);
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