using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shopping.Data;
using Shopping.Web.Security;

namespace Shopping.Web.Services;

/// <summary>結帳結果。</summary>
public abstract record CheckoutResult
{
    private CheckoutResult()
    {
    }

    /// <summary>沒有購物車。</summary>
    public sealed record Empty : CheckoutResult;

    /// <summary>已成立。</summary>
    public sealed record Placed(string Number) : CheckoutResult;

    /// <summary>庫存不足。</summary>
    public sealed record Insufficient(string ProductName) : CheckoutResult;

    /// <summary>編號連續碰撞。</summary>
    public sealed record Unavailable : CheckoutResult;
}

/// <summary>在單一交易裡扣庫存並建立訂單。</summary>
public sealed class CheckoutService
{
    private readonly ShoppingDbContext _db;
    private readonly IOrderNumberFactory _numbers;
    private readonly TimeProvider _clock;

    /// <summary>建立服務。</summary>
    public CheckoutService(ShoppingDbContext db, IOrderNumberFactory numbers, TimeProvider clock)
    {
        _db = db;
        _numbers = numbers;
        _clock = clock;
    }

    /// <summary>結帳。編號碰撞時換一組再試一次。</summary>
    public async Task<CheckoutResult> PlaceAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            return await TryOnceAsync(userId, cancellationToken);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            try
            {
                return await TryOnceAsync(userId, cancellationToken);
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();
                return new CheckoutResult.Unavailable();
            }
        }
    }

    private async Task<CheckoutResult> TryOnceAsync(string userId, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var lines = await _db.CartLines.Where(item => item.UserId == userId).ToListAsync(cancellationToken);
        if (lines.Count == 0)
        {
            return new CheckoutResult.Empty();
        }

        var ids = lines.Select(item => item.ProductId).Distinct().ToList();
        await LockAsync(ids, cancellationToken);
        var products = await _db.Products.Where(item => ids.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        foreach (var line in lines)
        {
            var product = products[line.ProductId];
            if (product.Stock < line.Quantity)
            {
                return new CheckoutResult.Insufficient(product.Name);
            }
        }

        var order = new ShopOrder
        {
            Number = _numbers.Create(),
            UserId = userId,
            CreatedAt = _clock.GetUtcNow(),
            Total = 0,
        };
        foreach (var line in lines)
        {
            var product = products[line.ProductId];
            product.Stock -= line.Quantity;
            var lineTotal = product.Price * line.Quantity;
            order.Total += lineTotal;
            order.Lines.Add(new OrderLine
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = line.Quantity,
                LineTotal = lineTotal,
            });
        }

        _db.Orders.Add(order);
        _db.CartLines.RemoveRange(lines);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CheckoutResult.Placed(order.Number);
    }

    private async Task LockAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken)
    {
        var parameters = new object[ids.Count];
        var names = new string[ids.Count];
        for (var index = 0; index < ids.Count; index++)
        {
            names[index] = "@p" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            parameters[index] = new SqlParameter(names[index], ids[index]);
        }

        var sql = "SELECT Id FROM dbo.shop_product WITH (UPDLOCK, ROWLOCK, HOLDLOCK) WHERE Id IN (" + string.Join(", ", names) + ")";
        await _db.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
    }
}
