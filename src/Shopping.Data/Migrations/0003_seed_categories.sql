IF NOT EXISTS (SELECT 1 FROM dbo.shop_category WHERE Name = N'Computer')
    INSERT INTO dbo.shop_category (Name, ImagePath) VALUES (N'Computer', N'/images/categories/desktop.png');
IF NOT EXISTS (SELECT 1 FROM dbo.shop_category WHERE Name = N'Computer Accessories')
    INSERT INTO dbo.shop_category (Name, ImagePath) VALUES (N'Computer Accessories', N'/images/categories/laptop.png');
