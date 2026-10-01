-- 清除示範資料（Plan.md §10.3）。不在 migration 資料夾，不會自動執行；轉入正式營運時（M8）人工執行。
-- 刪除示範帳號的購物車、訂單、商品與帳號；保留分類。
-- 範例圖片另以：az storage blob delete-batch --account-name <帳戶> --source products \
--   --pattern 'demo_seller/*' --auth-mode login
BEGIN TRANSACTION;

DELETE FROM dbo.violet_cart
WHERE uname IN ('Demo Customer', 'Demo Seller') OR sname = 'Demo Seller';

DELETE FROM dbo.violet_order
WHERE uname IN ('Demo Customer', 'Demo Seller');

DELETE FROM dbo.violet_products
WHERE uname = 'Demo Seller';

DELETE FROM dbo.violet_user_login
WHERE username IN ('demo_customer', 'demo_seller');

COMMIT TRANSACTION;
