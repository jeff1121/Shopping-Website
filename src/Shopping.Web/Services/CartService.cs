using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Shopping.Data;

namespace Shopping.Web.Services;

/// <summary>加入購物車的結果。</summary>
public enum CartAddStatus
{
    /// <summary>已寫入。</summary>
    Ok,

    /// <summary>沒有這個商品。</summary>
    Missing,

    /// <summary>已售完。</summary>
    SoldOut,

    /// <summary>數量不允許。</summary>
    Rejected,
}

/// <summary>購物車的一列。</summary>
public sealed class CartRow
{
    /// <summary>商品識別碼。</summary>
    public int ProductId { get; set; }

    /// <summary>名稱。</summary>
    public string Name { get; set; } = "";

    /// <summary>現價。</summary>
    public decimal Price { get; set; }

    /// <summary>數量。</summary>
    public int Quantity { get; set; }

    /// <summary>圖片。</summary>
    public string ImageUrl { get; set; } = "";

    /// <summary>小計。</summary>
    public decimal LineTotal { get; set; }
}

/// <summary>購物車頁面。</summary>
public sealed class CartPage
{
    /// <summary>列。</summary>
    public IReadOnlyList<CartRow> Lines { get; set; } = [];

    /// <summary>總額。</summary>
    public decimal Total { get; set; }
}

/// <summary>購物車。不改庫存。</summary>
public sealed class CartService
{
    private readonly ShoppingDbContext _db;

    /// <summary>建立服務。</summary>
    public CartService(ShoppingDbContext db)
    {
        _db = db;
    }

    /// <summary>加入或合併數量。</summary>
    public async Task<CartAddStatus> AddAsync(string userId, int productId, int quantity, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(item => item.Id == productId, cancellationToken);
        if (product is null)
        {
            return CartAddStatus.Missing;
        }

        if (product.Stock <= 0)
        {
            return CartAddStatus.SoldOut;
        }

        if (quantity < 1 || quantity > 10 || quantity > product.Stock)
        {
            return CartAddStatus.Rejected;
        }

        var line = await _db.CartLines.FirstOrDefaultAsync(item => item.UserId == userId && item.ProductId == productId, cancellationToken);
        var current = line is null ? 0 : line.Quantity;
        var next = current + quantity;
        if (next > 10 || next > product.Stock)
        {
            return CartAddStatus.Rejected;
        }

        if (line is null)
        {
            _db.CartLines.Add(new CartLine { UserId = userId, ProductId = productId, Quantity = quantity });
        }
        else
        {
            line.Quantity = next;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return CartAddStatus.Ok;
    }

    /// <summary>改數量。</summary>
    public async Task<bool> UpdateAsync(string userId, int productId, int quantity, CancellationToken cancellationToken)
    {
        var line = await _db.CartLines.FirstOrDefaultAsync(item => item.UserId == userId && item.ProductId == productId, cancellationToken);
        if (line is null)
        {
            return false;
        }

        var product = await _db.Products.FirstAsync(item => item.Id == productId, cancellationToken);
        if (quantity < 1 || quantity > 10 || quantity > product.Stock)
        {
            return false;
        }

        line.Quantity = quantity;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>移除一列。</summary>
    public async Task<bool> RemoveAsync(string userId, int productId, CancellationToken cancellationToken)
    {
        var line = await _db.CartLines.FirstOrDefaultAsync(item => item.UserId == userId && item.ProductId == productId, cancellationToken);
        if (line is null)
        {
            return false;
        }

        _db.CartLines.Remove(line);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>讀取購物車，小計用現價。</summary>
    public async Task<CartPage> ReadAsync(string userId, CancellationToken cancellationToken)
    {
        var lines = await _db.CartLines.Where(item => item.UserId == userId).Select(item => new CartRow
        {
            ProductId = item.ProductId,
            Name = item.Product!.Name,
            Price = item.Product.Price,
            Quantity = item.Quantity,
            ImageUrl = item.Product.ImageUrl,
            LineTotal = item.Product.Price * item.Quantity,
        }).ToListAsync(cancellationToken);
        decimal total = 0;
        foreach (var line in lines)
        {
            total += line.LineTotal;
        }

        return new CartPage { Lines = lines, Total = total };
    }

    /// <summary>徽章用的數量加總。未登入為 0。</summary>
    public async Task<int> TotalAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(id))
        {
            return 0;
        }

        var sum = await _db.CartLines.Where(item => item.UserId == id).SumAsync(item => (int?)item.Quantity, cancellationToken);
        return sum ?? 0;
    }
}
