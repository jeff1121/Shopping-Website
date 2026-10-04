using Microsoft.EntityFrameworkCore;
using Shopping.Data;

namespace Shopping.Web.Services;

/// <summary>賣家維護結果。</summary>
public enum SellerEditStatus
{
    /// <summary>已寫入。</summary>
    Ok,

    /// <summary>找不到或不是貨主。</summary>
    NotFound,

    /// <summary>欄位不合法。</summary>
    Invalid,

    /// <summary>尚未確認刪除。</summary>
    NeedsConfirm,
}

/// <summary>上架表單。</summary>
public sealed class ProductForm
{
    /// <summary>名稱。</summary>
    public string? Name { get; set; }

    /// <summary>價格文字。</summary>
    public string? Price { get; set; }

    /// <summary>分類。</summary>
    public int CategoryId { get; set; }

    /// <summary>關鍵字。</summary>
    public string? Keywords { get; set; }
}

/// <summary>上架、修改與刪除。</summary>
public sealed class SellerService
{
    private readonly ShoppingDbContext _db;
    private readonly ImageStore _images;

    /// <summary>建立服務。</summary>
    public SellerService(ShoppingDbContext db, ImageStore images)
    {
        _db = db;
        _images = images;
    }

    /// <summary>上架。庫存固定為 0。失敗不上傳。</summary>
    public async Task<IReadOnlyDictionary<string, string>> CreateAsync(string userId, string userName, ProductForm form, Stream? image, string? fileName, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string>();
        if (!InputRules.TryProductName(form.Name, out var name))
        {
            errors["Name"] = string.IsNullOrWhiteSpace(form.Name) ? "Required" : "NameInvalid";
        }

        if (!InputRules.TryPrice(form.Price, out var price))
        {
            errors["Price"] = string.IsNullOrWhiteSpace(form.Price) ? "Required" : "PriceInvalid";
        }

        if (!InputRules.IsKeywords(form.Keywords))
        {
            errors["Keywords"] = "KeywordsInvalid";
        }

        if (image is null || string.IsNullOrWhiteSpace(fileName))
        {
            errors["Image"] = "ImageRequired";
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var categoryOk = await _db.Categories.AnyAsync(item => item.Id == form.CategoryId, cancellationToken);
        if (!categoryOk)
        {
            errors["CategoryId"] = "CategoryInvalid";
            return errors;
        }

        var nameTaken = await _db.Products.AnyAsync(item => item.Name == name, cancellationToken);
        if (nameTaken)
        {
            errors["Name"] = "NameTaken";
            return errors;
        }

        string url;
        try
        {
            url = await _images.UploadAsync(image!, userName, Path.GetExtension(fileName!), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            errors["Image"] = "ImageInvalid";
            return errors;
        }

        _db.Products.Add(new Product
        {
            Name = name,
            Price = price,
            CategoryId = form.CategoryId,
            SellerId = userId,
            Keywords = form.Keywords ?? "",
            ImageUrl = url,
            Stock = 0,
        });
        await _db.SaveChangesAsync(cancellationToken);
        return errors;
    }

    /// <summary>修改自己的價格、庫存與關鍵字。</summary>
    public async Task<SellerEditStatus> UpdateAsync(string userId, int id, string? priceText, string? stockText, string? keywords, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null || product.SellerId != userId)
        {
            return SellerEditStatus.NotFound;
        }

        if (!InputRules.TryPrice(priceText, out var price) || !InputRules.TryStock(stockText, out var stock) || !InputRules.IsKeywords(keywords))
        {
            return SellerEditStatus.Invalid;
        }

        product.Price = price;
        product.Stock = stock;
        product.Keywords = keywords ?? "";
        await _db.SaveChangesAsync(cancellationToken);
        return SellerEditStatus.Ok;
    }

    /// <summary>確認後刪除自己的商品。</summary>
    public async Task<SellerEditStatus> DeleteAsync(string userId, int id, bool confirm, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null || product.SellerId != userId)
        {
            return SellerEditStatus.NotFound;
        }

        if (!confirm)
        {
            return SellerEditStatus.NeedsConfirm;
        }

        _db.Products.Remove(product);
        await _db.SaveChangesAsync(cancellationToken);
        return SellerEditStatus.Ok;
    }

    /// <summary>賣家自己的商品。</summary>
    public async Task<IReadOnlyList<Product>> MineAsync(string userId, CancellationToken cancellationToken)
    {
        return await _db.Products.Where(item => item.SellerId == userId).OrderBy(item => item.Name).ToListAsync(cancellationToken);
    }
}
