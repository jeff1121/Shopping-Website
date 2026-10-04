using Microsoft.EntityFrameworkCore;
using Shopping.Data;

namespace Shopping.Web.Services;

/// <summary>目錄上的一張商品卡。</summary>
public sealed class ProductCard
{
    /// <summary>識別碼。</summary>
    public int Id { get; set; }

    /// <summary>名稱。</summary>
    public string Name { get; set; } = "";

    /// <summary>價格。</summary>
    public decimal Price { get; set; }

    /// <summary>庫存。</summary>
    public int Stock { get; set; }

    /// <summary>圖片網址。</summary>
    public string ImageUrl { get; set; } = "";

    /// <summary>賣家顯示名稱。</summary>
    public string SellerName { get; set; } = "";

    /// <summary>分類名稱。</summary>
    public string CategoryName { get; set; } = "";

    /// <summary>關鍵字。</summary>
    public string Keywords { get; set; } = "";

    /// <summary>可選數量上限。</summary>
    public int MaxQuantity { get; set; }
}

/// <summary>分類連結。</summary>
public sealed class CategoryLink
{
    /// <summary>識別碼。</summary>
    public int Id { get; set; }

    /// <summary>名稱。</summary>
    public string Name { get; set; } = "";

    /// <summary>圖片。</summary>
    public string ImagePath { get; set; } = "";
}

/// <summary>目錄查詢。</summary>
public sealed class CatalogService
{
    private readonly ShoppingDbContext _db;

    /// <summary>建立服務。</summary>
    public CatalogService(ShoppingDbContext db)
    {
        _db = db;
    }

    /// <summary>依關鍵字、分類與排序列出商品。</summary>
    public async Task<IReadOnlyList<ProductCard>> SearchAsync(string? keywordRaw, int? categoryId, string? sort, CancellationToken cancellationToken)
    {
        var keyword = (keywordRaw ?? "").Trim();
        var query = _db.Products.AsQueryable();
        if (keyword.Length > 0)
        {
            query = query.Where(product => product.Name.Contains(keyword) || (product.Keywords != null && product.Keywords.Contains(keyword)));
        }

        if (categoryId is int selected)
        {
            query = query.Where(product => product.CategoryId == selected);
        }

        query = sort switch
        {
            "price-asc" => query.OrderBy(product => product.Price).ThenBy(product => product.Name),
            "price-desc" => query.OrderByDescending(product => product.Price).ThenBy(product => product.Name),
            _ => query.OrderBy(_ => ShoppingDbContext.NewId()),
        };
        var items = await query.Select(product => new ProductCard
        {
            Id = product.Id,
            Name = product.Name,
            Price = product.Price,
            Stock = product.Stock,
            ImageUrl = product.ImageUrl,
            SellerName = product.Seller!.DisplayName,
            CategoryName = product.Category!.Name,
            Keywords = product.Keywords ?? "",
        }).ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            item.MaxQuantity = Math.Min(10, item.Stock);
        }

        return items;
    }

    /// <summary>全部分類。</summary>
    public async Task<IReadOnlyList<CategoryLink>> CategoriesAsync(CancellationToken cancellationToken)
    {
        return await _db.Categories.OrderBy(category => category.Name).Select(category => new CategoryLink
        {
            Id = category.Id,
            Name = category.Name,
            ImagePath = category.ImagePath,
        }).ToListAsync(cancellationToken);
    }

    /// <summary>單一商品。不存在時回傳 null。</summary>
    public async Task<ProductCard?> FindAsync(int id, CancellationToken cancellationToken)
    {
        var items = await _db.Products.Where(product => product.Id == id).Select(product => new ProductCard
        {
            Id = product.Id,
            Name = product.Name,
            Price = product.Price,
            Stock = product.Stock,
            ImageUrl = product.ImageUrl,
            SellerName = product.Seller!.DisplayName,
            CategoryName = product.Category!.Name,
            Keywords = product.Keywords ?? "",
        }).ToListAsync(cancellationToken);
        if (items.Count == 0)
        {
            return null;
        }

        var item = items[0];
        item.MaxQuantity = Math.Min(10, item.Stock);
        return item;
    }
}
