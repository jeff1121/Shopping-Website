using Microsoft.EntityFrameworkCore;
using Shopping.Data;
using Shopping.Web.Security;

namespace Shopping.Web.Services;

/// <summary>訂單摘要。</summary>
public sealed class OrderSummary
{
    /// <summary>編號。</summary>
    public string Number { get; set; } = "";

    /// <summary>台北日期。</summary>
    public string DateText { get; set; } = "";

    /// <summary>總額。</summary>
    public decimal Total { get; set; }

    /// <summary>品名。</summary>
    public IReadOnlyList<string> Names { get; set; } = [];
}

/// <summary>訂單查詢與 PDF。</summary>
public sealed class InvoiceService
{
    private readonly ShoppingDbContext _db;

    /// <summary>建立服務。</summary>
    public InvoiceService(ShoppingDbContext db)
    {
        _db = db;
    }

    /// <summary>目前使用者的訂單。</summary>
    public async Task<IReadOnlyList<OrderSummary>> ListAsync(string userId, CancellationToken cancellationToken)
    {
        var orders = await _db.Orders.Where(item => item.UserId == userId).OrderByDescending(item => item.CreatedAt).Include(item => item.Lines).ToListAsync(cancellationToken);
        var list = new List<OrderSummary>();
        foreach (var order in orders)
        {
            list.Add(new OrderSummary
            {
                Number = order.Number,
                DateText = TaipeiClock.Format(order.CreatedAt),
                Total = order.Total,
                Names = order.Lines.Select(line => line.ProductName).ToList(),
            });
        }

        return list;
    }

    /// <summary>產生自己的訂單 PDF。別人的編號回傳 null。</summary>
    public async Task<byte[]?> RenderAsync(string userId, string number, CancellationToken cancellationToken)
    {
        var order = await _db.Orders.Include(item => item.Lines).FirstOrDefaultAsync(item => item.Number == number, cancellationToken);
        if (order is null || order.UserId != userId)
        {
            return null;
        }

        var lines = new List<InvoiceLine>();
        foreach (var line in order.Lines)
        {
            lines.Add(new InvoiceLine(line.ProductName, line.UnitPrice, line.Quantity, line.LineTotal));
        }

        return InvoicePdf.Render(order.Number, TaipeiClock.Format(order.CreatedAt), lines, order.Total);
    }
}
