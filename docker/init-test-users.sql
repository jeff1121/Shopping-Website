-- 只用於本機與 CI 的測試資料庫。密碼不是 Azure 密碼。
IF DB_ID(N'ShoppingTests') IS NULL
    CREATE DATABASE ShoppingTests;
GO
IF SUSER_ID(N'shopping_migrator') IS NULL
    CREATE LOGIN shopping_migrator WITH PASSWORD = N'Test_only_9xQ';
GO
IF SUSER_ID(N'shopping_app') IS NULL
    CREATE LOGIN shopping_app WITH PASSWORD = N'Test_only_9xQ';
GO
USE ShoppingTests;
GO
IF DATABASE_PRINCIPAL_ID(N'shopping_migrator') IS NULL
    CREATE USER shopping_migrator FOR LOGIN shopping_migrator;
GO
ALTER ROLE db_owner ADD MEMBER shopping_migrator;
GO
IF DATABASE_PRINCIPAL_ID(N'shopping_app') IS NULL
    CREATE USER shopping_app FOR LOGIN shopping_app;
GO
ALTER ROLE db_datareader ADD MEMBER shopping_app;
GO
ALTER ROLE db_datawriter ADD MEMBER shopping_app;
GO
