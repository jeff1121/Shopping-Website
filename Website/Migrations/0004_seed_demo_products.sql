-- 0004：示範商品（Plan.md §10.2）。
-- pimage 指向 Blob 的範例圖片（Website/img/products/demo_seller/ 由部署流程上傳）；
-- $IMAGE_BASE_URL$ 由 DbUp 在執行時替換為環境變數 IMAGE_BASE_URL。
-- 最後一項庫存為 0，用來展示「售完」圖示。
INSERT INTO dbo.violet_products (
    pname, price, pimage, category, uname, keywords, stock
)
SELECT
    v.pname,
    v.price,
    v.pimage,
    v.category,
    v.uname,
    v.keywords,
    v.stock
FROM (
    VALUES
    (
        'Demo Smart Watch', 2999.00,
        '$IMAGE_BASE_URL$/products/demo_seller/watch.png',
        'Computer Accesories', 'Demo Seller', 'watch smart wearable demo', 20
    ),
    (
        'Demo Laser Printer', 8999.00,
        '$IMAGE_BASE_URL$/products/demo_seller/printer.png',
        'Computer Accesories', 'Demo Seller', 'printer office demo', 5
    ),
    (
        'Demo Laptop', 32999.00,
        '$IMAGE_BASE_URL$/products/demo_seller/laptop.png',
        'Computer', 'Demo Seller', 'laptop notebook demo', 10
    ),
    (
        'Demo Arcade Machine', 15999.00,
        '$IMAGE_BASE_URL$/products/demo_seller/arcade.png',
        'Computer', 'Demo Seller', 'arcade game demo', 0
    )
) AS v (pname, price, pimage, category, uname, keywords, stock)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.violet_products AS p
    WHERE p.pname = v.pname
);
