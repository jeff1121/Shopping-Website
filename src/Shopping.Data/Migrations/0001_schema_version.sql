IF OBJECT_ID(N'dbo.shop_schema_version', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.shop_schema_version
    (
        ScriptName nvarchar(260) NOT NULL CONSTRAINT PK_shop_schema_version PRIMARY KEY,
        AppliedAt datetimeoffset NOT NULL
    );
END
