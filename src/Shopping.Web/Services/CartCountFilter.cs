using Microsoft.AspNetCore.Mvc.Filters;
using Shopping.Web.Controllers;

namespace Shopping.Web.Services;

/// <summary>把購物車件數放進 HttpContext，錯誤頁不查資料庫。</summary>
public sealed class CartCountFilter : IAsyncActionFilter
{
    private readonly CartService _cart;

    /// <summary>建立篩選器。</summary>
    public CartCountFilter(CartService cart)
    {
        _cart = cart;
    }

    /// <summary>非錯誤頁才查件數。</summary>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.Controller is not ErrorController)
        {
            var count = await _cart.TotalAsync(context.HttpContext.User, context.HttpContext.RequestAborted);
            context.HttpContext.Items["CartCount"] = count;
        }

        await next();
    }
}
