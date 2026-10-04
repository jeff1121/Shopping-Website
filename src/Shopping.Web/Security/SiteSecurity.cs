using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace Shopping.Web.Security;

/// <summary>只允許站內相對網址。</summary>
public static class ReturnUrls
{
    /// <summary>不是站內路徑時回首頁。</summary>
    public static string LocalOrHome(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return "/";
        }

        if (url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal))
        {
            return url;
        }

        return "/";
    }
}

/// <summary>把語系收斂成 zh-Hant 或 en。</summary>
public static class CultureCodes
{
    /// <summary>不支援的值變成 zh-Hant。</summary>
    public static string Normalize(string? culture)
    {
        if (culture == "en")
        {
            return "en";
        }

        return "zh-Hant";
    }
}

/// <summary>台北時區的日期。</summary>
public static class TaipeiClock
{
    /// <summary>格式化成 yyyy-MM-dd。</summary>
    public static string Format(DateTimeOffset instant)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
        return TimeZoneInfo.ConvertTime(instant, zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    /// <summary>TimeProvider 的 UTC 日期。</summary>
    public static DateOnly UtcToday(TimeProvider time)
    {
        return DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
    }
}

/// <summary>訂單編號產生器。</summary>
public interface IOrderNumberFactory
{
    /// <summary>產生一組新編號。</summary>
    string Create();
}

/// <summary>V 加 ULID。</summary>
public sealed class UlidOrderNumberFactory : IOrderNumberFactory
{
    /// <summary>產生長度 27 的編號。</summary>
    public string Create()
    {
        return "V" + Ulid.NewUlid().ToString();
    }
}

/// <summary>安全標頭。</summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>建立中介層。</summary>
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>寫入標頭。</summary>
    public Task Invoke(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XFrameOptions = "DENY";
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data: https:; font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
            headers.Remove("X-Powered-By");
            headers.Remove("Server");
            return Task.CompletedTask;
        });
        return _next(context);
    }
}

/// <summary>GET 的 culture 參數寫入語系 Cookie。</summary>
public sealed class CultureCookieMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>建立中介層。</summary>
    public CultureCookieMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>必要時寫入 Cookie。</summary>
    public Task Invoke(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method) && context.Request.Query.ContainsKey("culture"))
        {
            var culture = CultureCodes.Normalize(context.Request.Query["culture"].ToString());
            context.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { IsEssential = true, Secure = true, SameSite = SameSiteMode.Lax, HttpOnly = false, Path = "/" });
        }

        return _next(context);
    }
}
