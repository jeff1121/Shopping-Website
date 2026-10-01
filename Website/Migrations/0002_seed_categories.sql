-- 0002：商品分類（Plan.md §10.2）。
-- 名稱必須與 addProducts.aspx.cs 的固定清單完全一致（Computer Accesories 拼字保留原樣）。
INSERT INTO dbo.violet_categories (name, cimage)
SELECT
    v.name,
    v.cimage
FROM (
    VALUES
    ('Computer', 'img/categories/desktop.png'),
    ('Computer Accesories', 'img/categories/laptop.png')
) AS v (name, cimage)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.violet_categories AS c
    WHERE c.name = v.name
);
