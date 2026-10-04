using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shopping.Data;
using Shopping.Web.Security;
using Shopping.Web.Services;

namespace Shopping.Web.Tests;

/// <summary>透過測試主機走每條使用者流程。</summary>
public sealed class FlowTests : IClassFixture<ShopApp>
{
    private static long _phones = Random.Shared.NextInt64(2_000_000_000, 7_000_000_000);
    private readonly ShopApp _app;

    /// <summary>建立測試。</summary>
    public FlowTests(ShopApp app)
    {
        _app = app;
    }

    /// <summary>版面、匿名與已登入導覽。</summary>
    [Fact(DisplayName = "FR-01 FR-02 FR-05 shared layout")]
    public async Task LayoutChangesWithSignIn()
    {
        var anonymous = _app.CreateNoRedirectClient();
        var home = await anonymous.GetStringAsync("/");
        Assert.Contains("市集", home, StringComparison.Ordinal);
        Assert.Contains("跳至主要內容", home, StringComparison.Ordinal);
        Assert.Contains("登入", home, StringComparison.Ordinal);
        Assert.Contains("註冊", home, StringComparison.Ordinal);
        Assert.DoesNotContain("上架商品", home, StringComparison.Ordinal);
        var client = await LoginAsync("demo_customer");
        var signedIn = await client.GetStringAsync("/");
        Assert.Contains("帳戶", signedIn, StringComparison.Ordinal);
        Assert.Contains("登出", signedIn, StringComparison.Ordinal);
        Assert.Contains("購物車", signedIn, StringComparison.Ordinal);
    }

    /// <summary>賣家與管理者才看得到自己的連結。</summary>
    [Fact(DisplayName = "FR-03 role navigation")]
    public async Task RoleNavigation()
    {
        var seller = await LoginAsync("demo_seller");
        var sellerHome = await seller.GetStringAsync("/");
        Assert.Contains("href=\"/seller/products/new\"", sellerHome, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/operator/sellers\"", sellerHome, StringComparison.Ordinal);
        var op = await LoginAsync("demo_operator");
        var opHome = await op.GetStringAsync("/");
        Assert.Contains("href=\"/operator/sellers\"", opHome, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/seller/products/new\"", opHome, StringComparison.Ordinal);
    }

    /// <summary>購物車徽章是數量加總，登出後版面恢復匿名。</summary>
    [Fact(DisplayName = "FR-04 cart badge")]
    public async Task CartBadge()
    {
        var id = await ProductIdAsync("Demo Laptop");
        var client = await LoginAsync("demo_customer");
        await ClearCartAsync("demo_customer");
        var token = await TokenAsync(client, "/");
        var added = await PostAsync(client, "/cart", token, new Dictionary<string, string> { ["productId"] = id.ToString(CultureInfo.InvariantCulture), ["quantity"] = "2" });
        Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
        var page = await client.GetStringAsync("/");
        Assert.Contains(">2<", page, StringComparison.Ordinal);
        var logout = await PostAsync(client, "/account/logout", await TokenAsync(client, "/"), new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        var again = await client.GetStringAsync("/");
        Assert.Contains("登入", again, StringComparison.Ordinal);
        await ClearCartAsync("demo_customer");
    }

    /// <summary>窄版導覽使用按鈕切換，而不是永久隱藏。</summary>
    [Fact(DisplayName = "FR-06 mobile navigation hook")]
    public async Task MobileNavigationMarkup()
    {
        var html = await _app.CreateNoRedirectClient().GetStringAsync("/");
        Assert.Contains("id=\"nav-toggle\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"site-nav\"", html, StringComparison.Ordinal);
        Assert.Contains("/js/nav.js", html, StringComparison.Ordinal);
    }

    /// <summary>語系 Cookie 與搜尋。</summary>
    [Fact(DisplayName = "FR-07 FR-08 culture and search")]
    public async Task CultureAndSearch()
    {
        var client = _app.CreateNoRedirectClient();
        var token = await TokenAsync(client, "/");
        var switched = await PostAsync(client, "/culture", token, new Dictionary<string, string> { ["culture"] = "en", ["returnUrl"] = "/" });
        Assert.Equal(HttpStatusCode.Redirect, switched.StatusCode);
        var english = await client.GetStringAsync("/");
        Assert.Contains("Market", english, StringComparison.Ordinal);
        Assert.Contains("Search", english, StringComparison.Ordinal);
        var chinese = await client.GetAsync("/?culture=zh-Hant");
        Assert.Contains(".AspNetCore.Culture", chinese.Headers.GetValues("Set-Cookie").First(), StringComparison.Ordinal);
        var ignored = await client.GetAsync("/?culture=fr");
        Assert.Equal(HttpStatusCode.OK, ignored.StatusCode);
        var searched = await client.GetAsync("/?keyword=%20laptop%20");
        var body = await searched.Content.ReadAsStringAsync();
        Assert.Contains("Demo Laptop", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Demo Smart Watch", body, StringComparison.Ordinal);
    }

    /// <summary>目錄排序、空結果與不存在的分類。</summary>
    [Fact(DisplayName = "FR-11 FR-12 FR-13 FR-14 FR-16 catalog filters")]
    public async Task CatalogFilters()
    {
        var client = _app.CreateNoRedirectClient();
        var random = await client.GetStringAsync("/?sort=random");
        Assert.Contains("Demo Laptop", random, StringComparison.Ordinal);
        Assert.Contains("Demo Laser Printer", random, StringComparison.Ordinal);
        var asc = await client.GetStringAsync("/?sort=price-asc");
        Assert.True(asc.IndexOf("Demo Smart Watch", StringComparison.Ordinal) < asc.IndexOf("Demo Laptop", StringComparison.Ordinal));
        var desc = await client.GetStringAsync("/?sort=price-desc");
        Assert.True(desc.IndexOf("Demo Laptop", StringComparison.Ordinal) < desc.IndexOf("Demo Smart Watch", StringComparison.Ordinal));
        var keyword = await client.GetStringAsync("/?keyword=WATCH");
        Assert.Contains("Demo Smart Watch", keyword, StringComparison.Ordinal);
        Assert.DoesNotContain("Demo Laptop", keyword, StringComparison.Ordinal);
        var empty = await client.GetAsync("/?keyword=zzzz-no-such-product");
        var emptyBody = await empty.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Contains("沒有符合的商品", emptyBody, StringComparison.Ordinal);
        var missingCategory = await client.GetAsync("/?category=999999");
        Assert.Equal(HttpStatusCode.OK, missingCategory.StatusCode);
        Assert.Contains("沒有符合的商品", await missingCategory.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var plain = await client.GetStringAsync("/");
        Assert.Contains("如何成為賣家", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("註冊成賣家", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("sellerRegister", plain, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>預設目錄排序必須是每一列的 NEWID()，不能是整段查詢共用的 RAND()。</summary>
    [Fact(DisplayName = "FR-11 default order uses NEWID")]
    public async Task DefaultOrderUsesNewIdPerRow()
    {
        using var scope = _app.Services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogService>();
        using var capture = new CommandCapture();
        var cards = await catalog.SearchAsync(null, null, null, CancellationToken.None);
        var sql = capture.Commands.First(command => command.Contains("shop_product", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("NEWID()", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RAND(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(cards, card => card.Name == "Demo Laptop");
        Assert.Contains(cards, card => card.Name == "Demo Arcade Machine");
    }

    /// <summary>分類頁連到該分類的目錄。</summary>
    [Fact(DisplayName = "FR-15 categories")]
    public async Task Categories()
    {
        var html = await _app.CreateNoRedirectClient().GetStringAsync("/categories");
        Assert.Contains("Computer Accessories", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Computer Accesories", html, StringComparison.Ordinal);
        var id = Regex.Match(html, "href=\"/\\?category=(\\d+)\"").Groups[1].Value;
        var filtered = await _app.CreateNoRedirectClient().GetStringAsync("/?category=" + id);
        Assert.Contains("Demo", filtered, StringComparison.Ordinal);
    }

    /// <summary>售完與數量上限。</summary>
    [Fact(DisplayName = "FR-17 FR-18 sold out and quantity")]
    public async Task SoldOutAndQuantity()
    {
        var html = await _app.CreateNoRedirectClient().GetStringAsync("/");
        Assert.Contains("已售完", html, StringComparison.Ordinal);
        Assert.Contains("disabled", html, StringComparison.Ordinal);
        var watch = Card(html, "Demo Smart Watch");
        Assert.Contains("value=\"10\"", watch, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"11\"", watch, StringComparison.Ordinal);
        var printer = Card(html, "Demo Laser Printer");
        Assert.Contains("value=\"5\"", printer, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"6\"", printer, StringComparison.Ordinal);
    }

    /// <summary>未登入不能加入購物車。</summary>
    [Fact(DisplayName = "FR-19 anonymous add redirects")]
    public async Task AnonymousAddDoesNotChangeStock()
    {
        var id = await ProductIdAsync("Demo Laptop");
        var before = await StockAsync("Demo Laptop");
        var client = _app.CreateNoRedirectClient();
        var token = await TokenAsync(client, "/");
        var response = await PostAsync(client, "/cart", token, new Dictionary<string, string> { ["productId"] = id.ToString(CultureInfo.InvariantCulture), ["quantity"] = "1" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Equal(before, await StockAsync("Demo Laptop"));
    }

    /// <summary>加入、合併與拒絕。</summary>
    [Fact(DisplayName = "FR-20 FR-21 add and merge")]
    public async Task AddAndMerge()
    {
        var name = await CreateProductAsync("合併商品", 100m, 4);
        var id = await ProductIdAsync(name);
        var client = await LoginAsync("demo_customer");
        var token = await TokenAsync(client, "/products/" + id.ToString(CultureInfo.InvariantCulture));
        var first = await PostAsync(client, "/cart", token, new Dictionary<string, string> { ["productId"] = id.ToString(CultureInfo.InvariantCulture), ["quantity"] = "2" });
        Assert.Equal("/cart", first.Headers.Location?.OriginalString);
        Assert.Equal(4, await StockAsync(name));
        var cart = await client.GetStringAsync("/cart");
        Assert.Contains(name, cart, StringComparison.Ordinal);
        token = await TokenAsync(client, "/products/" + id.ToString(CultureInfo.InvariantCulture));
        var reject = await PostAsync(client, "/cart", token, new Dictionary<string, string> { ["productId"] = id.ToString(CultureInfo.InvariantCulture), ["quantity"] = "3" });
        Assert.Contains("/products/" + id.ToString(CultureInfo.InvariantCulture), reject.Headers.Location?.OriginalString, StringComparison.Ordinal);
        await ClearCartAsync("demo_customer");
        await DeleteProductAsync(name);
    }

    /// <summary>商品詳情與不存在的商品。</summary>
    [Fact(DisplayName = "FR-22 FR-23 FR-24 product detail")]
    public async Task ProductDetail()
    {
        var id = await ProductIdAsync("Demo Laptop");
        var client = _app.CreateNoRedirectClient();
        var html = await client.GetStringAsync("/products/" + id.ToString(CultureInfo.InvariantCulture));
        Assert.Contains("alt=\"Demo Laptop\"", html, StringComparison.Ordinal);
        Assert.Contains("32999.00", html, StringComparison.Ordinal);
        Assert.Contains("Computer", html, StringComparison.Ordinal);
        Assert.Contains("Demo Seller", html, StringComparison.Ordinal);
        var missing = await client.GetAsync("/products/999999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("找不到頁面", await missing.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>購物車頁、改數量與移除。</summary>
    [Fact(DisplayName = "FR-30 FR-31 FR-32 FR-33 cart page")]
    public async Task CartPageFlow()
    {
        var anonymous = await _app.CreateNoRedirectClient().GetAsync("/cart");
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Contains("/account/login", anonymous.Headers.Location?.OriginalString, StringComparison.Ordinal);
        var checkout = await _app.CreateNoRedirectClient().GetAsync("/checkout");
        Assert.Contains("/account/login", checkout.Headers.Location?.OriginalString, StringComparison.Ordinal);
        var client = await LoginAsync("demo_customer");
        await ClearCartAsync("demo_customer");
        var empty = await client.GetStringAsync("/cart");
        Assert.Contains("購物車是空的", empty, StringComparison.Ordinal);
        Assert.Contains("回目錄", empty, StringComparison.Ordinal);
        Assert.DoesNotContain("結帳", empty, StringComparison.Ordinal);
        var name = await CreateProductAsync("改價商品", 50m, 6);
        var id = await ProductIdAsync(name);
        await AddAsync(client, id, 1);
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
            var product = await db.Products.FirstAsync(item => item.Id == id);
            product.Price = 80m;
            await db.SaveChangesAsync();
        }

        var token = await TokenAsync(client, "/cart");
        var updated = await PostAsync(client, "/cart/" + id.ToString(CultureInfo.InvariantCulture), token, new Dictionary<string, string> { ["action"] = "update", ["quantity"] = "2" });
        Assert.Equal(HttpStatusCode.Redirect, updated.StatusCode);
        var page = await client.GetStringAsync("/cart");
        Assert.Contains("160.00", page, StringComparison.Ordinal);
        token = await TokenAsync(client, "/cart");
        await PostAsync(client, "/cart/" + id.ToString(CultureInfo.InvariantCulture), token, new Dictionary<string, string> { ["action"] = "remove", ["quantity"] = "1" });
        Assert.Contains("購物車是空的", await client.GetStringAsync("/cart"), StringComparison.Ordinal);
        await DeleteProductAsync(name);
    }

    /// <summary>結帳成功、快照、庫存不足與空購物車。</summary>
    [Fact(DisplayName = "FR-34 FR-35 FR-36 FR-37 FR-38 checkout")]
    public async Task CheckoutFlow()
    {
        var client = await LoginAsync("demo_customer");
        await ClearCartAsync("demo_customer");
        var emptyPost = await PostAsync(client, "/checkout", await TokenAsync(client, "/account"), new Dictionary<string, string>());
        Assert.Equal("/cart", emptyPost.Headers.Location?.OriginalString);
        var name = await CreateProductAsync("結帳商品", 25m, 3);
        var id = await ProductIdAsync(name);
        await AddAsync(client, id, 2);
        var page = await client.GetStringAsync("/checkout");
        Assert.Contains("沒有線上付款", page, StringComparison.Ordinal);
        Assert.Contains(name, page, StringComparison.Ordinal);
        Assert.Contains(DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), page, StringComparison.Ordinal);
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
            var product = await db.Products.FirstAsync(item => item.Id == id);
            product.Stock = 1;
            await db.SaveChangesAsync();
        }

        var denied = await PostAsync(client, "/checkout", await TokenAsync(client, "/checkout"), new Dictionary<string, string>());
        var deniedBody = await denied.Content.ReadAsStringAsync();
        Assert.Contains("庫存不足", deniedBody, StringComparison.Ordinal);
        Assert.Contains(name, deniedBody, StringComparison.Ordinal);
        Assert.Equal(1, await StockAsync(name));
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
            var product = await db.Products.FirstAsync(item => item.Id == id);
            product.Stock = 3;
            await db.SaveChangesAsync();
        }

        var placed = await PostAsync(client, "/checkout", await TokenAsync(client, "/checkout"), new Dictionary<string, string>());
        var placedBody = await placed.Content.ReadAsStringAsync();
        Assert.Contains("訂單已成立", placedBody, StringComparison.Ordinal);
        var number = Regex.Match(placedBody, "V[0-9A-HJKMNP-TV-Z]{26}").Value;
        Assert.Equal(27, number.Length);
        Assert.Equal(1, await StockAsync(name));
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
            var product = await db.Products.FirstAsync(item => item.Id == id);
            product.Price = 99m;
            await db.SaveChangesAsync();
        }

        var account = await client.GetStringAsync("/account");
        Assert.Contains(number, account, StringComparison.Ordinal);
        Assert.Contains("50.00", account, StringComparison.Ordinal);
        await DeleteProductAsync(name);
    }

    /// <summary>同時購買最後一件時只有一筆成功。</summary>
    [Fact(DisplayName = "FR-39 one winner")]
    public async Task ConcurrentLastItem()
    {
        var name = await CreateProductAsync("最後一件", 10m, 1);
        var productId = await ProductIdAsync(name);
        var first = await RegisterAsync("racea");
        var second = await RegisterAsync("raceb");
        await AddForAsync(first, productId, 1);
        await AddForAsync(second, productId, 1);
        var left = PlaceAsync(first);
        var right = PlaceAsync(second);
        var results = await Task.WhenAll(left, right);
        Assert.Equal(1, results.Count(item => item is CheckoutResult.Placed));
        Assert.Equal(0, await StockAsync(name));
        await DeleteProductAsync(name);
    }

    /// <summary>訂單 PDF 與別人不能下載。</summary>
    [Fact(DisplayName = "FR-40 FR-41 FR-42 invoice")]
    public async Task InvoiceDownload()
    {
        var client = await LoginAsync("demo_customer");
        await ClearCartAsync("demo_customer");
        var name = await CreateProductAsync("發票商品", 15m, 2);
        var id = await ProductIdAsync(name);
        await AddAsync(client, id, 1);
        var placed = await PostAsync(client, "/checkout", await TokenAsync(client, "/checkout"), new Dictionary<string, string>());
        var number = Regex.Match(await placed.Content.ReadAsStringAsync(), "V[0-9A-HJKMNP-TV-Z]{26}").Value;
        var pdf = await client.GetAsync("/account/orders/" + number + "/invoice");
        Assert.Equal("OrderInvoice.pdf", pdf.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes[..4]), StringComparison.Ordinal);
        var other = await LoginAsync("demo_seller");
        var missing = await other.GetAsync("/account/orders/" + number + "/invoice");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var seller = await LoginAsync("demo_seller");
        var deleteToken = await TokenAsync(seller, "/account");
        await PostAsync(seller, "/seller/products/" + id.ToString(CultureInfo.InvariantCulture), deleteToken, new Dictionary<string, string> { ["action"] = "delete", ["confirm"] = "true" });
        var history = await client.GetStringAsync("/account");
        Assert.Contains(name, history, StringComparison.Ordinal);
    }

    /// <summary>註冊驗證與成功後只是會員。</summary>
    [Fact(DisplayName = "FR-50 FR-51 FR-52 FR-53 FR-54 FR-55 FR-56 FR-57 FR-58 register")]
    public async Task RegisterFlow()
    {
        var client = _app.CreateNoRedirectClient();
        var blank = await PostAsync(client, "/account/register", await TokenAsync(client, "/account/register"), new Dictionary<string, string>());
        var blankBody = await blank.Content.ReadAsStringAsync();
        Assert.Contains("必填", blankBody, StringComparison.Ordinal);
        var weak = Fields(NextUser(), NextPhone());
        weak["Password"] = "password";
        weak["ConfirmPassword"] = "password";
        Assert.Contains("密碼", await (await PostAsync(client, "/account/register", await TokenAsync(client, "/account/register"), weak)).Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var mismatch = Fields(NextUser(), NextPhone());
        mismatch["ConfirmPassword"] = "Other@1234";
        Assert.Contains("確認密碼不一致", await (await PostAsync(client, "/account/register", await TokenAsync(client, "/account/register"), mismatch)).Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var phone = Fields(NextUser(), "123");
        Assert.Contains("10 碼", await (await PostAsync(client, "/account/register", await TokenAsync(client, "/account/register"), phone)).Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var future = Fields(NextUser(), NextPhone());
        future["BirthDate"] = "2999-01-01";
        Assert.Contains("未來", await (await PostAsync(client, "/account/register", await TokenAsync(client, "/account/register"), future)).Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var user = NextUser();
        var fields = Fields(user, NextPhone(), "陳小明");
        fields["Email"] = user + "@example.museum";
        var created = await PostAsync(client, "/account/register", await TokenAsync(client, "/account/register"), fields);
        Assert.Equal("/account/login", created.Headers.Location?.OriginalString);
        var signed = await LoginAsync(user, "Test@1234");
        var home = await signed.GetStringAsync("/");
        Assert.DoesNotContain("上架商品", home, StringComparison.Ordinal);
        var revisit = await signed.GetAsync("/account/register");
        Assert.Equal("/", revisit.Headers.Location?.OriginalString);
        var duplicate = Fields(user, NextPhone());
        var another = _app.CreateNoRedirectClient();
        var duplicateResponse = await PostAsync(another, "/account/register", await TokenAsync(another, "/account/register"), duplicate);
        Assert.Contains("已被使用", await duplicateResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>登入、鎖定與登出。</summary>
    [Fact(DisplayName = "FR-60 FR-61 FR-62 login lockout logout")]
    public async Task LoginLockoutAndLogout()
    {
        var user = await RegisterAsync("lockme");
        var client = _app.CreateNoRedirectClient();
        var byName = await PostAsync(client, "/account/login", await TokenAsync(client, "/account/login"), new Dictionary<string, string> { ["account"] = user, ["password"] = "Test@1234", ["returnUrl"] = "/cart" });
        Assert.Equal("/cart", byName.Headers.Location?.OriginalString);
        var byEmail = _app.CreateNoRedirectClient();
        var email = await PostAsync(byEmail, "/account/login", await TokenAsync(byEmail, "/account/login"), new Dictionary<string, string> { ["account"] = user + "@example.com", ["password"] = "Test@1234", ["returnUrl"] = "/" });
        Assert.Equal("/", email.Headers.Location?.OriginalString);
        var bad = _app.CreateNoRedirectClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await PostAsync(bad, "/account/login", await TokenAsync(bad, "/account/login"), new Dictionary<string, string> { ["account"] = user, ["password"] = "Wrong@123" });
            Assert.Contains("不正確", await failed.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        var locked = await PostAsync(bad, "/account/login", await TokenAsync(bad, "/account/login"), new Dictionary<string, string> { ["account"] = user, ["password"] = "Test@1234" });
        Assert.Contains("不正確", await locked.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await Task.Delay(TimeSpan.FromSeconds(3));
        var again = await PostAsync(bad, "/account/login", await TokenAsync(bad, "/account/login"), new Dictionary<string, string> { ["account"] = user, ["password"] = "Test@1234", ["returnUrl"] = "/" });
        Assert.Equal("/", again.Headers.Location?.OriginalString);
        var logout = await PostAsync(bad, "/account/logout", await TokenAsync(bad, "/"), new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Contains("登入", await bad.GetStringAsync("/"), StringComparison.Ordinal);
        var getLogout = await bad.GetAsync("/account/logout");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, getLogout.StatusCode);
    }

    /// <summary>忘記密碼的文案、配額、失效與重設。</summary>
    [Fact(DisplayName = "FR-63 FR-64 FR-65 FR-66 FR-67 FR-68 FR-69 password reset")]
    public async Task PasswordResetFlow()
    {
        var user = await RegisterAsync("resetme");
        var client = _app.CreateNoRedirectClient();
        var missing = await PostAsync(client, "/account/forgot", await TokenAsync(client, "/account/forgot"), new Dictionary<string, string> { ["account"] = "nobody" });
        var missingText = await missing.Content.ReadAsStringAsync();
        var present = await PostAsync(client, "/account/forgot", await TokenAsync(client, "/account/forgot"), new Dictionary<string, string> { ["account"] = user });
        var presentText = await present.Content.ReadAsStringAsync();
        Assert.Contains("若帳號存在", missingText, StringComparison.Ordinal);
        Assert.Contains("若帳號存在", presentText, StringComparison.Ordinal);
        string? link;
        using (var scope = _app.Services.CreateScope())
        {
            var mailbox = scope.ServiceProvider.GetRequiredService<MemoryMailbox>();
            link = mailbox.Messages.Last(item => item.To == user + "@example.com").Body;
        }

        Assert.Contains("/account/reset?user=", link, StringComparison.Ordinal);
        var uri = new Uri(link);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        var reset = _app.CreateNoRedirectClient();
        var done = await PostAsync(reset, "/account/reset", await TokenAsync(reset, "/account/reset?user=" + query["user"] + "&token=" + query["token"]), new Dictionary<string, string>
        {
            ["user"] = query["user"].ToString(),
            ["token"] = query["token"].ToString(),
            ["password"] = "New@12345",
            ["confirmPassword"] = "New@12345",
        });
        Assert.Contains("密碼已更新", await done.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var oldClient = _app.CreateNoRedirectClient();
        var oldPassword = await PostAsync(oldClient, "/account/login", await TokenAsync(oldClient, "/account/login"), new Dictionary<string, string> { ["account"] = user, ["password"] = "Test@1234" });
        Assert.Contains("不正確", await oldPassword.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var logged = await LoginAsync(user, "New@12345");
        Assert.Contains("帳戶", await logged.GetStringAsync("/"), StringComparison.Ordinal);
        await logged.GetAsync("/");
        var reuseClient = _app.CreateNoRedirectClient();
        var reuse = await PostAsync(reuseClient, "/account/reset", await TokenAsync(reuseClient, "/account/reset"), new Dictionary<string, string>
        {
            ["user"] = query["user"].ToString(),
            ["token"] = query["token"].ToString(),
            ["password"] = "Other@1234",
            ["confirmPassword"] = "Other@1234",
        });
        Assert.Contains("重設失敗", await reuse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var emptyClient = _app.CreateNoRedirectClient();
        var emptyToken = await PostAsync(emptyClient, "/account/reset", await TokenAsync(emptyClient, "/account/reset"), new Dictionary<string, string> { ["password"] = "Other@1234", ["confirmPassword"] = "Other@1234" });
        Assert.Contains("重設失敗", await emptyToken.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>個人資料只能改地址相關欄位。</summary>
    [Fact(DisplayName = "FR-70 FR-71 FR-72 profile")]
    public async Task ProfileFlow()
    {
        var user = await RegisterAsync("profileu");
        var client = await LoginAsync(user, "Test@1234");
        var page = await client.GetStringAsync("/account");
        Assert.Contains("還沒有訂單", page, StringComparison.Ordinal);
        Assert.Contains(user, page, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"DisplayName\"", page, StringComparison.Ordinal);
        var phone = NextPhone();
        var saved = await PostAsync(client, "/account", await TokenAsync(client, "/account"), new Dictionary<string, string>
        {
            ["Phone"] = phone,
            ["Address"] = "New address",
            ["Country"] = "Japan",
            ["Region"] = "Osaka",
            ["City"] = "Osaka",
            ["DisplayName"] = "Hacker",
            ["Email"] = "hacker@example.com",
            ["UserName"] = "hacker",
        });
        var body = await saved.Content.ReadAsStringAsync();
        Assert.Contains("New address", body, StringComparison.Ordinal);
        Assert.Contains("測試會員", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Hacker", body, StringComparison.Ordinal);
        var bad = await PostAsync(client, "/account", await TokenAsync(client, "/account"), new Dictionary<string, string> { ["Phone"] = "12", ["Address"] = "New address", ["Country"] = "Japan", ["Region"] = "Osaka", ["City"] = "Osaka" });
        Assert.Contains("10 碼", await bad.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>上架、圖片、修改與刪除。</summary>
    [Fact(DisplayName = "FR-80 FR-81 FR-82 FR-84 FR-85 FR-86 FR-87 FR-88 seller products")]
    public async Task SellerProducts()
    {
        var customer = await LoginAsync("demo_customer");
        var denied = await customer.GetAsync("/seller/products/new");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("沒有權限", await denied.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var anonymous = await _app.CreateNoRedirectClient().GetAsync("/seller/products/new");
        Assert.Contains("/account/login", anonymous.Headers.Location?.OriginalString, StringComparison.Ordinal);
        var seller = await LoginAsync("demo_seller");
        var name = "上架" + Guid.NewGuid().ToString("N")[..6];
        var form = new Dictionary<string, string> { ["Name"] = name, ["Price"] = "12.50", ["CategoryId"] = (await CategoryIdAsync()).ToString(CultureInfo.InvariantCulture), ["Keywords"] = "demo" };
        var created = await UploadAsync(seller, form, "photo.png", Png());
        Assert.Equal("/account", created.Headers.Location?.OriginalString);
        var account = await seller.GetStringAsync("/account");
        Assert.Contains(name, account, StringComparison.Ordinal);
        var catalog = await _app.CreateNoRedirectClient().GetStringAsync("/?keyword=" + name);
        Assert.Contains("已售完", catalog, StringComparison.Ordinal);
        var id = await ProductIdAsync(name);
        var badExt = await UploadAsync(seller, form, "photo.txt", Png());
        Assert.Contains("圖片", await badExt.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var huge = new byte[(2 * 1024 * 1024) + 1];
        Png().CopyTo(huge, 0);
        var tooBig = await UploadAsync(seller, new Dictionary<string, string>(form) { ["Name"] = name + "b" }, "big.png", huge);
        Assert.Contains("圖片", await tooBig.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var updated = await PostAsync(seller, "/seller/products/" + id.ToString(CultureInfo.InvariantCulture), await TokenAsync(seller, "/account"), new Dictionary<string, string> { ["action"] = "update", ["price"] = "20.00", ["stock"] = "2", ["keywords"] = "ready" });
        Assert.Equal(HttpStatusCode.Redirect, updated.StatusCode);
        Assert.Equal(2, await StockAsync(name));
        var rejected = await PostAsync(seller, "/seller/products/" + id.ToString(CultureInfo.InvariantCulture), await TokenAsync(seller, "/account"), new Dictionary<string, string> { ["action"] = "update", ["price"] = "-1", ["stock"] = "2", ["keywords"] = "ready" });
        Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        Assert.Equal(2, await StockAsync(name));
        var other = await LoginAsync("demo_operator");
        var foreign = await PostAsync(other, "/seller/products/" + id.ToString(CultureInfo.InvariantCulture), await TokenAsync(other, "/account"), new Dictionary<string, string> { ["action"] = "update", ["price"] = "1", ["stock"] = "1", ["keywords"] = "x" });
        Assert.True(foreign.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
        var removed = await PostAsync(seller, "/seller/products/" + id.ToString(CultureInfo.InvariantCulture), await TokenAsync(seller, "/account"), new Dictionary<string, string> { ["action"] = "delete", ["confirm"] = "true" });
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.DoesNotContain(name, await seller.GetStringAsync("/account"), StringComparison.Ordinal);
    }

    /// <summary>管理者授予與收回賣家。</summary>
    [Fact(DisplayName = "FR-89 FR-90 FR-91 FR-92 operator")]
    public async Task OperatorFlow()
    {
        var user = await RegisterAsync("grantme");
        var member = await LoginAsync(user, "Test@1234");
        var blocked = await member.GetAsync("/operator/sellers");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        var op = await LoginAsync("demo_operator");
        var missing = await op.GetStringAsync("/operator/sellers?query=no-such-user");
        Assert.Contains("找不到會員", missing, StringComparison.Ordinal);
        var found = await op.GetStringAsync("/operator/sellers?query=" + user);
        Assert.Contains(user, found, StringComparison.Ordinal);
        var byEmail = await op.GetStringAsync("/operator/sellers?query=" + user + "@example.com");
        Assert.Contains(user, byEmail, StringComparison.Ordinal);
        var token = await TokenAsync(op, "/operator/sellers?query=" + user);
        await PostAsync(op, "/operator/sellers", token, new Dictionary<string, string> { ["targetId"] = await UserIdAsync(user), ["role"] = "Seller", ["action"] = "grant", ["query"] = user });
        var refreshed = await LoginAsync(user, "Test@1234");
        Assert.Contains("href=\"/seller/products/new\"", await refreshed.GetStringAsync("/"), StringComparison.Ordinal);
        token = await TokenAsync(op, "/operator/sellers?query=" + user);
        await PostAsync(op, "/operator/sellers", token, new Dictionary<string, string> { ["targetId"] = await UserIdAsync(user), ["role"] = "Seller", ["action"] = "revoke", ["query"] = user });
        var after = await member.GetAsync("/seller/products/new");
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
        token = await TokenAsync(op, "/operator/sellers?query=demo_operator");
        var rejected = await PostAsync(op, "/operator/sellers", token, new Dictionary<string, string> { ["targetId"] = await UserIdAsync("demo_operator"), ["role"] = "Operator", ["action"] = "revoke", ["query"] = "demo_operator" });
        Assert.Contains("不能變更管理者", await rejected.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Contains("賣家資格", await op.GetStringAsync("/"), StringComparison.Ordinal);
    }

    /// <summary>關於、部落格與聯絡我們。</summary>
    [Fact(DisplayName = "FR-100 FR-101 FR-102 about blog contact")]
    public async Task AboutBlogContact()
    {
        var client = _app.CreateNoRedirectClient();
        Assert.Contains("關於我們", await client.GetStringAsync("/about"), StringComparison.Ordinal);
        Assert.Contains("部落格", await client.GetStringAsync("/blog"), StringComparison.Ordinal);
        var token = await TokenAsync(client, "/");
        await PostAsync(client, "/culture", token, new Dictionary<string, string> { ["culture"] = "en", ["returnUrl"] = "/about" });
        Assert.Contains("About", await client.GetStringAsync("/about"), StringComparison.Ordinal);
        Assert.Contains("Blog", await client.GetStringAsync("/blog"), StringComparison.Ordinal);
        var bad = await PostAsync(client, "/contact", await TokenAsync(client, "/contact"), new Dictionary<string, string> { ["name"] = "Ann", ["email"] = "bad", ["message"] = "hello" });
        Assert.Contains("Email", await bad.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var sent = await PostAsync(client, "/contact", await TokenAsync(client, "/contact"), new Dictionary<string, string> { ["name"] = "Ann", ["email"] = "ann@example.com", ["message"] = "hello" });
        Assert.Contains("received", (await sent.Content.ReadAsStringAsync()).ToLowerInvariant(), StringComparison.Ordinal);
    }

    /// <summary>錯誤頁、安全標頭、Cookie 與防偽權杖。</summary>
    [Fact(DisplayName = "FR-103 FR-104 FR-105 FR-107 FR-108 errors and headers")]
    public async Task ErrorsAndHeaders()
    {
        var client = _app.CreateNoRedirectClient();
        var missing = await client.GetAsync("/no-such-page");
        var body = await missing.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("找不到頁面", body, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("at Shopping", body, StringComparison.Ordinal);
        var fault = await client.GetAsync("/test/fault");
        var faultBody = await fault.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, fault.StatusCode);
        Assert.Contains("發生問題", faultBody, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", faultBody, StringComparison.Ordinal);
        Assert.DoesNotContain("測試用例外", faultBody, StringComparison.Ordinal);
        Assert.Equal("DENY", missing.Headers.GetValues("X-Frame-Options").First());
        Assert.Equal("nosniff", missing.Headers.GetValues("X-Content-Type-Options").First());
        var csp = missing.Headers.GetValues("Content-Security-Policy").First();
        Assert.DoesNotContain("unsafe-inline", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-eval", csp, StringComparison.Ordinal);
        Assert.False(missing.Headers.Contains("Strict-Transport-Security"));
        var id = await ProductIdAsync("Demo Laptop");
        var before = await StockAsync("Demo Laptop");
        var member = await LoginAsync("demo_customer");
        var forged = await member.PostAsync("/cart", new FormUrlEncodedContent(new Dictionary<string, string> { ["productId"] = id.ToString(CultureInfo.InvariantCulture), ["quantity"] = "1" }));
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Equal(before, await StockAsync("Demo Laptop"));
        var fresh = _app.CreateNoRedirectClient();
        var login = await PostAsync(fresh, "/account/login", await TokenAsync(fresh, "/account/login"), new Dictionary<string, string> { ["account"] = "demo_customer", ["password"] = "Demo@1234", ["returnUrl"] = "/" });
        var setCookie = string.Join("\n", login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>正式環境有 HSTS，且沒有測試例外點。</summary>
    [Fact(DisplayName = "FR-106 hsts only in production")]
    public async Task ProductionHsts()
    {
        var snapshot = AppSettings.RequiredVariables.ToDictionary(name => name, Environment.GetEnvironmentVariable, StringComparer.Ordinal);
        try
        {
            Environment.SetEnvironmentVariable("SQL_SERVER", "127.0.0.1,14333");
            Environment.SetEnvironmentVariable("SQL_DATABASE", "ShoppingTests");
            Environment.SetEnvironmentVariable("SQL_USER", "shopping_app");
            Environment.SetEnvironmentVariable("SQL_PASSWORD", "Test_only_9xQ");
            Environment.SetEnvironmentVariable("SQL_ENCRYPT", "False");
            Environment.SetEnvironmentVariable("SQL_MIGRATOR_USER", "shopping_migrator");
            Environment.SetEnvironmentVariable("SQL_MIGRATOR_PASSWORD", "Test_only_9xQ");
            Environment.SetEnvironmentVariable("SMTP_HOST", "127.0.0.1");
            Environment.SetEnvironmentVariable("SMTP_PORT", "1");
            Environment.SetEnvironmentVariable("SMTP_USER", "user");
            Environment.SetEnvironmentVariable("SMTP_PASSWORD", "secret");
            Environment.SetEnvironmentVariable("SMTP_FROM", "shop@example.com");
            Environment.SetEnvironmentVariable("STORAGE_BLOB_ENDPOINT", "http://127.0.0.1:10000/devstoreaccount1");
            Environment.SetEnvironmentVariable("STORAGE_CONTAINER", "products");
            Environment.SetEnvironmentVariable("IMAGE_BASE_URL", "https://images.example");
            Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", "client");
            await using var production = new ProductionApp();
            var client = production.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
            var response = await client.GetAsync("/");
            Assert.True(response.Headers.Contains("Strict-Transport-Security"));
            var fault = await client.GetAsync("/test/fault");
            Assert.Equal(HttpStatusCode.NotFound, fault.StatusCode);
            var mailer = production.Services.GetRequiredService<IShopMailer>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => mailer.SendAsync("person@example.com", "subject", "body", CancellationToken.None));
        }
        finally
        {
            foreach (var pair in snapshot)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }
    }

    /// <summary>開發環境使用詳細錯誤頁。</summary>
    [Fact]
    public async Task DevelopmentShowsTheException()
    {
        await using var app = new DevelopmentApp();
        var client = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/test/fault");
        var body = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("測試用例外", body, StringComparison.Ordinal);
    }

    /// <summary>Migration、圖片與種子的剩餘分支。</summary>
    [Fact]
    public async Task DataBranches()
    {
        ShopApp.EnsureDatabase();
        var scripts = DatabaseMigrator.LoadFromAssembly(typeof(DatabaseMigrator).Assembly);
        Assert.Contains(scripts, script => script.Name.Contains("0003", StringComparison.Ordinal));
        var probe = "zz_" + Guid.NewGuid().ToString("N") + ".sql";
        var migrator = new DatabaseMigrator(ShopApp.MigratorConnectionString, [new SqlScript(probe, "SELECT 1;")]);
        await migrator.ApplyAsync();
        await migrator.ApplyAsync();
        await using (var hold = new Microsoft.Data.SqlClient.SqlConnection(ShopApp.MigratorConnectionString + ";Pooling=False"))
        {
            await hold.OpenAsync();
            await using var command = hold.CreateCommand();
            command.CommandText = "DECLARE @result int; EXEC @result = sp_getapplock @Resource = N'shop_migrations', @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 1000; SELECT @result;";
            try
            {
                Assert.True(Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) >= 0);
                var blocked = new DatabaseMigrator(ShopApp.MigratorConnectionString, [new SqlScript(probe, "SELECT 1;")], 0);
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => blocked.ApplyAsync());
                Assert.Contains("無法取得 migration 鎖", error.Message, StringComparison.Ordinal);
            }
            finally
            {
                await using var release = hold.CreateCommand();
                release.CommandText = "EXEC sp_releaseapplock @Resource = N'shop_migrations', @LockOwner = 'Session';";
                await release.ExecuteNonQueryAsync();
            }
        }

        Assert.Throws<InvalidOperationException>(() => DatabaseMigrator.LoadFromAssembly(typeof(string).Assembly));
        Assert.Throws<InvalidOperationException>(() => DatabaseMigrator.LoadFromAssembly(typeof(FlowTests).Assembly));
        var store = ImageStore.CreateClient("http://127.0.0.1:10000/devstoreaccount1", "products", null);
        var local = ImageStore.CreateClient("http://localhost:10000/devstoreaccount1", "products", null);
        var remote = ImageStore.CreateClient("https://example.blob.core.windows.net", "products", "client");
        Assert.NotNull(remote);
        var images = new ImageStore(store, "https://cdn.example/");
        await images.EnsureContainerAsync();
        var url = await images.UploadAsync(new MemoryStream(Png()), "demo_seller", ".PNG");
        Assert.Contains("/products/demo_seller/", url, StringComparison.Ordinal);
        Assert.EndsWith(".png", url, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => images.UploadAsync(new MemoryStream(Png()), "demo_seller", ".txt"));
        var oversized = new byte[(2 * 1024 * 1024) + 1];
        Png().CopyTo(oversized, 0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => images.UploadAsync(new MemoryStream(oversized), "demo_seller", ".png"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => images.UploadAsync(new MemoryStream([1, 2, 3, 4]), "demo_seller", ".png"));
        await new ImageStore(local, "").EnsureContainerAsync();
        using var scope = _app.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
        await seeder.SeedAsync();
        var db = scope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
        var hash = await db.Users.Where(user => user.UserName == "demo_customer").Select(user => user.PasswordHash).FirstAsync();
        Assert.DoesNotContain("Demo@1234", hash, StringComparison.Ordinal);
        var arcade = await db.Products.FirstAsync(item => item.Name == "Demo Arcade Machine");
        db.Products.Remove(arcade);
        await db.SaveChangesAsync();
        await seeder.SeedAsync();
        Assert.True(await db.Products.AnyAsync(item => item.Name == "Demo Arcade Machine"));
    }

    private async Task<HttpClient> LoginAsync(string account, string password = "Demo@1234")
    {
        var client = _app.CreateNoRedirectClient();
        var response = await PostAsync(client, "/account/login", await TokenAsync(client, "/account/login"), new Dictionary<string, string> { ["account"] = account, ["password"] = password, ["returnUrl"] = "/" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private async Task<string> RegisterAsync(string prefix)
    {
        var user = prefix + Guid.NewGuid().ToString("N")[..6];
        var client = _app.CreateNoRedirectClient();
        var response = await PostAsync(client, "/account/register", await TokenAsync(client, "/account/register"), Fields(user, NextPhone()));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return user;
    }

    private static async Task<string> TokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + " " + html);
        }

        var match = Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"");
        Assert.True(match.Success, html);
        return match.Groups[1].Value;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string token, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = token;
        return client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    private async Task AddAsync(HttpClient client, int id, int quantity)
    {
        var token = await TokenAsync(client, "/");
        var response = await PostAsync(client, "/cart", token, new Dictionary<string, string> { ["productId"] = id.ToString(CultureInfo.InvariantCulture), ["quantity"] = quantity.ToString(CultureInfo.InvariantCulture) });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private async Task AddForAsync(string userName, int productId, int quantity)
    {
        using var scope = _app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var cart = scope.ServiceProvider.GetRequiredService<CartService>();
        var user = (await users.FindByNameAsync(userName))!;
        Assert.Equal(CartAddStatus.Ok, await cart.AddAsync(user.Id, productId, quantity, CancellationToken.None));
    }

    private async Task<CheckoutResult> PlaceAsync(string userName)
    {
        using var scope = _app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByNameAsync(userName))!;
        var checkout = scope.ServiceProvider.GetRequiredService<CheckoutService>();
        return await checkout.PlaceAsync(user.Id, CancellationToken.None);
    }

    private async Task<int> ProductIdAsync(string name)
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
        return await db.Products.Where(item => item.Name == name).Select(item => item.Id).FirstAsync();
    }

    private async Task<int> StockAsync(string name)
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
        return await db.Products.Where(item => item.Name == name).Select(item => item.Stock).FirstAsync();
    }

    private async Task<int> CategoryIdAsync()
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
        return await db.Categories.Select(item => item.Id).FirstAsync();
    }

    private async Task<string> UserIdAsync(string name)
    {
        using var scope = _app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (await users.FindByNameAsync(name))!.Id;
    }

    private async Task ClearCartAsync(string name)
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByNameAsync(name))!;
        var lines = await db.CartLines.Where(item => item.UserId == user.Id).ToListAsync();
        db.CartLines.RemoveRange(lines);
        await db.SaveChangesAsync();
    }

    private async Task<string> CreateProductAsync(string name, decimal price, int stock)
    {
        name += Guid.NewGuid().ToString("N")[..6];
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
        var seller = await db.Users.FirstAsync(item => item.UserName == "demo_seller");
        var category = await db.Categories.FirstAsync();
        db.Products.Add(new Product
        {
            Name = name,
            Price = price,
            Stock = stock,
            CategoryId = category.Id,
            SellerId = seller.Id,
            ImageUrl = "/images/products/demo_seller/laptop.png",
            Keywords = "demo",
        });
        await db.SaveChangesAsync();
        return name;
    }

    private async Task DeleteProductAsync(string name)
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
        var product = await db.Products.FirstOrDefaultAsync(item => item.Name == name);
        if (product is null)
        {
            return;
        }

        var lines = await db.CartLines.Where(item => item.ProductId == product.Id).ToListAsync();
        db.CartLines.RemoveRange(lines);
        db.Products.Remove(product);
        await db.SaveChangesAsync();
    }

    private async Task<HttpResponseMessage> UploadAsync(HttpClient client, Dictionary<string, string> fields, string fileName, byte[] bytes)
    {
        var token = await TokenAsync(client, "/seller/products/new");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(token), "__RequestVerificationToken");
        foreach (var field in fields)
        {
            content.Add(new StringContent(field.Value), field.Key);
        }

        content.Add(new ByteArrayContent(bytes), "image", fileName);
        return await client.PostAsync("/seller/products/new", content);
    }

    private static Dictionary<string, string> Fields(string user, string phone, string display = "測試會員")
    {
        return new Dictionary<string, string>
        {
            ["DisplayName"] = display,
            ["Email"] = user + "@example.com",
            ["UserName"] = user,
            ["Password"] = "Test@1234",
            ["ConfirmPassword"] = "Test@1234",
            ["Phone"] = phone,
            ["BirthDate"] = "1990-01-01",
            ["Gender"] = "Female",
            ["Country"] = "Taiwan",
            ["Region"] = "Taipei",
            ["City"] = "Taipei",
            ["Address"] = "Test address",
        };
    }

    private static string NextUser()
    {
        return "u" + Guid.NewGuid().ToString("N")[..8];
    }

    private static string NextPhone()
    {
        return Interlocked.Increment(ref _phones).ToString(CultureInfo.InvariantCulture);
    }

    private static string Card(string html, string name)
    {
        var nameAt = html.IndexOf(name, StringComparison.Ordinal);
        var start = html.LastIndexOf("<li", nameAt, StringComparison.Ordinal);
        var end = html.IndexOf("</li>", nameAt, StringComparison.Ordinal);
        return html[start..end];
    }

    private static byte[] Png()
    {
        return [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00];
    }

    /// <summary>記下 EF 實際送出的 SQL。</summary>
    private sealed class CommandCapture : IObserver<DiagnosticListener>, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = [];

        /// <summary>這段期間的指令文字。</summary>
        public List<string> Commands { get; } = [];

        /// <summary>開始收聽。</summary>
        public CommandCapture()
        {
            _subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(this));
        }

        /// <inheritdoc />
        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == "Microsoft.EntityFrameworkCore")
            {
                _subscriptions.Add(value.Subscribe(new CommandObserver(this)));
            }
        }

        /// <inheritdoc />
        public void OnError(Exception error)
        {
        }

        /// <inheritdoc />
        public void OnCompleted()
        {
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
        }

        private sealed class CommandObserver : IObserver<KeyValuePair<string, object?>>
        {
            private readonly CommandCapture _owner;

            public CommandObserver(CommandCapture owner)
            {
                _owner = owner;
            }

            public void OnNext(KeyValuePair<string, object?> value)
            {
                if (value.Value is CommandEventData data)
                {
                    _owner.Commands.Add(data.Command.CommandText);
                }
            }

            public void OnError(Exception error)
            {
            }

            public void OnCompleted()
            {
            }
        }
    }
}

/// <summary>正式環境主機。</summary>
public sealed class ProductionApp : ShopApp
{
    /// <summary>建立正式環境主機。</summary>
    public ProductionApp()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(WebHostDefaults.EnvironmentKey, "Production");
        builder.UseEnvironment("Production");
    }
}

/// <summary>開發環境主機，並打開測試例外。</summary>
public sealed class DevelopmentApp : ShopApp
{
    /// <summary>建立開發環境主機。</summary>
    public DevelopmentApp()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");
        builder.UseEnvironment("Development");
        builder.UseSetting("Testing:ExposeFault", "true");
        builder.UseSetting("Sql:Password", "Test_only_9xQ");
        builder.UseSetting("Sql:MigratorPassword", "Test_only_9xQ");
    }
}
