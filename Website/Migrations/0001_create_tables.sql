-- 0001：建立網站使用的資料表（Plan.md §10.2）。
-- 欄位順序必須與程式中的位置式 INSERT 一致：
--   violet_user_login（register.aspx.cs，14 欄）、violet_cart（cart.aspx.cs，8 欄）、
--   violet_order（checkout.aspx.cs，6 欄）、violet_contact（contact.aspx.cs，3 欄）。
-- 每張表只在不存在時建立，因此可套用到依 QuickStart 舊版手動建立的資料庫。
-- orderID、orderDate 保留原始大小寫，與 profile.aspx 等處的欄位名稱一致。
-- 刻意不建立外鍵：賣家刪除商品後，購物車與訂單仍保留商品名稱。

-- 會員與賣家帳號（uid 1～4998：一般會員；> 5000：賣家）
IF OBJECT_ID(N'dbo.violet_user_login', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.violet_user_login (
            uname VARCHAR(50) NOT NULL PRIMARY KEY,
            email VARCHAR(50) NOT NULL UNIQUE,
            username VARCHAR(20) NOT NULL UNIQUE,
            password VARCHAR(20) NOT NULL,  -- noqa: RF04
            phone DECIMAL(10, 0) NOT NULL UNIQUE,
            dob DATE NOT NULL,
            country VARCHAR(20) NOT NULL,
            state VARCHAR(20) NOT NULL,
            city VARCHAR(20) NOT NULL,
            gender VARCHAR(10) NOT NULL,
            address VARCHAR(500) NOT NULL,
            secq VARCHAR(100) NOT NULL,
            seca VARCHAR(20) NOT NULL,
            uid INT NOT NULL DEFAULT 0
        );
    END;

-- 商品（uname 為賣家姓名；pimage 為圖片網址或舊資料的相對路徑）
IF OBJECT_ID(N'dbo.violet_products', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.violet_products (
            pname VARCHAR(50) NOT NULL PRIMARY KEY,
            price DECIMAL(20, 2) NOT NULL,
            pimage VARCHAR(250) NOT NULL,
            category VARCHAR(50) NULL,
            uname VARCHAR(50) NULL,
            keywords VARCHAR(500) NULL,
            stock INT NOT NULL DEFAULT 0
        );
    END;

-- 購物車
IF OBJECT_ID(N'dbo.violet_cart', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.violet_cart (
            uname VARCHAR(50) NOT NULL,
            sno INT NOT NULL,
            pimage VARCHAR(250) NULL,
            pname VARCHAR(50) NOT NULL,
            price DECIMAL(20, 2) NOT NULL,
            quantity INT NOT NULL,
            total DECIMAL(20, 2) NOT NULL,
            sname VARCHAR(50) NULL
        );
    END;

-- 訂單（同一次結帳共用 orderID）
IF OBJECT_ID(N'dbo.violet_order', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.violet_order (
            uname VARCHAR(50) NOT NULL,
            pname VARCHAR(50) NOT NULL,
            orderID VARCHAR(30) NOT NULL,  -- noqa: CP02
            orderDate VARCHAR(20) NOT NULL,  -- noqa: CP02
            quantity INT NOT NULL,
            total DECIMAL(20, 2) NOT NULL
        );
    END;

-- 商品分類
IF OBJECT_ID(N'dbo.violet_categories', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.violet_categories (
            name VARCHAR(50) NOT NULL PRIMARY KEY,  -- noqa: RF04
            cimage VARCHAR(250) NULL
        );
    END;

-- 聯絡我們留言
IF OBJECT_ID(N'dbo.violet_contact', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.violet_contact (
            uname VARCHAR(50) NULL,
            email VARCHAR(50) NULL,
            message VARCHAR(500) NULL  -- noqa: RF04
        );
    END;
