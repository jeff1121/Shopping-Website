namespace Shopping.Web.Services;

/// <summary>結帳頁面。</summary>
public sealed class CheckoutPage
{
    /// <summary>購物車。</summary>
    public CartPage Cart { get; set; } = new();

    /// <summary>台北日期。</summary>
    public string DateText { get; set; } = "";

    /// <summary>庫存不足的品名，或 unavailable。</summary>
    public string? Message { get; set; }

    /// <summary>成立後的編號。</summary>
    public string? OrderNumber { get; set; }
}
