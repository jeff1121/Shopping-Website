using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Shopping.Data;
using Shopping.Web.Services;

namespace Shopping.Web;

/// <summary>從 ViewData 取出頁面清單。</summary>
public static class ViewLists
{
    /// <summary>訂單。</summary>
    public static IReadOnlyList<OrderSummary> Orders(ViewDataDictionary data)
    {
        return (IReadOnlyList<OrderSummary>)data["Orders"]!;
    }

    /// <summary>賣家商品。</summary>
    public static IEnumerable<Product> Products(ViewDataDictionary data)
    {
        return (IEnumerable<Product>)data["Products"]!;
    }

    /// <summary>分類。</summary>
    public static IReadOnlyList<CategoryLink> Categories(ViewDataDictionary data)
    {
        return (IReadOnlyList<CategoryLink>)data["Categories"]!;
    }
}
