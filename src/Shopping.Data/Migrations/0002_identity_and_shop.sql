IF OBJECT_ID(N'dbo.shop_user', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.shop_user
    (
        Id nvarchar(450) NOT NULL CONSTRAINT PK_shop_user PRIMARY KEY,
        UserName nvarchar(20) NULL,
        NormalizedUserName nvarchar(20) NULL,
        Email nvarchar(50) NULL,
        NormalizedEmail nvarchar(50) NULL,
        EmailConfirmed bit NOT NULL,
        PasswordHash nvarchar(max) NULL,
        SecurityStamp nvarchar(max) NULL,
        ConcurrencyStamp nvarchar(max) NULL,
        PhoneNumber nvarchar(10) NULL,
        PhoneNumberConfirmed bit NOT NULL,
        TwoFactorEnabled bit NOT NULL,
        LockoutEnd datetimeoffset NULL,
        LockoutEnabled bit NOT NULL,
        AccessFailedCount int NOT NULL,
        DisplayName nvarchar(50) NOT NULL,
        DateOfBirth date NOT NULL,
        Country nvarchar(50) NOT NULL,
        Region nvarchar(50) NOT NULL,
        City nvarchar(50) NOT NULL,
        Gender nvarchar(20) NOT NULL,
        Address nvarchar(500) NOT NULL
    );
    CREATE UNIQUE INDEX UserNameIndex ON dbo.shop_user (NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
    CREATE INDEX EmailIndex ON dbo.shop_user (NormalizedEmail);
    CREATE UNIQUE INDEX IX_shop_user_PhoneNumber ON dbo.shop_user (PhoneNumber) WHERE PhoneNumber IS NOT NULL;
END

IF OBJECT_ID(N'dbo.AspNetRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetRoles
    (
        Id nvarchar(450) NOT NULL CONSTRAINT PK_AspNetRoles PRIMARY KEY,
        Name nvarchar(256) NULL,
        NormalizedName nvarchar(256) NULL,
        ConcurrencyStamp nvarchar(max) NULL
    );
    CREATE UNIQUE INDEX RoleNameIndex ON dbo.AspNetRoles (NormalizedName) WHERE NormalizedName IS NOT NULL;
END

IF OBJECT_ID(N'dbo.AspNetUserRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserRoles
    (
        UserId nvarchar(450) NOT NULL,
        RoleId nvarchar(450) NOT NULL,
        CONSTRAINT PK_AspNetUserRoles PRIMARY KEY (UserId, RoleId),
        CONSTRAINT FK_AspNetUserRoles_shop_user FOREIGN KEY (UserId) REFERENCES dbo.shop_user (Id) ON DELETE CASCADE,
        CONSTRAINT FK_AspNetUserRoles_AspNetRoles FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_AspNetUserRoles_RoleId ON dbo.AspNetUserRoles (RoleId);
END

IF OBJECT_ID(N'dbo.AspNetUserClaims', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserClaims
    (
        Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_AspNetUserClaims PRIMARY KEY,
        UserId nvarchar(450) NOT NULL,
        ClaimType nvarchar(max) NULL,
        ClaimValue nvarchar(max) NULL,
        CONSTRAINT FK_AspNetUserClaims_shop_user FOREIGN KEY (UserId) REFERENCES dbo.shop_user (Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_AspNetUserClaims_UserId ON dbo.AspNetUserClaims (UserId);
END

IF OBJECT_ID(N'dbo.AspNetUserLogins', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserLogins
    (
        LoginProvider nvarchar(450) NOT NULL,
        ProviderKey nvarchar(450) NOT NULL,
        ProviderDisplayName nvarchar(max) NULL,
        UserId nvarchar(450) NOT NULL,
        CONSTRAINT PK_AspNetUserLogins PRIMARY KEY (LoginProvider, ProviderKey),
        CONSTRAINT FK_AspNetUserLogins_shop_user FOREIGN KEY (UserId) REFERENCES dbo.shop_user (Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_AspNetUserLogins_UserId ON dbo.AspNetUserLogins (UserId);
END

IF OBJECT_ID(N'dbo.AspNetUserTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserTokens
    (
        UserId nvarchar(450) NOT NULL,
        LoginProvider nvarchar(450) NOT NULL,
        Name nvarchar(450) NOT NULL,
        Value nvarchar(max) NULL,
        CONSTRAINT PK_AspNetUserTokens PRIMARY KEY (UserId, LoginProvider, Name),
        CONSTRAINT FK_AspNetUserTokens_shop_user FOREIGN KEY (UserId) REFERENCES dbo.shop_user (Id) ON DELETE CASCADE
    );
END

IF OBJECT_ID(N'dbo.AspNetRoleClaims', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetRoleClaims
    (
        Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_AspNetRoleClaims PRIMARY KEY,
        RoleId nvarchar(450) NOT NULL,
        ClaimType nvarchar(max) NULL,
        ClaimValue nvarchar(max) NULL,
        CONSTRAINT FK_AspNetRoleClaims_AspNetRoles FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_AspNetRoleClaims_RoleId ON dbo.AspNetRoleClaims (RoleId);
END

IF OBJECT_ID(N'dbo.shop_category', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.shop_category
    (
        Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_shop_category PRIMARY KEY,
        Name nvarchar(50) NOT NULL,
        ImagePath nvarchar(250) NOT NULL,
        CONSTRAINT UX_shop_category_Name UNIQUE (Name)
    );
END

IF OBJECT_ID(N'dbo.shop_product', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.shop_product
    (
        Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_shop_product PRIMARY KEY,
        Name nvarchar(50) NOT NULL,
        Price decimal(18, 2) NOT NULL,
        ImageUrl nvarchar(250) NOT NULL,
        CategoryId int NOT NULL,
        SellerId nvarchar(450) NOT NULL,
        Keywords nvarchar(500) NULL,
        Stock int NOT NULL,
        CONSTRAINT UX_shop_product_Name UNIQUE (Name),
        CONSTRAINT FK_shop_product_category FOREIGN KEY (CategoryId) REFERENCES dbo.shop_category (Id),
        CONSTRAINT FK_shop_product_seller FOREIGN KEY (SellerId) REFERENCES dbo.shop_user (Id)
    );
END

IF OBJECT_ID(N'dbo.shop_cart_line', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.shop_cart_line
    (
        Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_shop_cart_line PRIMARY KEY,
        UserId nvarchar(450) NOT NULL,
        ProductId int NOT NULL,
        Quantity int NOT NULL,
        CONSTRAINT UX_shop_cart_line UNIQUE (UserId, ProductId),
        CONSTRAINT FK_shop_cart_user FOREIGN KEY (UserId) REFERENCES dbo.shop_user (Id) ON DELETE CASCADE,
        CONSTRAINT FK_shop_cart_product FOREIGN KEY (ProductId) REFERENCES dbo.shop_product (Id) ON DELETE CASCADE
    );
END

IF OBJECT_ID(N'dbo.shop_order', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.shop_order
    (
        Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_shop_order PRIMARY KEY,
        Number varchar(32) NOT NULL,
        UserId nvarchar(450) NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        Total decimal(18, 2) NOT NULL,
        CONSTRAINT UX_shop_order_Number UNIQUE (Number),
        CONSTRAINT FK_shop_order_user FOREIGN KEY (UserId) REFERENCES dbo.shop_user (Id)
    );
END

IF OBJECT_ID(N'dbo.shop_order_line', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.shop_order_line
    (
        Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_shop_order_line PRIMARY KEY,
        OrderId int NOT NULL,
        ProductId int NULL,
        ProductName nvarchar(50) NOT NULL,
        UnitPrice decimal(18, 2) NOT NULL,
        Quantity int NOT NULL,
        LineTotal decimal(18, 2) NOT NULL,
        CONSTRAINT FK_shop_order_line_order FOREIGN KEY (OrderId) REFERENCES dbo.shop_order (Id) ON DELETE CASCADE,
        CONSTRAINT FK_shop_order_line_product FOREIGN KEY (ProductId) REFERENCES dbo.shop_product (Id) ON DELETE SET NULL
    );
END

IF OBJECT_ID(N'dbo.shop_contact_message', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.shop_contact_message
    (
        Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_shop_contact_message PRIMARY KEY,
        Name nvarchar(50) NOT NULL,
        Email nvarchar(50) NOT NULL,
        Message nvarchar(500) NOT NULL,
        CreatedAt datetimeoffset NOT NULL
    );
END

IF OBJECT_ID(N'dbo.shop_password_reset_quota', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.shop_password_reset_quota
    (
        UserId nvarchar(450) NOT NULL CONSTRAINT PK_shop_password_reset_quota PRIMARY KEY,
        WindowStart datetimeoffset NOT NULL,
        Count int NOT NULL
    );
END
