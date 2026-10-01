-- 0005：密碼雜湊與重設密碼權杖（Plan.md §12 項目 7-3、7-4）。
-- password 擴充為 200 字元以存放 PBKDF2 雜湊字串；既有明碼密碼在下次登入成功時
-- 由程式（UserAccounts.Authenticate）自動升級為雜湊，因此本腳本不改動既有資料。
-- violet_password_reset 只存權杖的 SHA-256 雜湊，原始權杖只出現在寄出的重設連結中。
IF COL_LENGTH(N'dbo.violet_user_login', N'password') < 200
    BEGIN
        ALTER TABLE dbo.violet_user_login
        ALTER COLUMN password VARCHAR(200) NOT NULL;  -- noqa: RF04
    END;

IF OBJECT_ID(N'dbo.violet_password_reset', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.violet_password_reset (
            token_hash CHAR(64) NOT NULL PRIMARY KEY,
            uname VARCHAR(50) NOT NULL,
            expires_at DATETIME2 NOT NULL,
            used_at DATETIME2 NULL
        );

        CREATE INDEX ix_violet_password_reset_uname
            ON dbo.violet_password_reset (uname);
    END;
