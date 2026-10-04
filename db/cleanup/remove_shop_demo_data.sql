-- 清除新站示範資料。不在 migration 資料夾，不會自動執行，部署時也不要執行。
-- 只刪除 demo_customer、demo_seller、demo_operator 與其訂單、購物車、商品與配額。
-- 保留 shop_category。不碰 violet_ 開頭的表。
BEGIN TRANSACTION;

DELETE FROM dbo.shop_order_line
WHERE OrderId IN (
    SELECT shop_order.Id
    FROM dbo.shop_order
    INNER JOIN dbo.shop_user ON shop_user.Id = shop_order.UserId
    WHERE shop_user.UserName IN ('demo_customer', 'demo_seller', 'demo_operator')
);

DELETE FROM dbo.shop_order
WHERE UserId IN (
    SELECT Id
    FROM dbo.shop_user
    WHERE UserName IN ('demo_customer', 'demo_seller', 'demo_operator')
);

DELETE FROM dbo.shop_cart_line
WHERE UserId IN (
    SELECT Id
    FROM dbo.shop_user
    WHERE UserName IN ('demo_customer', 'demo_seller', 'demo_operator')
);

DELETE FROM dbo.shop_product
WHERE SellerId IN (
    SELECT Id
    FROM dbo.shop_user
    WHERE UserName = 'demo_seller'
);

DELETE FROM dbo.shop_password_reset_quota
WHERE UserId IN (
    SELECT Id
    FROM dbo.shop_user
    WHERE UserName IN ('demo_customer', 'demo_seller', 'demo_operator')
);

DELETE FROM dbo.AspNetUserRoles
WHERE UserId IN (
    SELECT Id
    FROM dbo.shop_user
    WHERE UserName IN ('demo_customer', 'demo_seller', 'demo_operator')
);

DELETE FROM dbo.shop_user
WHERE UserName IN ('demo_customer', 'demo_seller', 'demo_operator');

COMMIT TRANSACTION;
