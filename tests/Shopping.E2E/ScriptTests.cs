using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NUnit.Framework;
using UglyToad.PdfPig;
using static Microsoft.Playwright.Assertions;

namespace Shopping.E2E;

/// <summary>計畫第 16.4 節的劇本與版面矩陣。</summary>
[TestFixture]
[NonParallelizable]
[Timeout(240000)]
public sealed class ScriptTests
{
    private static long _phone = 2000000000L + (DateTimeOffset.UtcNow.Ticks % 4000000000L);

    /// <summary>目錄、搜尋、分類、售完與未登入加入。</summary>
    [TestCase(TestName = "FR-01 FR-05 FR-06 FR-08 FR-10 FR-11 FR-12 FR-13 FR-14 FR-15 FR-16 FR-17 FR-18 FR-19 FR-22 FR-23 FR-24 FR-105 catalog")]
    public async Task Catalog()
    {
        await using var context = await SiteHost.OpenAsync(1440);
        var page = await NewPage(context);
        var home = await page.GotoAsync("/");
        AssertSecurity(home!);
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "市集", Exact = true })).ToBeVisibleAsync();
        await Expect(page.Locator("#site-search")).ToBeVisibleAsync();
        await Expect(page.Locator("a[href='/categories']")).ToBeVisibleAsync();
        await Expect(page.Locator("a[href='/about']")).ToBeVisibleAsync();
        await Expect(page.Locator("a[href='/blog']")).ToBeVisibleAsync();
        await Expect(page.Locator("a[href='/contact']")).ToBeVisibleAsync();
        await Expect(page.Locator("a[href='/account/login']")).ToBeVisibleAsync();
        await Expect(page.Locator("a[href='/account/register']")).ToBeVisibleAsync();
        await page.Locator("a[href='#main']").FocusAsync();
        await Expect(page.Locator("a[href='#main']")).ToBeFocusedAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Demo Laptop" })).ToBeVisibleAsync();
        await Expect(page.GetByText("賣家資格")).ToBeVisibleAsync();
        Assert.That(await page.ContentAsync(), Does.Not.Contain("href=\"/seller/products/new\""));
        var first = await Names(page);
        await page.ReloadAsync();
        Assert.That(await Names(page), Is.EquivalentTo(first));
        await page.FillAsync("#site-search", "  LaPtOp ");
        await page.Locator("form[role='search'] button").ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Demo Laptop" })).ToBeVisibleAsync();
        await page.FillAsync("#site-search", "zzzz-no-such");
        await page.Locator("form[role='search'] button").ClickAsync();
        await Expect(page.GetByText("沒有符合的商品。")).ToBeVisibleAsync();
        await page.GotoAsync("/?culture=en&keyword=zzzz-no-such");
        await Expect(page.GetByText("No matching products.")).ToBeVisibleAsync();
        await page.GotoAsync("/?culture=zh-Hant");
        await page.GotoAsync("/categories");
        await page.GetByText("Computer", new() { Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Demo Laptop" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Demo Smart Watch" })).ToHaveCountAsync(0);
        await page.GotoAsync("/?category=999999");
        await Expect(page.GetByText("沒有符合的商品。")).ToBeVisibleAsync();
        await page.GotoAsync("/?keyword=arcade");
        var arcade = page.Locator("li").Filter(new() { HasText = "Demo Arcade Machine" });
        await Expect(arcade.GetByText("已售完")).ToBeVisibleAsync();
        await Expect(arcade.Locator("button")).ToBeDisabledAsync();
        await page.GotoAsync("/?keyword=watch");
        var watch = page.Locator("li").Filter(new() { HasText = "Demo Smart Watch" });
        Assert.That(await watch.Locator("select[name='quantity'] option").CountAsync(), Is.EqualTo(10));
        await watch.Locator("a").First.ClickAsync();
        await Expect(page.Locator("h1")).ToHaveTextAsync("Demo Smart Watch");
        await Expect(page.GetByText("2999.00")).ToBeVisibleAsync();
        await Expect(page.GetByText("Demo Seller")).ToBeVisibleAsync();
        await Expect(page.GetByText("watch smart wearable demo")).ToBeVisibleAsync();
        Assert.That(await page.Locator("img[alt='Demo Smart Watch']").CountAsync(), Is.GreaterThan(0));
        var stockBefore = await page.GetByText("庫存").TextContentAsync();
        await page.Locator("form[action='/cart'] button").ClickAsync();
        await page.WaitForURLAsync("**/account/login**");
        await page.GotoAsync("/?keyword=watch");
        await page.Locator("a:has(h2:text-is(\"Demo Smart Watch\"))").ClickAsync();
        Assert.That(await page.GetByText("庫存").TextContentAsync(), Is.EqualTo(stockBefore));
        var missing = await page.GotoAsync("/products/999999");
        Assert.That(missing!.Status, Is.EqualTo(404));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "找不到頁面" })).ToBeVisibleAsync();
        await using var narrow = await SiteHost.OpenAsync(375);
        var mobile = await NewPage(narrow);
        await mobile.GotoAsync("/");
        await Expect(mobile.Locator("#nav-toggle")).ToBeVisibleAsync();
        await Expect(mobile.Locator("#site-nav")).ToBeHiddenAsync();
        await mobile.Locator("#nav-toggle").ClickAsync();
        await Expect(mobile.Locator("#nav-toggle")).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(mobile.Locator("#site-nav")).ToBeVisibleAsync();
        await mobile.Locator("#site-nav a[href='/categories']").ClickAsync();
        await Expect(mobile.Locator("h1")).ToHaveTextAsync("分類");
        await Expect(mobile.Locator("#site-nav")).ToBeHiddenAsync();
        await mobile.Locator("#nav-toggle").ClickAsync();
        await Expect(mobile.Locator("#site-nav")).ToBeVisibleAsync();
        await mobile.Locator("#nav-toggle").ClickAsync();
        await Expect(mobile.Locator("#site-nav")).ToBeHiddenAsync();
        await using var sellerContext = await SiteHost.OpenAsync(1440);
        var seller = await NewPage(sellerContext);
        await LoginAsync(seller, "demo_seller", "Demo@1234");
        await UpdateProduct(seller, "Demo Smart Watch", "1000.00", null);
        await UpdateProduct(seller, "Demo Laser Printer", "2000.00", null);
        try
        {
            await seller.GotoAsync("/?sort=price-asc");
            var asc = await seller.Locator("main ul li h2").AllTextContentsAsync();
            Assert.That(asc.ToList().IndexOf("Demo Smart Watch"), Is.LessThan(asc.ToList().IndexOf("Demo Laser Printer")));
            await seller.GotoAsync("/?sort=price-desc");
            var desc = await seller.Locator("main ul li h2").AllTextContentsAsync();
            Assert.That(desc.ToList().IndexOf("Demo Laser Printer"), Is.LessThan(desc.ToList().IndexOf("Demo Smart Watch")));
        }
        finally
        {
            await UpdateProduct(seller, "Demo Smart Watch", "2999.00", null);
            await UpdateProduct(seller, "Demo Laser Printer", "8999.00", null);
        }

        await UpdateProduct(seller, "Demo Laser Printer", null, "3");
        try
        {
            await seller.GotoAsync("/?keyword=printer");
            Assert.That(await seller.Locator("li").Filter(new() { HasText = "Demo Laser Printer" }).Locator("select[name='quantity'] option").CountAsync(), Is.EqualTo(3));
        }
        finally
        {
            await UpdateProduct(seller, "Demo Laser Printer", null, "5");
        }
    }

    /// <summary>註冊、購物車、結帳、PDF 與庫存不足。</summary>
    [TestCase(TestName = "FR-02 FR-04 FR-20 FR-21 FR-30 FR-31 FR-32 FR-33 FR-34 FR-35 FR-36 FR-37 FR-38 FR-40 FR-41 FR-55 FR-57 FR-58 FR-62 FR-72 FR-107 checkout")]
    public async Task Checkout()
    {
        var user = UserName();
        var email = user + "@example.com";
        var phone = NextPhone();
        await using var context = await SiteHost.OpenAsync(1440);
        var page = await NewPage(context);
        var anonCart = await page.GotoAsync("/cart");
        AssertSecurity(anonCart!);
        await page.WaitForURLAsync("**/account/login**");
        await page.GotoAsync("/checkout");
        await page.WaitForURLAsync("**/account/login**");
        await RegisterAsync(page, user, email, phone);
        await LoginAsync(page, email, "Test@1234", expectCookie: true);
        await Expect(page.Locator("a[href='/account']")).ToBeVisibleAsync();
        await Expect(page.Locator("form[action='/account/logout']")).ToBeVisibleAsync();
        await Expect(page.Locator("a[href='/account/register']")).ToHaveCountAsync(0);
        await Expect(page.Locator("a[href='/seller/products/new']")).ToHaveCountAsync(0);
        await page.GotoAsync("/account/register");
        await page.WaitForURLAsync(url => !url.Contains("/account/register", StringComparison.Ordinal));
        await page.GotoAsync("/account");
        await Expect(page.GetByText("還沒有訂單。")).ToBeVisibleAsync();
        await Expect(page.GetByText("測試會員")).ToBeVisibleAsync();
        var watchHref = await OpenProduct(page, "Demo Smart Watch");
        var stockText = await page.GetByText("庫存").TextContentAsync();
        await page.Locator("form[action='/cart'] button").ClickAsync();
        await page.WaitForURLAsync("**/cart");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Demo Smart Watch" })).ToBeVisibleAsync();
        await page.GotoAsync(watchHref);
        Assert.That(await page.GetByText("庫存").TextContentAsync(), Is.EqualTo(stockText));
        await page.Locator("form[action='/cart'] button").ClickAsync();
        await page.WaitForURLAsync("**/cart");
        await Expect(page.Locator("input[name='quantity']")).ToHaveValueAsync("2");
        await page.GotoAsync("/");
        await Expect(page.Locator("a[href='/cart']")).ToContainTextAsync("2");
        await page.GotoAsync(watchHref);
        await page.Locator("select[name='quantity']").SelectOptionAsync("10");
        await page.Locator("form[action='/cart'] button").ClickAsync();
        await Expect(page.Locator("h1")).ToHaveTextAsync("Demo Smart Watch");
        await page.GotoAsync("/cart");
        await Expect(page.Locator("input[name='quantity']")).ToHaveValueAsync("2");
        await page.Locator("input[name='quantity']").FillAsync("2");
        await page.Locator("button[value='update']").ClickAsync();
        await Expect(page.GetByText("5998.00").First).ToBeVisibleAsync();
        await page.Locator("button[value='remove']").ClickAsync();
        await Expect(page.GetByText("購物車是空的。")).ToBeVisibleAsync();
        await Expect(page.Locator("a[href='/checkout']")).ToHaveCountAsync(0);
        var token = await Token(page);
        var emptyPost = await page.Context.APIRequest.PostAsync("/checkout", new()
        {
            Form = Fields(page, ("__RequestVerificationToken", token)),
            MaxRedirects = 0,
        });
        Assert.That(emptyPost.Status, Is.EqualTo(302));
        await page.GotoAsync("/account");
        await Expect(page.GetByText("還沒有訂單。")).ToBeVisibleAsync();
        await page.GotoAsync(watchHref);
        await page.Locator("form[action='/cart'] button").ClickAsync();
        await page.WaitForURLAsync("**/cart");
        await page.GotoAsync("/checkout");
        await Expect(page.GetByText("此訂單沒有線上付款。")).ToBeVisibleAsync();
        await Expect(page.GetByText(TaipeiToday())).ToBeVisibleAsync();
        await page.GotoAsync("/checkout?culture=en");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Checkout" })).ToBeVisibleAsync();
        await Expect(page.GetByText("This order has no online payment.")).ToBeVisibleAsync();
        await page.GotoAsync("/checkout?culture=zh-Hant");
        await page.Locator("form[action='/checkout'] button").ClickAsync();
        await Expect(page.GetByText("訂單已成立")).ToBeVisibleAsync();
        var orderNumber = (await page.Locator("main p").Filter(new() { HasText = "V" }).First.TextContentAsync())!.Trim().Split(' ').Last();
        Assert.That(orderNumber, Does.StartWith("V"));
        Assert.That(orderNumber, Has.Length.EqualTo(27));
        await page.GotoAsync("/cart");
        await Expect(page.GetByText("購物車是空的。")).ToBeVisibleAsync();
        await page.GotoAsync("/account");
        await Expect(page.GetByText(orderNumber)).ToBeVisibleAsync();
        var pdf = await ReadPdf(page, orderNumber);
        Assert.That(pdf, Does.Contain("Demo Smart Watch"));
        Assert.That(pdf, Does.Contain("2999.00"));
        await using var other = await SiteHost.OpenAsync(1440);
        var stranger = await NewPage(other);
        await LoginAsync(stranger, "demo_customer", "Demo@1234");
        var denied = await stranger.GotoAsync("/account/orders/" + orderNumber + "/invoice");
        Assert.That(denied!.Status, Is.EqualTo(404));
        await using var sellerContext = await SiteHost.OpenAsync(1440);
        var seller = await NewPage(sellerContext);
        await LoginAsync(seller, "demo_seller", "Demo@1234");
        await UpdateProduct(seller, "Demo Smart Watch", "3000.00", null);
        try
        {
            await page.GotoAsync("/account");
            var again = await ReadPdf(page, orderNumber);
            Assert.That(again, Does.Contain("2999.00"));
            Assert.That(again, Does.Not.Contain("3000.00"));
        }
        finally
        {
            await UpdateProduct(seller, "Demo Smart Watch", "2999.00", null);
        }

        await UpdateProduct(seller, "Demo Laser Printer", null, "1");
        try
        {
            await page.GotoAsync(await OpenProduct(page, "Demo Laser Printer"));
            await page.Locator("form[action='/cart'] button").ClickAsync();
            await page.WaitForURLAsync("**/cart");
            await UpdateProduct(seller, "Demo Laser Printer", null, "0");
            await page.GotoAsync("/checkout");
            await page.Locator("form[action='/checkout'] button").ClickAsync();
            await Expect(page.GetByText("庫存不足")).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Demo Laser Printer" })).ToBeVisibleAsync();
            await page.GotoAsync("/cart");
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Demo Laser Printer" })).ToBeVisibleAsync();
        }
        finally
        {
            await UpdateProduct(seller, "Demo Laser Printer", null, "5");
        }

        await page.Locator("button[value='remove']").ClickAsync();
        await page.GotoAsync(watchHref);
        await page.Locator("form[action='/cart'] button").ClickAsync();
        await page.WaitForURLAsync("**/cart");
        await page.Locator("form[action='/account/logout'] button").ClickAsync();
        await page.WaitForURLAsync("**/");
        await Expect(page.Locator("a[href='/account/login']")).ToBeVisibleAsync();
        await Expect(page.Locator("a[href='/cart']")).ToContainTextAsync("0");
        await LoginAsync(page, user, "Test@1234");
        await page.GotoAsync("/cart");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Demo Smart Watch" })).ToBeVisibleAsync();
        await page.Locator("button[value='remove']").ClickAsync();
    }

    /// <summary>註冊驗證。</summary>
    [TestCase(TestName = "FR-50 FR-51 FR-52 FR-53 FR-54 FR-56 register validation")]
    public async Task RegisterValidation()
    {
        await using var context = await SiteHost.OpenAsync(1440);
        var page = await NewPage(context);
        await page.GotoAsync("/account/register");
        await page.Locator("form[action='/account/register'] button").ClickAsync();
        await Expect(page.GetByText("必填。").First).ToBeVisibleAsync();
        Assert.That(await page.GetByText("必填。").CountAsync(), Is.GreaterThan(5));
        await FillRegister(page, "測試會員", "bad-mail", "gooduser1", "short", "short", "123", "1990-01-01");
        await page.Locator("form[action='/account/register'] button").ClickAsync();
        await Expect(page.GetByText("Email 格式不正確。")).ToBeVisibleAsync();
        await Expect(page.GetByText("密碼至少 8 碼，需含大小寫、數字與特殊字元。")).ToBeVisibleAsync();
        await Expect(page.GetByText("電話須為 10 碼數字。")).ToBeVisibleAsync();
        await FillRegister(page, "測試會員", UserName() + "@example.com", "bad_name", "Test@1234", "Other@1234", NextPhone(), "2999-01-01");
        await page.Locator("form[action='/account/register'] button").ClickAsync();
        await Expect(page.GetByText("使用者名稱只接受英數，最多 20 字。")).ToBeVisibleAsync();
        await Expect(page.GetByText("確認密碼不一致。")).ToBeVisibleAsync();
        await Expect(page.GetByText("生日不能是未來。")).ToBeVisibleAsync();
        var user = UserName();
        var email = user + "@example.email";
        await RegisterAsync(page, user, email, NextPhone());
        await page.GotoAsync("/account/register");
        await FillRegister(page, "測試會員", email, UserName(), "Test@1234", "Test@1234", NextPhone(), "1990-01-01");
        await page.Locator("form[action='/account/register'] button").ClickAsync();
        await Expect(page.GetByText("這個 Email 已被使用。")).ToBeVisibleAsync();
        await using var english = await SiteHost.OpenAsync(1440);
        var en = await NewPage(english);
        await en.GotoAsync("/account/register?culture=en");
        await en.Locator("form[action='/account/register'] button").ClickAsync();
        await Expect(en.GetByText("Required.").First).ToBeVisibleAsync();
    }

    /// <summary>登入失敗與鎖定。</summary>
    [TestCase(TestName = "FR-60 FR-61 login lockout")]
    public async Task Lockout()
    {
        var user = UserName();
        var email = user + "@example.com";
        await using var context = await SiteHost.OpenAsync(1440);
        var page = await NewPage(context);
        await RegisterAsync(page, user, email, NextPhone());
        await page.GotoAsync("/account/login");
        await page.FillAsync("#account", user);
        await page.FillAsync("#password", "Wrong@123");
        await page.Locator("form[action='/account/login'] button").ClickAsync();
        await Expect(page.GetByText("帳號或密碼不正確。")).ToBeVisibleAsync();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await page.FillAsync("#password", "Wrong@123");
            await page.Locator("form[action='/account/login'] button").ClickAsync();
        }

        await page.FillAsync("#password", "Test@1234");
        await page.Locator("form[action='/account/login'] button").ClickAsync();
        await Expect(page.GetByText("帳號或密碼不正確。")).ToBeVisibleAsync();
        await Task.Delay(2500);
        await LoginAsync(page, email, "Test@1234");
        await Expect(page.Locator("a[href='/account']")).ToBeVisibleAsync();
    }

    /// <summary>忘記密碼、配額與失效連結。</summary>
    [TestCase(TestName = "FR-63 FR-64 FR-65 FR-67 FR-68 FR-69 password reset")]
    public async Task PasswordReset()
    {
        var user = UserName();
        var email = user + "@example.com";
        await using var context = await SiteHost.OpenAsync(1440);
        var page = await NewPage(context);
        await RegisterAsync(page, user, email, NextPhone());
        await page.GotoAsync("/account/forgot");
        await page.FillAsync("#account", "nobody-e2e");
        await page.Locator("form[action='/account/forgot'] button").ClickAsync();
        await Expect(page.GetByText("若帳號存在，重設信已寄出。")).ToBeVisibleAsync();
        var before = SiteHost.Mail.Messages.Count(item => item.To == email);
        await page.FillAsync("#account", email);
        await page.Locator("form[action='/account/forgot'] button").ClickAsync();
        await Expect(page.GetByText("若帳號存在，重設信已寄出。")).ToBeVisibleAsync();
        Assert.That(SiteHost.Mail.Messages.Count(item => item.To == email), Is.EqualTo(before + 1));
        await using var english = await SiteHost.OpenAsync(1440);
        var en = await NewPage(english);
        await en.GotoAsync("/account/forgot?culture=en");
        await en.FillAsync("#account", "nobody-e2e");
        await en.Locator("form[action='/account/forgot'] button").ClickAsync();
        await Expect(en.GetByText("If the account exists, a reset email was sent.")).ToBeVisibleAsync();
        await en.FillAsync("#account", email);
        await en.Locator("form[action='/account/forgot'] button").ClickAsync();
        await Expect(en.GetByText("If the account exists, a reset email was sent.")).ToBeVisibleAsync();
        while (SiteHost.Mail.Messages.Count(item => item.To == email) < 5)
        {
            await page.FillAsync("#account", email);
            await page.Locator("form[action='/account/forgot'] button").ClickAsync();
        }

        var sent = SiteHost.Mail.Messages.Count(item => item.To == email);
        await page.FillAsync("#account", email);
        await page.Locator("form[action='/account/forgot'] button").ClickAsync();
        await Expect(page.GetByText("若帳號存在，重設信已寄出。")).ToBeVisibleAsync();
        Assert.That(SiteHost.Mail.Messages.Count(item => item.To == email), Is.EqualTo(sent));
        var link = SiteHost.Mail.Messages.Last(item => item.To == email).Body.Trim();
        var linkMatch = Regex.Match(link, @"https?://\S+");
        Assert.That(linkMatch.Success, Is.True, link);
        link = linkMatch.Value;
        await page.GotoAsync("/account/reset");
        await page.FillAsync("#password", "New@12345");
        await page.FillAsync("#confirmPassword", "New@12345");
        await page.Locator("form[action='/account/reset'] button").ClickAsync();
        await Expect(page.GetByText("重設失敗，請重新申請。")).ToBeVisibleAsync();
        await page.GotoAsync("/account/reset?user=missing&token=not-a-token");
        await page.FillAsync("#password", "New@12345");
        await page.FillAsync("#confirmPassword", "New@12345");
        await page.Locator("form[action='/account/reset'] button").ClickAsync();
        await Expect(page.GetByText("重設失敗，請重新申請。")).ToBeVisibleAsync();
        await page.GotoAsync(link);
        await page.FillAsync("#password", "New@12345");
        await page.FillAsync("#confirmPassword", "New@12345");
        await page.Locator("form[action='/account/reset'] button").ClickAsync();
        await LoginAsync(page, user, "Test@1234");
        await Expect(page.GetByText("帳號或密碼不正確。")).ToBeVisibleAsync();
        await LoginAsync(page, user, "New@12345");
        await page.Locator("form[action='/account/logout'] button").ClickAsync();
        await page.WaitForURLAsync(url => !url.Contains("/account/login", StringComparison.Ordinal));
        await page.GotoAsync(link);
        await page.FillAsync("#password", "Other@1234");
        await page.FillAsync("#confirmPassword", "Other@1234");
        await page.Locator("form[action='/account/reset'] button").ClickAsync();
        await Expect(page.GetByText("重設失敗，請重新申請。")).ToBeVisibleAsync();
        await page.GotoAsync("/account/login");
        await page.FillAsync("#account", user);
        await page.FillAsync("#password", "New@12345");
        await page.Locator("form[action='/account/login'] button").ClickAsync();
        await Expect(page.Locator("a[href='/account']")).ToBeVisibleAsync();
    }

    /// <summary>個人資料。</summary>
    [TestCase(TestName = "FR-70 FR-71 profile")]
    public async Task Profile()
    {
        var user = UserName();
        var email = user + "@example.com";
        await using var context = await SiteHost.OpenAsync(1440);
        var page = await NewPage(context);
        await RegisterAsync(page, user, email, NextPhone());
        await LoginAsync(page, user, "Test@1234");
        await page.GotoAsync("/account");
        await Expect(page.GetByText("測試會員")).ToBeVisibleAsync();
        await Expect(page.GetByText("Email " + email)).ToBeVisibleAsync();
        await Expect(page.GetByText("使用者名稱 " + user)).ToBeVisibleAsync();
        await Expect(page.GetByText("1990-01-01")).ToBeVisibleAsync();
        await Expect(page.GetByText("Female")).ToBeVisibleAsync();
        Assert.That(await page.Locator("input[name='DisplayName']").CountAsync(), Is.EqualTo(0));
        var phone = NextPhone();
        await page.FillAsync("#Phone", phone);
        await page.FillAsync("#Address", "新地址");
        await page.Locator("form[action='/account'] button").ClickAsync();
        await Expect(page.Locator("#Phone")).ToHaveValueAsync(phone);
        await Expect(page.Locator("#Address")).ToHaveValueAsync("新地址");
        await page.FillAsync("#Phone", "12");
        await page.Locator("form[action='/account'] button").ClickAsync();
        await Expect(page.GetByText("電話須為 10 碼數字。")).ToBeVisibleAsync();
        await page.FillAsync("#Phone", "9000000001");
        await page.Locator("form[action='/account'] button").ClickAsync();
        await Expect(page.GetByText("這個電話已被使用。")).ToBeVisibleAsync();
    }

    /// <summary>賣家上架、庫存與刪除。</summary>
    [TestCase(TestName = "FR-03 FR-42 FR-80 FR-81 FR-82 FR-84 FR-85 FR-86 FR-87 seller products")]
    public async Task Seller()
    {
        await using var customerContext = await SiteHost.OpenAsync(1440);
        var customer = await NewPage(customerContext);
        var anon = await customer.GotoAsync("/seller/products/new");
        Assert.That(anon!.Status, Is.EqualTo(200));
        await customer.WaitForURLAsync("**/account/login**");
        var user = UserName();
        await RegisterAsync(customer, user, user + "@example.com", NextPhone());
        await LoginAsync(customer, user, "Test@1234");
        var forbidden = await customer.GotoAsync("/seller/products/new");
        Assert.That(forbidden!.Status, Is.EqualTo(403));
        await Expect(customer.GetByRole(AriaRole.Heading, new() { Name = "沒有權限" })).ToBeVisibleAsync();
        await using var operatorContext = await SiteHost.OpenAsync(1440);
        var op = await NewPage(operatorContext);
        await LoginAsync(op, "demo_operator", "Demo@1234");
        await Expect(op.Locator("a[href='/operator/sellers']")).ToBeVisibleAsync();
        await Expect(op.Locator("a[href='/seller/products/new']")).ToHaveCountAsync(0);
        await op.GotoAsync("/operator/sellers?query=" + user);
        await op.Locator("button[value='grant']").ClickAsync();
        await customer.Locator("form[action='/account/logout'] button").ClickAsync();
        await customer.WaitForURLAsync("**/");
        await LoginAsync(customer, user, "Test@1234");
        await Expect(customer.Locator("a[href='/seller/products/new']")).ToBeVisibleAsync();
        var name = "E2E " + user;
        await customer.GotoAsync("/seller/products/new");
        await customer.Locator("form[action='/seller/products/new'] button").ClickAsync();
        await Expect(customer.GetByText("必填。").First).ToBeVisibleAsync();
        await customer.FillAsync("#Name", new string('名', 51));
        await customer.FillAsync("#Price", "-1");
        await customer.FillAsync("#Keywords", new string('k', 501));
        await customer.Locator("form[action='/seller/products/new'] button").ClickAsync();
        await Expect(customer.GetByText("名稱不正確。")).ToBeVisibleAsync();
        await Expect(customer.GetByText("價格不正確。")).ToBeVisibleAsync();
        await Expect(customer.GetByText("關鍵字過長。")).ToBeVisibleAsync();
        await customer.FillAsync("#Name", "Demo Laptop");
        await customer.FillAsync("#Price", "10.00");
        await customer.FillAsync("#Keywords", "e2e");
        await customer.Locator("#image").SetInputFilesAsync(PngPath());
        await customer.Locator("form[action='/seller/products/new'] button").ClickAsync();
        await Expect(customer.GetByText("這個名稱已被使用。")).ToBeVisibleAsync();
        await customer.FillAsync("#Name", name);
        await customer.Locator("#image").SetInputFilesAsync(TextAs("note.txt"));
        await customer.Locator("form[action='/seller/products/new'] button").ClickAsync();
        await Expect(customer.GetByText("圖片無法使用。")).ToBeVisibleAsync();
        await customer.Locator("#image").SetInputFilesAsync(TextAs("bad.png"));
        await customer.Locator("form[action='/seller/products/new'] button").ClickAsync();
        await Expect(customer.GetByText("圖片無法使用。")).ToBeVisibleAsync();
        await customer.Locator("#image").SetInputFilesAsync(Oversize());
        await customer.Locator("form[action='/seller/products/new'] button").ClickAsync();
        await Expect(customer.GetByText("圖片無法使用。")).ToBeVisibleAsync();
        await customer.Locator("#image").SetInputFilesAsync(PngPath());
        await customer.Locator("form[action='/seller/products/new'] button").ClickAsync();
        await customer.WaitForURLAsync("**/account");
        await Expect(customer.GetByRole(AriaRole.Heading, new() { Name = name })).ToBeVisibleAsync();
        var block = customer.Locator("li").Filter(new() { HasText = name });
        await Expect(block.Locator("input[name='stock']")).ToHaveValueAsync("0");
        await customer.GotoAsync("/?keyword=" + Uri.EscapeDataString(name));
        var card = customer.Locator("li").Filter(new() { HasText = name });
        await Expect(card.GetByText("已售完")).ToBeVisibleAsync();
        await Expect(card.Locator("button")).ToBeDisabledAsync();
        var src = await card.Locator("img").First.GetAttributeAsync("src");
        Assert.That(src, Does.Contain("/products/" + user + "/"));
        Assert.That(src, Does.EndWith(".png"));
        await customer.GotoAsync("/account");
        await block.Locator("input[name='price']").FillAsync("-2");
        await block.Locator("button[value='update']").ClickAsync();
        await customer.GotoAsync("/account");
        await Expect(customer.Locator("li").Filter(new() { HasText = name }).Locator("input[name='price']")).ToHaveValueAsync("10.00");
        block = customer.Locator("li").Filter(new() { HasText = name });
        await block.Locator("input[name='stock']").FillAsync("2");
        await block.Locator("button[value='update']").ClickAsync();
        await customer.GotoAsync("/?keyword=" + Uri.EscapeDataString(name));
        await Expect(customer.Locator("li").Filter(new() { HasText = name }).Locator("button[type='submit']")).ToBeEnabledAsync();
        await using var buyerContext = await SiteHost.OpenAsync(1440);
        var buyer = await NewPage(buyerContext);
        var buyerName = UserName();
        await RegisterAsync(buyer, buyerName, buyerName + "@example.com", NextPhone());
        await LoginAsync(buyer, buyerName, "Test@1234");
        await buyer.GotoAsync("/?keyword=" + Uri.EscapeDataString(name));
        await buyer.Locator("li").Filter(new() { HasText = name }).Locator("button[type='submit']").ClickAsync();
        await buyer.WaitForURLAsync("**/cart");
        await buyer.GotoAsync("/checkout");
        await buyer.Locator("form[action='/checkout'] button").ClickAsync();
        await Expect(buyer.GetByText("訂單已成立")).ToBeVisibleAsync();
        await buyer.GotoAsync("/?keyword=" + Uri.EscapeDataString(name));
        await buyer.Locator("li").Filter(new() { HasText = name }).Locator("button[type='submit']").ClickAsync();
        await buyer.WaitForURLAsync("**/cart");
        await customer.GotoAsync("/account");
        block = customer.Locator("li").Filter(new() { HasText = name });
        await block.Locator("button[value='delete']").ClickAsync();
        await customer.GotoAsync("/account");
        await Expect(customer.GetByRole(AriaRole.Heading, new() { Name = name })).ToBeVisibleAsync();
        block = customer.Locator("li").Filter(new() { HasText = name });
        await block.Locator("input[name='confirm']").CheckAsync();
        await block.Locator("button[value='delete']").ClickAsync();
        await customer.GotoAsync("/account");
        await Expect(customer.GetByRole(AriaRole.Heading, new() { Name = name })).ToHaveCountAsync(0);
        await buyer.GotoAsync("/cart");
        await Expect(buyer.GetByRole(AriaRole.Heading, new() { Name = name })).ToHaveCountAsync(0);
        await buyer.GotoAsync("/account");
        await Expect(buyer.GetByText(name)).ToBeVisibleAsync();
    }

    /// <summary>非貨主與非管理者。</summary>
    [TestCase(TestName = "FR-88 FR-91 seller and operator forbidden")]
    public async Task Forbidden()
    {
        await using var context = await SiteHost.OpenAsync(1440);
        var page = await NewPage(context);
        await LoginAsync(page, "demo_seller", "Demo@1234");
        var denied = await page.GotoAsync("/operator/sellers");
        Assert.That(denied!.Status, Is.EqualTo(403));
        await page.GotoAsync("/account");
        var token = await Token(page);
        var response = await page.Context.APIRequest.PostAsync("/seller/products/999999", new()
        {
            Form = Fields(
                page,
                ("__RequestVerificationToken", token),
                ("action", "update"),
                ("price", "1.00"),
                ("stock", "1"),
                ("keywords", "x")),
            MaxRedirects = 0,
        });
        Assert.That(response.Status, Is.EqualTo(404));
    }

    /// <summary>管理者授予與收回。</summary>
    [TestCase(TestName = "FR-89 FR-90 operator grant")]
    public async Task OperatorGrant()
    {
        var user = UserName();
        var email = user + "@example.com";
        await using var memberContext = await SiteHost.OpenAsync(1440);
        var member = await NewPage(memberContext);
        await RegisterAsync(member, user, email, NextPhone());
        await using var context = await SiteHost.OpenAsync(1440);
        var page = await NewPage(context);
        await LoginAsync(page, "demo_operator", "Demo@1234");
        await page.GotoAsync("/operator/sellers?query=missing-e2e-user");
        await Expect(page.GetByText("找不到會員。")).ToBeVisibleAsync();
        await page.GotoAsync("/operator/sellers?query=" + email);
        await Expect(page.GetByText(user, new() { Exact = true })).ToBeVisibleAsync();
        await page.Locator("button[value='grant']").ClickAsync();
        await LoginAsync(member, user, "Test@1234");
        var allowed = await member.GotoAsync("/seller/products/new");
        Assert.That(allowed!.Status, Is.EqualTo(200));
        await Expect(member.Locator("h1")).ToHaveTextAsync("上架商品");
        var name = "Keep " + user;
        await member.FillAsync("#Name", name);
        await member.FillAsync("#Price", "8.00");
        await member.FillAsync("#Keywords", "keep");
        await member.Locator("#image").SetInputFilesAsync(PngPath());
        await member.Locator("form[action='/seller/products/new'] button").ClickAsync();
        await member.WaitForURLAsync("**/account");
        await page.GotoAsync("/operator/sellers?query=" + user);
        await page.Locator("button[value='revoke']").ClickAsync();
        await member.Locator("form[action='/account/logout'] button").ClickAsync();
        await member.WaitForURLAsync("**/");
        await LoginAsync(member, user, "Test@1234");
        var revoked = await member.GotoAsync("/seller/products/new");
        Assert.That(revoked!.Status, Is.EqualTo(403));
        await member.GotoAsync("/?keyword=" + Uri.EscapeDataString(name));
        await Expect(member.GetByRole(AriaRole.Heading, new() { Name = name })).ToBeVisibleAsync();
    }

    /// <summary>關於、部落格、聯絡與未知網址。</summary>
    [TestCase(TestName = "FR-100 FR-101 FR-102 FR-103 about blog contact")]
    public async Task Pages()
    {
        await using var context = await SiteHost.OpenAsync(1440);
        var page = await NewPage(context);
        await page.GotoAsync("/about");
        await Expect(page.Locator("h1")).ToHaveTextAsync("關於我們");
        await Expect(page.Locator("main p")).Not.ToBeEmptyAsync();
        await page.GotoAsync("/blog");
        await Expect(page.Locator("h1")).ToHaveTextAsync("部落格");
        await page.GotoAsync("/about?culture=en");
        await Expect(page.Locator("h1")).ToHaveTextAsync("About");
        await Expect(page.GetByText("demo marketplace")).ToBeVisibleAsync();
        await page.GotoAsync("/blog");
        await Expect(page.Locator("h1")).ToHaveTextAsync("Blog");
        await page.GotoAsync("/contact?culture=zh-Hant");
        await page.Locator("form[action='/contact'] button").ClickAsync();
        await Expect(page.GetByText("必填。").First).ToBeVisibleAsync();
        await page.FillAsync("#name", "訪客");
        await page.FillAsync("#email", "bad");
        await page.FillAsync("#message", new string('訊', 501));
        await page.Locator("form[action='/contact'] button").ClickAsync();
        await Expect(page.GetByText("Email 格式不正確。")).ToBeVisibleAsync();
        await Expect(page.GetByText("長度過長。")).ToBeVisibleAsync();
        var email = UserName() + "@example.com";
        await page.FillAsync("#email", email);
        await page.FillAsync("#message", "你好");
        await page.Locator("form[action='/contact'] button").ClickAsync();
        await Expect(page.GetByText("已收到您的訊息。")).ToBeVisibleAsync();
        Assert.That(SiteHost.CountContact(email), Is.EqualTo(1));
        await page.GotoAsync("/contact?culture=en");
        await page.FillAsync("#name", "Guest");
        await page.FillAsync("#email", UserName() + "@example.com");
        await page.FillAsync("#message", "Hello");
        await page.Locator("form[action='/contact'] button").ClickAsync();
        await Expect(page.GetByText("received")).ToBeVisibleAsync();
        await page.GotoAsync("/?culture=zh-Hant");
        var missing = await page.GotoAsync("/missing-page");
        Assert.That(missing!.Status, Is.EqualTo(404));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "找不到頁面" })).ToBeVisibleAsync();
        Assert.That(await page.ContentAsync(), Does.Not.Contain("SqlException"));
        Assert.That(await page.ContentAsync(), Does.Not.Contain("at Shopping"));
    }

    /// <summary>測試拋出點是一般 500 頁。</summary>
    [TestCase(TestName = "FR-104 fault page")]
    public async Task Fault()
    {
        await using var context = await SiteHost.OpenAsync(1440);
        var page = await NewPage(context);
        var response = await page.GotoAsync("/test/fault");
        Assert.That(response!.Status, Is.EqualTo(500));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "發生問題" })).ToBeVisibleAsync();
        await Expect(page.GetByText("請稍後再試。")).ToBeVisibleAsync();
        var html = await page.ContentAsync();
        Assert.That(html, Does.Not.Contain("InvalidOperationException"));
        Assert.That(html, Does.Not.Contain("測試用例外"));
    }

    /// <summary>沒有防偽造權杖的加入不會改變購物車。</summary>
    [TestCase(TestName = "FR-108 missing antiforgery token")]
    public async Task MissingToken()
    {
        await using var context = await SiteHost.OpenAsync(1440);
        var page = await NewPage(context);
        await LoginAsync(page, "demo_customer", "Demo@1234");
        await page.GotoAsync("/cart");
        while (await page.Locator("button[value='remove']").CountAsync() > 0)
        {
            await page.Locator("button[value='remove']").First.ClickAsync();
        }

        await page.GotoAsync("/");
        var href = await page.Locator("a:has(h2:text-is(\"Demo Smart Watch\"))").GetAttributeAsync("href");
        var id = href!.Split('/').Last();
        var response = await page.Context.APIRequest.PostAsync("/cart", new()
        {
            Form = Fields(page, ("productId", id), ("quantity", "1")),
            MaxRedirects = 0,
        });
        Assert.That(response.Status, Is.EqualTo(400));
        await page.GotoAsync("/cart");
        await Expect(page.GetByText("購物車是空的。")).ToBeVisibleAsync();
    }

    /// <summary>頁首切換語系後重新整理仍保留。</summary>
    [TestCase(TestName = "FR-07 culture cookie")]
    public async Task Culture()
    {
        await using var context = await SiteHost.OpenAsync(1440);
        var page = await NewPage(context);
        await page.GotoAsync("/");
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "市集", Exact = true })).ToBeVisibleAsync();
        await page.Locator("form[action='/culture'] input[value='en']").Locator("xpath=..").Locator("button").ClickAsync();
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Market", Exact = true })).ToBeVisibleAsync();
        await page.ReloadAsync();
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Market", Exact = true })).ToBeVisibleAsync();
        await page.Locator("form[action='/culture'] input[value='zh-Hant']").Locator("xpath=..").Locator("button").ClickAsync();
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "市集", Exact = true })).ToBeVisibleAsync();
    }

    /// <summary>每個路由在兩種寬度與兩種語系都開得起來。</summary>
    [TestCase(TestName = "FR-109 viewports")]
    public async Task Viewports()
    {
        var laptop = "";
        await using var probe = await SiteHost.OpenAsync(1440);
        var probePage = await NewPage(probe);
        await probePage.GotoAsync("/");
        laptop = (await probePage.Locator("a:has(h2:text-is(\"Demo Smart Watch\"))").GetAttributeAsync("href"))!;
        foreach (var culture in new[] { "zh-Hant", "en" })
        {
            foreach (var width in new[] { 375, 1440 })
            {
                await using var context = await SiteHost.OpenAsync(width);
                var page = await NewPage(context);
                await page.GotoAsync("/?culture=" + culture);
                await AssertFits(page, culture);
                foreach (var path in new[] { "/categories", "/about", "/blog", "/contact", "/account/register", "/account/login", "/account/forgot", "/account/reset?user=no&token=no", laptop, "/products/999999", "/missing-page", "/error/404" })
                {
                    var response = await page.GotoAsync(path);
                    await AssertFits(page, culture);
                    if (path is "/products/999999" or "/missing-page" or "/error/404")
                    {
                        Assert.That(response!.Status, Is.EqualTo(404));
                    }
                }

                var cart = await page.GotoAsync("/cart");
                await page.WaitForURLAsync("**/account/login**");
                await AssertFits(page, culture);
                Assert.That(cart!.Status, Is.AnyOf(200, 302));
                var checkout = await page.GotoAsync("/checkout");
                await page.WaitForURLAsync("**/account/login**");
                await AssertFits(page, culture);
                Assert.That(checkout!.Status, Is.AnyOf(200, 302));
            }
        }

        await using var memberContext = await SiteHost.OpenAsync(1440);
        var member = await NewPage(memberContext);
        await LoginAsync(member, "demo_customer", "Demo@1234");
        await member.GotoAsync("/cart");
        while (await member.Locator("button[value='remove']").CountAsync() > 0)
        {
            await member.Locator("button[value='remove']").First.ClickAsync();
        }

        var empty = await member.GotoAsync("/checkout");
        await member.WaitForURLAsync("**/cart");
        Assert.That(empty!.Status, Is.AnyOf(200, 302));
        await member.GotoAsync(laptop);
        await member.Locator("form[action='/cart'] button").ClickAsync();
        await member.WaitForURLAsync("**/cart");
        foreach (var culture in new[] { "zh-Hant", "en" })
        {
            foreach (var width in new[] { 375, 768, 1024, 1440 })
            {
                await member.SetViewportSizeAsync(width, 900);
                if (width is 768 or 1024 && culture == "en")
                {
                    await member.GotoAsync("/?culture=en");
                    await AssertFits(member, culture);
                }

                if (width is 375 or 1440)
                {
                    await member.GotoAsync("/cart?culture=" + culture);
                    await AssertFits(member, culture);
                    await member.GotoAsync("/account?culture=" + culture);
                    await AssertFits(member, culture);
                    await member.GotoAsync("/checkout?culture=" + culture);
                    await AssertFits(member, culture);
                }

                if (width is 768 or 1024)
                {
                    await member.GotoAsync("/?culture=" + culture);
                    await AssertFits(member, culture);
                    await member.GotoAsync("/checkout?culture=" + culture);
                    await AssertFits(member, culture);
                }
            }
        }

        await member.GotoAsync("/cart");
        await member.Locator("button[value='remove']").ClickAsync();
        await LoginAsync(member, "demo_seller", "Demo@1234");
        foreach (var culture in new[] { "zh-Hant", "en" })
        {
            foreach (var width in new[] { 375, 1440 })
            {
                await member.SetViewportSizeAsync(width, 900);
                await member.GotoAsync("/account?culture=" + culture);
                await AssertFits(member, culture);
                await member.GotoAsync("/seller/products/new?culture=" + culture);
                await AssertFits(member, culture);
            }
        }

        var sellerDenied = await member.GotoAsync("/operator/sellers");
        Assert.That(sellerDenied!.Status, Is.EqualTo(403));
        await LoginAsync(member, "demo_operator", "Demo@1234");
        foreach (var culture in new[] { "zh-Hant", "en" })
        {
            foreach (var width in new[] { 375, 1440 })
            {
                await member.SetViewportSizeAsync(width, 900);
                await member.GotoAsync("/operator/sellers?culture=" + culture);
                await AssertFits(member, culture);
            }
        }

        await LoginAsync(member, "demo_customer", "Demo@1234");
        await member.GotoAsync("/?culture=zh-Hant");
        foreach (var width in new[] { 375, 1440 })
        {
            await member.SetViewportSizeAsync(width, 900);
            var sellerPage = await member.GotoAsync("/seller/products/new");
            Assert.That(sellerPage!.Status, Is.EqualTo(403));
            await AssertFits(member, "zh-Hant");
            var operatorPage = await member.GotoAsync("/operator/sellers");
            Assert.That(operatorPage!.Status, Is.EqualTo(403));
            await AssertFits(member, "zh-Hant");
        }
    }

    private static async Task<IPage> NewPage(IBrowserContext context)
    {
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(15000);
        page.SetDefaultNavigationTimeout(20000);
        return page;
    }

    private static void AssertSecurity(IResponse response)
    {
        var headers = response.Headers;
        Assert.That(headers["x-content-type-options"], Is.EqualTo("nosniff"));
        Assert.That(headers["x-frame-options"], Is.EqualTo("DENY"));
        Assert.That(headers["referrer-policy"], Is.EqualTo("strict-origin-when-cross-origin"));
        Assert.That(headers["permissions-policy"], Does.Contain("payment=()"));
        var csp = headers["content-security-policy"];
        Assert.That(csp, Does.Contain("script-src 'self'"));
        Assert.That(csp, Does.Contain("style-src 'self'"));
        Assert.That(csp, Does.Not.Contain("unsafe-inline"));
        Assert.That(csp, Does.Not.Contain("unsafe-eval"));
        Assert.That(headers.ContainsKey("server"), Is.False);
    }

    private static async Task AssertFits(IPage page, string culture)
    {
        var brand = culture == "en" ? "Market" : "市集";
        var brandLink = page.GetByRole(AriaRole.Link, new() { Name = brand, Exact = true });
        await Expect(brandLink).ToBeVisibleAsync();
        await Expect(page.Locator("h1").First).ToBeVisibleAsync();
        var overflow = await page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth");
        Assert.That(overflow, Is.LessThanOrEqualTo(1));
        await brandLink.ClickAsync(new LocatorClickOptions { Trial = true });
    }

    private static async Task<IReadOnlyList<string>> Names(IPage page)
    {
        return await page.Locator("main ul li h2").AllTextContentsAsync();
    }

    private static IFormData Fields(IPage page, params (string Name, string Value)[] pairs)
    {
        var form = page.Context.APIRequest.CreateFormData();
        foreach (var pair in pairs)
        {
            form.Set(pair.Name, pair.Value);
        }

        return form;
    }

    private static async Task LoginAsync(IPage page, string account, string password, bool expectCookie = false)
    {
        await page.GotoAsync("/account/login");
        if (await page.Locator("#account").CountAsync() == 0)
        {
            await page.Locator("form[action='/account/logout'] button").ClickAsync();
            await page.WaitForURLAsync(url => !url.Contains("/account/login", StringComparison.Ordinal));
            await page.GotoAsync("/account/login");
        }

        await page.FillAsync("#account", account);
        await page.FillAsync("#password", password);
        var posted = await page.RunAndWaitForResponseAsync(
            () => page.Locator("form[action='/account/login'] button").ClickAsync(),
            item => item.Request.Method == "POST" && item.Url.Contains("/account/login", StringComparison.Ordinal));
        if (expectCookie)
        {
            var cookie = await SetCookieAsync(posted);
            Assert.That(cookie, Does.Contain("httponly").IgnoreCase);
            Assert.That(cookie, Does.Contain("secure").IgnoreCase);
            Assert.That(cookie, Does.Contain("samesite=lax").IgnoreCase);
        }

        if (password == "Test@1234" || password == "New@12345" || password == "Demo@1234")
        {
            try
            {
                await page.WaitForURLAsync(url => !url.Contains("/account/login", StringComparison.Ordinal), new() { Timeout = 5000 });
            }
            catch (TimeoutException)
            {
                if (password == "Test@1234" && await page.GetByText("帳號或密碼不正確。").CountAsync() > 0)
                {
                    return;
                }

                throw;
            }
        }
    }

    private static async Task<string> SetCookieAsync(IResponse response)
    {
        var headers = await response.HeadersArrayAsync();
        return string.Join('\n', headers.Where(item => item.Name.Equals("set-cookie", StringComparison.OrdinalIgnoreCase)).Select(item => item.Value));
    }

    private static async Task RegisterAsync(IPage page, string user, string email, string phone)
    {
        await page.GotoAsync("/account/register");
        await FillRegister(page, "測試會員", email, user, "Test@1234", "Test@1234", phone, "1990-01-01");
        await page.Locator("form[action='/account/register'] button").ClickAsync();
        await page.WaitForURLAsync("**/account/login");
    }

    private static async Task FillRegister(IPage page, string name, string email, string user, string password, string confirm, string phone, string birth)
    {
        await page.FillAsync("#DisplayName", name);
        await page.FillAsync("#Email", email);
        await page.FillAsync("#UserName", user);
        await page.FillAsync("#Password", password);
        await page.FillAsync("#ConfirmPassword", confirm);
        await page.FillAsync("#Phone", phone);
        await page.FillAsync("#BirthDate", birth);
        await page.SelectOptionAsync("#Gender", "Female");
        await page.FillAsync("#Country", "Taiwan");
        await page.FillAsync("#Region", "Taipei");
        await page.FillAsync("#City", "Taipei");
        await page.FillAsync("#Address", "Test address");
    }

    private static async Task<string> OpenProduct(IPage page, string name)
    {
        await page.GotoAsync("/?keyword=" + Uri.EscapeDataString(name));
        var href = await page.Locator($"a:has(h2:text-is(\"{name}\"))").GetAttributeAsync("href");
        await page.GotoAsync(href!);
        return href!;
    }

    private static async Task UpdateProduct(IPage page, string name, string? price, string? stock)
    {
        await page.GotoAsync("/account");
        var block = page.Locator("li").Filter(new() { HasText = name }).First;
        if (price is not null)
        {
            await block.Locator("input[name='price']").FillAsync(price);
        }

        if (stock is not null)
        {
            await block.Locator("input[name='stock']").FillAsync(stock);
        }

        await block.Locator("button[value='update']").ClickAsync();
        await page.WaitForURLAsync("**/account");
    }

    private static async Task<string> Token(IPage page)
    {
        return await page.Locator("input[name='__RequestVerificationToken']").First.InputValueAsync();
    }

    private static async Task<string> ReadPdf(IPage page, string orderNumber)
    {
        var download = await page.RunAndWaitForDownloadAsync(() => page.GetByRole(AriaRole.Link, new() { Name = "下載發票" }).ClickAsync());
        Assert.That(download.SuggestedFilename, Is.EqualTo("OrderInvoice.pdf"));
        var path = Path.Combine(AppContext.BaseDirectory, orderNumber + ".pdf");
        await download.SaveAsAsync(path);
        var bytes = await File.ReadAllBytesAsync(path);
        Assert.That(Encoding.ASCII.GetString(bytes, 0, 5), Is.EqualTo("%PDF-"));
        using var document = PdfDocument.Open(bytes);
        return string.Join('\n', document.GetPages().Select(item => item.Text));
    }

    private static string UserName()
    {
        return "e" + Guid.NewGuid().ToString("N")[..8];
    }

    private static string NextPhone()
    {
        return Interlocked.Increment(ref _phone).ToString(CultureInfo.InvariantCulture);
    }

    private static string TaipeiToday()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string PngPath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "one.png");
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        }

        return path;
    }

    private static string TextAs(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, name);
        File.WriteAllBytes(path, "not an image"u8.ToArray());
        return path;
    }

    private static string Oversize()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "big.png");
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, new byte[(2 * 1024 * 1024) + 1]);
        }

        return path;
    }
}
