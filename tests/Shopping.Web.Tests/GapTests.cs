using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shopping.Data;
using Shopping.Web.Security;
using Shopping.Web.Services;

namespace Shopping.Web.Tests;

/// <summary>補齊服務與進入點尚未走到的分支。</summary>
public sealed class GapTests : IClassFixture<ShopApp>
{
    private static long _phones = Random.Shared.NextInt64(7_100_000_000, 7_900_000_000);
    private readonly ShopApp _app;

    /// <summary>建立測試。</summary>
    public GapTests(ShopApp app)
    {
        _app = app;
    }

    /// <summary>註冊與個人資料的每一種拒絕。</summary>
    [Fact]
    public async Task AccountEdges()
    {
        using var scope = _app.Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var empty = await accounts.RegisterAsync(new RegistrationForm(), CancellationToken.None);
        Assert.Equal("Required", empty["DisplayName"]);
        Assert.Equal("Required", empty["Email"]);
        Assert.Equal("Required", empty["UserName"]);
        Assert.Equal("Required", empty["Password"]);
        Assert.Equal("Required", empty["ConfirmPassword"]);
        Assert.Equal("Required", empty["Phone"]);
        Assert.Equal("Required", empty["BirthDate"]);
        Assert.Equal("Required", empty["Gender"]);
        Assert.Equal("Required", empty["Country"]);
        Assert.Equal("Required", empty["Address"]);
        var invalid = await accounts.RegisterAsync(Form(NextUser(), NextPhone(), form =>
        {
            form.DisplayName = "bad1";
            form.Email = "not-an-email";
            form.UserName = "bad_name";
            form.Password = "abcdefg1";
            form.ConfirmPassword = "other";
            form.Phone = "123";
            form.BirthDate = "2099-01-01";
            form.Gender = "nope";
            form.Country = new string('c', 51);
            form.Region = new string('r', 51);
            form.City = new string('y', 51);
            form.Address = new string('a', 501);
        }), CancellationToken.None);
        Assert.Equal("DisplayNameInvalid", invalid["DisplayName"]);
        Assert.Equal("EmailInvalid", invalid["Email"]);
        Assert.Equal("UserNameInvalid", invalid["UserName"]);
        Assert.Equal("PasswordInvalid", invalid["Password"]);
        Assert.Equal("ConfirmMismatch", invalid["ConfirmPassword"]);
        Assert.Equal("PhoneInvalid", invalid["Phone"]);
        Assert.Equal("BirthInFuture", invalid["BirthDate"]);
        Assert.Equal("GenderInvalid", invalid["Gender"]);
        Assert.Equal("TooLong", invalid["Country"]);
        var unparsed = await accounts.RegisterAsync(Form(NextUser(), NextPhone(), form => form.BirthDate = "nope"), CancellationToken.None);
        Assert.Equal("BirthInvalid", unparsed["BirthDate"]);
        var existing = NextUser();
        Assert.Empty(await accounts.RegisterAsync(Form(existing, NextPhone()), CancellationToken.None));
        var emailTaken = await accounts.RegisterAsync(Form(NextUser(), NextPhone(), form => form.Email = existing + "@example.com"), CancellationToken.None);
        Assert.Equal("EmailTaken", emailTaken["Email"]);
        var nameTaken = await accounts.RegisterAsync(Form(existing, NextPhone()), CancellationToken.None);
        Assert.Equal("UserNameTaken", nameTaken["UserName"]);
        var phoneTaken = await accounts.RegisterAsync(Form(NextUser(), "9000000001"), CancellationToken.None);
        Assert.Equal("PhoneTaken", phoneTaken["Phone"]);
        var customer = (await users.FindByNameAsync("demo_customer"))!;
        var missingUser = await accounts.UpdateProfileAsync("missing-user", Profile(), CancellationToken.None);
        Assert.Equal("Required", missingUser[""]);
        var takenProfile = await accounts.UpdateProfileAsync(customer.Id, Profile("9000000002"), CancellationToken.None);
        Assert.Equal("PhoneTaken", takenProfile["Phone"]);
        var blankProfile = await accounts.UpdateProfileAsync(customer.Id, new ProfileForm(), CancellationToken.None);
        Assert.Equal("Required", blankProfile["Phone"]);
        Assert.Equal("Required", blankProfile["Address"]);
        await using var rejecting = new RejectingPasswordApp();
        using var rejectedScope = rejecting.Services.CreateScope();
        var rejectedAccounts = rejectedScope.ServiceProvider.GetRequiredService<AccountService>();
        var rejected = await rejectedAccounts.RegisterAsync(Form(NextUser(), NextPhone()), CancellationToken.None);
        Assert.Equal("PasswordInvalid", rejected["Password"]);
    }

    /// <summary>重設密碼的配額、寄信失敗與無效權杖。</summary>
    [Fact]
    public async Task PasswordResetEdges()
    {
        using var scope = _app.Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var resets = scope.ServiceProvider.GetRequiredService<PasswordResetService>();
        var mailbox = scope.ServiceProvider.GetRequiredService<MemoryMailbox>();
        var db = scope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
        var name = NextUser();
        Assert.Empty(await accounts.RegisterAsync(Form(name, NextPhone()), CancellationToken.None));
        var user = (await users.FindByNameAsync(name))!;
        Assert.Equal(ResetAsk.Generic, await resets.RequestAsync("  ", "https://localhost/account/reset", CancellationToken.None));
        Assert.Equal(ResetAsk.Generic, await resets.RequestAsync("nosuch", "https://localhost/account/reset", CancellationToken.None));
        Assert.Equal(ResetAsk.Generic, await resets.RequestAsync("not-an-email", "https://localhost/account/reset", CancellationToken.None));
        mailbox.ThrowOnSend = true;
        try
        {
            Assert.Equal(ResetAsk.Unavailable, await resets.RequestAsync(user.Email, "https://localhost/account/reset", CancellationToken.None));
        }
        finally
        {
            mailbox.ThrowOnSend = false;
        }

        Assert.False(await db.PasswordResetQuotas.AnyAsync(item => item.UserId == user.Id));
        var sent = mailbox.Messages.Last();
        Assert.Equal(user.Email, sent.To);
        Assert.Equal("重設密碼", sent.Subject);
        Assert.Contains("token=", sent.Body, StringComparison.Ordinal);
        Assert.Equal(ResetAsk.Generic, await resets.RequestAsync(name, "https://localhost/account/reset", CancellationToken.None));
        Assert.Equal(ResetAsk.Generic, await resets.RequestAsync(name, "https://localhost/account/reset", CancellationToken.None));
        var quota = await db.PasswordResetQuotas.FirstAsync(item => item.UserId == user.Id);
        Assert.Equal(2, quota.Count);
        quota.Count = 5;
        await db.SaveChangesAsync();
        var before = mailbox.Messages.Count;
        Assert.Equal(ResetAsk.Generic, await resets.RequestAsync(name, "https://localhost/account/reset", CancellationToken.None));
        Assert.Equal(before, mailbox.Messages.Count);
        quota.WindowStart = DateTimeOffset.UtcNow.AddHours(-2);
        quota.Count = 5;
        await db.SaveChangesAsync();
        Assert.Equal(ResetAsk.Generic, await resets.RequestAsync(name, "https://localhost/account/reset", CancellationToken.None));
        await db.Entry(quota).ReloadAsync();
        Assert.Equal(1, quota.Count);
        var required = await resets.ResetAsync(user.Id, "token", "", "", CancellationToken.None);
        Assert.Equal("Required", required["Password"]);
        Assert.Equal("Required", required["ConfirmPassword"]);
        var mismatch = await resets.ResetAsync(user.Id, "token", "short", "other", CancellationToken.None);
        Assert.Equal("PasswordInvalid", mismatch["Password"]);
        Assert.Equal("ConfirmMismatch", mismatch["ConfirmPassword"]);
        var noUser = await resets.ResetAsync("", "", "Test@1234", "Test@1234", CancellationToken.None);
        Assert.Equal("ResetFailed", noUser[""]);
        var missing = await resets.ResetAsync("missing-user", "token", "Test@1234", "Test@1234", CancellationToken.None);
        Assert.Equal("ResetFailed", missing[""]);
        var badToken = await resets.ResetAsync(user.Id, "not-a-token", "Test@1234", "Test@1234", CancellationToken.None);
        Assert.Equal("ResetFailed", badToken[""]);
    }

    /// <summary>購物車拒絕、編號碰撞與目錄空關鍵字。</summary>
    [Fact]
    public async Task CartCheckoutAndCatalogEdges()
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var cart = scope.ServiceProvider.GetRequiredService<CartService>();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogService>();
        var customer = (await users.FindByNameAsync("demo_customer"))!;
        var login = scope.ServiceProvider.GetRequiredService<LoginService>();
        Assert.False((await login.LoginAsync("nosuch", "Test@1234")).Succeeded);
        Assert.False((await login.LoginAsync("missing@example.com", "Test@1234")).Succeeded);
        await ClearCartAsync(db, customer.Id);
        Assert.Equal(CartAddStatus.Missing, await cart.AddAsync(customer.Id, 999999, 1, CancellationToken.None));
        var arcade = await db.Products.FirstAsync(item => item.Name == "Demo Arcade Machine");
        var arcadeStock = arcade.Stock;
        arcade.Stock = 0;
        await db.SaveChangesAsync();
        Assert.Equal(CartAddStatus.SoldOut, await cart.AddAsync(customer.Id, arcade.Id, 1, CancellationToken.None));
        arcade.Stock = arcadeStock;
        await db.SaveChangesAsync();
        var printer = await db.Products.FirstAsync(item => item.Name == "Demo Laser Printer");
        Assert.Equal(CartAddStatus.Rejected, await cart.AddAsync(customer.Id, printer.Id, 0, CancellationToken.None));
        Assert.Equal(CartAddStatus.Rejected, await cart.AddAsync(customer.Id, printer.Id, 11, CancellationToken.None));
        Assert.Equal(CartAddStatus.Rejected, await cart.AddAsync(customer.Id, printer.Id, printer.Stock + 1, CancellationToken.None));
        var watch = await db.Products.FirstAsync(item => item.Name == "Demo Smart Watch");
        Assert.Equal(CartAddStatus.Ok, await cart.AddAsync(customer.Id, watch.Id, 1, CancellationToken.None));
        Assert.Equal(CartAddStatus.Ok, await cart.AddAsync(customer.Id, watch.Id, 1, CancellationToken.None));
        await ClearCartAsync(db, customer.Id);
        Assert.Equal(CartAddStatus.Ok, await cart.AddAsync(customer.Id, watch.Id, 6, CancellationToken.None));
        Assert.Equal(CartAddStatus.Rejected, await cart.AddAsync(customer.Id, watch.Id, 6, CancellationToken.None));
        await ClearCartAsync(db, customer.Id);
        Assert.Equal(CartAddStatus.Ok, await cart.AddAsync(customer.Id, printer.Id, 1, CancellationToken.None));
        Assert.Equal(CartAddStatus.Rejected, await cart.AddAsync(customer.Id, printer.Id, printer.Stock, CancellationToken.None));
        Assert.False(await cart.UpdateAsync(customer.Id, 999999, 1, CancellationToken.None));
        Assert.False(await cart.UpdateAsync(customer.Id, printer.Id, 0, CancellationToken.None));
        Assert.True(await cart.UpdateAsync(customer.Id, printer.Id, 1, CancellationToken.None));
        var page = await cart.ReadAsync(customer.Id, CancellationToken.None);
        Assert.Equal(printer.Name, page.Lines[0].Name);
        Assert.Equal(1, page.Lines[0].Quantity);
        Assert.True(page.Total > 0);
        Assert.False(await cart.RemoveAsync(customer.Id, 999999, CancellationToken.None));
        Assert.True(await cart.RemoveAsync(customer.Id, printer.Id, CancellationToken.None));
        Assert.Equal(0, await cart.TotalAsync(new ClaimsPrincipal(new ClaimsIdentity()), CancellationToken.None));
        Assert.Equal(0, await cart.TotalAsync(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, customer.Id) }, "test")), CancellationToken.None));
        var cards = await catalog.SearchAsync(null, null, null, CancellationToken.None);
        Assert.Contains(cards, card => card.Name == "Demo Laptop");
        ReadCard(cards[0]);
        var links = await catalog.CategoriesAsync(CancellationToken.None);
        Assert.Contains(links, link => link.Name == "Computer");
        Assert.Equal(links[0].Id, (await catalog.CategoriesAsync(CancellationToken.None))[0].Id);
        var laptop = await db.Products.FirstAsync(item => item.Name == "Demo Laptop");
        var keywords = laptop.Keywords;
        laptop.Keywords = null;
        await db.SaveChangesAsync();
        var found = await catalog.FindAsync(laptop.Id, CancellationToken.None);
        Assert.Equal("", found!.Keywords);
        laptop.Keywords = keywords;
        await db.SaveChangesAsync();
        var number = "V" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant();
        db.Orders.Add(new ShopOrder { Number = number, UserId = customer.Id, CreatedAt = DateTimeOffset.UtcNow, Total = 1 });
        await db.SaveChangesAsync();
        var stock = laptop.Stock;
        if (stock < 1)
        {
            laptop.Stock = 2;
            await db.SaveChangesAsync();
        }

        Assert.Equal(CartAddStatus.Ok, await cart.AddAsync(customer.Id, laptop.Id, 1, CancellationToken.None));
        var checkout = new CheckoutService(db, new ScriptedNumbers(number, "V" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant()), TimeProvider.System);
        var placed = await checkout.PlaceAsync(customer.Id, CancellationToken.None);
        Assert.IsType<CheckoutResult.Placed>(placed);
        Assert.Equal(CartAddStatus.Ok, await cart.AddAsync(customer.Id, laptop.Id, 1, CancellationToken.None));
        var blocked = new CheckoutService(db, new ScriptedNumbers(number, number), TimeProvider.System);
        Assert.IsType<CheckoutResult.Unavailable>(await blocked.PlaceAsync(customer.Id, CancellationToken.None));
        Assert.True(await db.CartLines.AnyAsync(item => item.UserId == customer.Id && item.ProductId == laptop.Id));
        await ClearCartAsync(db, customer.Id);
        laptop.Stock = stock;
        await db.SaveChangesAsync();
        TouchModels(db);
    }

    /// <summary>賣家、管理者與聯絡表單的拒絕分支。</summary>
    [Fact]
    public async Task SellerOperatorAndContactEdges()
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var sellerService = scope.ServiceProvider.GetRequiredService<SellerService>();
        var operators = scope.ServiceProvider.GetRequiredService<OperatorService>();
        var contact = scope.ServiceProvider.GetRequiredService<ContactService>();
        var seller = (await users.FindByNameAsync("demo_seller"))!;
        var customer = (await users.FindByNameAsync("demo_customer"))!;
        var categoryId = await db.Categories.Select(item => item.Id).FirstAsync();
        var empty = await sellerService.CreateAsync(seller.Id, seller.UserName!, new ProductForm(), null, null, CancellationToken.None);
        Assert.Equal("Required", empty["Name"]);
        Assert.Equal("Required", empty["Price"]);
        Assert.Equal("ImageRequired", empty["Image"]);
        var invalid = await sellerService.CreateAsync(seller.Id, seller.UserName!, new ProductForm
        {
            Name = new string('n', 51),
            Price = "-1",
            Keywords = new string('k', 501),
            CategoryId = categoryId,
        }, new MemoryStream(Png()), "   ", CancellationToken.None);
        Assert.Equal("NameInvalid", invalid["Name"]);
        Assert.Equal("PriceInvalid", invalid["Price"]);
        Assert.Equal("KeywordsInvalid", invalid["Keywords"]);
        Assert.Equal("ImageRequired", invalid["Image"]);
        var badCategory = await sellerService.CreateAsync(seller.Id, seller.UserName!, new ProductForm
        {
            Name = "Unique " + Guid.NewGuid().ToString("N")[..6],
            Price = "10",
            Keywords = "demo",
            CategoryId = 999999,
        }, new MemoryStream(Png()), "shot.png", CancellationToken.None);
        Assert.Equal("CategoryInvalid", badCategory["CategoryId"]);
        var taken = await sellerService.CreateAsync(seller.Id, seller.UserName!, new ProductForm
        {
            Name = "Demo Laptop",
            Price = "10",
            CategoryId = categoryId,
        }, new MemoryStream(Png()), "shot.png", CancellationToken.None);
        Assert.Equal("NameTaken", taken["Name"]);
        var badImage = await sellerService.CreateAsync(seller.Id, seller.UserName!, new ProductForm
        {
            Name = "Unique " + Guid.NewGuid().ToString("N")[..6],
            Price = "10",
            CategoryId = categoryId,
        }, new MemoryStream([1, 2, 3, 4]), "shot.png", CancellationToken.None);
        Assert.Equal("ImageInvalid", badImage["Image"]);
        var createdName = "Unique " + Guid.NewGuid().ToString("N")[..6];
        var created = await sellerService.CreateAsync(seller.Id, seller.UserName!, new ProductForm
        {
            Name = createdName,
            Price = "10",
            Keywords = null,
            CategoryId = categoryId,
        }, new MemoryStream(Png()), "shot.png", CancellationToken.None);
        Assert.Empty(created);
        var product = await db.Products.FirstAsync(item => item.Name == createdName);
        Assert.Equal("", product.Keywords);
        Assert.Equal(SellerEditStatus.NotFound, await sellerService.UpdateAsync(seller.Id, 999999, "1", "1", "", CancellationToken.None));
        Assert.Equal(SellerEditStatus.NotFound, await sellerService.UpdateAsync(customer.Id, product.Id, "1", "1", "", CancellationToken.None));
        Assert.Equal(SellerEditStatus.Invalid, await sellerService.UpdateAsync(seller.Id, product.Id, "-1", "1", "", CancellationToken.None));
        Assert.Equal(SellerEditStatus.Invalid, await sellerService.UpdateAsync(seller.Id, product.Id, "1", "-1", "", CancellationToken.None));
        Assert.Equal(SellerEditStatus.Invalid, await sellerService.UpdateAsync(seller.Id, product.Id, "1", "1", new string('k', 501), CancellationToken.None));
        Assert.Equal(SellerEditStatus.Ok, await sellerService.UpdateAsync(seller.Id, product.Id, "12.50", "3", null, CancellationToken.None));
        Assert.Equal(SellerEditStatus.NotFound, await sellerService.DeleteAsync(seller.Id, 999999, true, CancellationToken.None));
        Assert.Equal(SellerEditStatus.NotFound, await sellerService.DeleteAsync(customer.Id, product.Id, true, CancellationToken.None));
        Assert.Equal(SellerEditStatus.NeedsConfirm, await sellerService.DeleteAsync(seller.Id, product.Id, false, CancellationToken.None));
        Assert.Equal(SellerEditStatus.Ok, await sellerService.DeleteAsync(seller.Id, product.Id, true, CancellationToken.None));
        Assert.Null(await operators.FindAsync("  "));
        Assert.Null(await operators.FindAsync("nosuch"));
        Assert.Null(await operators.FindAsync("missing@example.com"));
        var byEmail = await operators.FindAsync(" demo_customer@example.com ");
        Assert.Equal("demo_customer", byEmail!.UserName);
        Assert.False(byEmail.IsSeller);
        Assert.False(await operators.ChangeSellerAsync("missing-user", true));
        Assert.True(await operators.ChangeSellerAsync(seller.Id, true));
        Assert.True(await operators.ChangeSellerAsync(customer.Id, false));
        Assert.True(await operators.ChangeSellerAsync(customer.Id, true));
        Assert.True(await operators.ChangeSellerAsync(customer.Id, false));
        Assert.False(await operators.ChangeOperatorAsync(seller.Id, customer.Id, true));
        var blankContact = await contact.SendAsync(" ", " ", " ", CancellationToken.None);
        Assert.Equal("Required", blankContact["Name"]);
        Assert.Equal("Required", blankContact["Email"]);
        Assert.Equal("Required", blankContact["Message"]);
        var longContact = await contact.SendAsync(new string('名', 51), "bad", new string('m', 501), CancellationToken.None);
        Assert.Equal("TooLong", longContact["Name"]);
        Assert.Equal("EmailInvalid", longContact["Email"]);
        Assert.Equal("TooLong", longContact["Message"]);
        Assert.Empty(await contact.SendAsync("訪客", "guest@example.com", "hello", CancellationToken.None));
    }

    /// <summary>已登入仍開啟帳戶頁、賣家動作、以及結帳編號碰撞。</summary>
    [Fact(DisplayName = "FR-58 FR-109 signed-in pages and headings")]
    public async Task SignedInPagesAndCollidingCheckout()
    {
        var client = await LoginAsync("demo_customer");
        foreach (var path in new[] { "/account/register", "/account/login", "/account/forgot", "/account/reset" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/", response.Headers.Location?.OriginalString);
        }

        var homeToken = await TokenAsync(client, "/");
        foreach (var path in new[] { "/account/register", "/account/login", "/account/forgot", "/account/reset" })
        {
            var response = await PostAsync(client, path, homeToken, new Dictionary<string, string>());
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var anonymous = _app.CreateNoRedirectClient();
        var login = await PostAsync(anonymous, "/account/login", await TokenAsync(anonymous, "/account/login"), new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var reset = await anonymous.GetAsync("/account/reset");
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var contact = await PostAsync(anonymous, "/contact", await TokenAsync(anonymous, "/contact"), new Dictionary<string, string> { ["name"] = new string('名', 51) });
        Assert.Equal(HttpStatusCode.OK, contact.StatusCode);
        var nameless = await PostAsync(anonymous, "/contact", await TokenAsync(anonymous, "/contact"), new Dictionary<string, string> { ["email"] = "bad" });
        Assert.Equal(HttpStatusCode.OK, nameless.StatusCode);
        var buyerForEmpty = await LoginAsync("demo_customer");
        using (var emptyScope = _app.Services.CreateScope())
        {
            var emptyDb = emptyScope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
            var emptyUsers = emptyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var emptyCustomer = (await emptyUsers.FindByNameAsync("demo_customer"))!;
            await ClearCartAsync(emptyDb, emptyCustomer.Id);
        }

        var emptyCheckout = await buyerForEmpty.GetAsync("/checkout");
        Assert.Equal(HttpStatusCode.Redirect, emptyCheckout.StatusCode);
        Assert.Equal("/cart", emptyCheckout.Headers.Location?.OriginalString);
        var fresh = NextUser();
        var registered = _app.CreateNoRedirectClient();
        var registeredFields = new Dictionary<string, string>
        {
            ["DisplayName"] = "測試會員",
            ["Email"] = fresh + "@example.com",
            ["UserName"] = fresh,
            ["Password"] = "Test@1234",
            ["ConfirmPassword"] = "Test@1234",
            ["Phone"] = NextPhone(),
            ["BirthDate"] = "1990-01-01",
            ["Gender"] = "Female",
            ["Country"] = "Taiwan",
            ["Region"] = "Taipei",
            ["City"] = "Taipei",
            ["Address"] = "Test address",
        };
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(registered, "/account/register", await TokenAsync(registered, "/account/register"), registeredFields)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(registered, "/account/login", await TokenAsync(registered, "/account/login"), new Dictionary<string, string> { ["account"] = fresh, ["password"] = "Test@1234" })).StatusCode);
        using (var deleteScope = _app.Services.CreateScope())
        {
            var deleteUsers = deleteScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var created = (await deleteUsers.FindByNameAsync(fresh))!;
            Assert.True((await deleteUsers.DeleteAsync(created)).Succeeded);
        }

        var gone = await registered.GetAsync("/account");
        Assert.Equal(HttpStatusCode.Redirect, gone.StatusCode);
        var seller = await LoginAsync("demo_seller");
        var ignored = await PostAsync(seller, "/seller/products/1", await TokenAsync(seller, "/account"), new Dictionary<string, string> { ["action"] = "noop" });
        Assert.Equal(HttpStatusCode.Redirect, ignored.StatusCode);
        var missing = await PostAsync(seller, "/seller/products/999999", await TokenAsync(seller, "/account"), new Dictionary<string, string> { ["action"] = "delete" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var op = await LoginAsync("demo_operator");
        Assert.Equal(HttpStatusCode.OK, (await op.GetAsync("/operator/sellers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await op.GetAsync("/operator/sellers?query=demo_customer")).StatusCode);
        var rejected = await PostAsync(op, "/operator/sellers", await TokenAsync(op, "/operator/sellers"), new Dictionary<string, string> { ["role"] = "Operator", ["action"] = "grant" });
        Assert.Contains("不能變更管理者", await rejected.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await PostAsync(op, "/operator/sellers", await TokenAsync(op, "/operator/sellers"), new Dictionary<string, string> { ["action"] = "noop" });
        var mailbox = _app.Services.GetRequiredService<MemoryMailbox>();
        mailbox.ThrowOnSend = true;
        try
        {
            var forgot = await PostAsync(anonymous, "/account/forgot", await TokenAsync(anonymous, "/account/forgot"), new Dictionary<string, string> { ["account"] = "demo_customer" });
            Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        }
        finally
        {
            mailbox.ThrowOnSend = false;
        }

        await using var colliding = new CollidingNumberApp();
        using (var scope = colliding.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var customer = (await users.FindByNameAsync("demo_customer"))!;
            db.CartLines.RemoveRange(await db.CartLines.Where(item => item.UserId == customer.Id).ToListAsync());
            var laptop = await db.Products.FirstAsync(item => item.Name == "Demo Laptop");
            if (laptop.Stock < 2)
            {
                laptop.Stock = 5;
            }

            await db.SaveChangesAsync();
        }

        var buyer = colliding.CreateNoRedirectClient();
        var loggedIn = await PostAsync(buyer, "/account/login", await TokenAsync(buyer, "/account/login"), new Dictionary<string, string> { ["account"] = "demo_customer", ["password"] = "Demo@1234" });
        Assert.Equal(HttpStatusCode.Redirect, loggedIn.StatusCode);
        using var idScope = colliding.Services.CreateScope();
        var id = await idScope.ServiceProvider.GetRequiredService<ShoppingDbContext>().Products.Where(item => item.Name == "Demo Laptop").Select(item => item.Id).FirstAsync();
        await PostAsync(buyer, "/cart", await TokenAsync(buyer, "/"), new Dictionary<string, string> { ["productId"] = id.ToString(CultureInfo.InvariantCulture), ["quantity"] = "1" });
        var first = await PostAsync(buyer, "/checkout", await TokenAsync(buyer, "/checkout"), new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await PostAsync(buyer, "/cart", await TokenAsync(buyer, "/"), new Dictionary<string, string> { ["productId"] = id.ToString(CultureInfo.InvariantCulture), ["quantity"] = "1" });
        var second = await PostAsync(buyer, "/checkout", await TokenAsync(buyer, "/checkout"), new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains("目前無法成立訂單", await second.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        foreach (var path in new[] { "/", "/categories", "/about", "/blog", "/contact" })
        {
            var page = await anonymous.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Contains("<h1", await page.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        var missingPage = await anonymous.GetAsync("/missing-page");
        Assert.Equal(HttpStatusCode.NotFound, missingPage.StatusCode);
        Assert.Contains("<h1", await missingPage.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>刪除示範資料後重新種入，並覆蓋建立失敗。</summary>
    [Fact]
    public async Task DemoSeedCreatesMissingRowsAndRejectsDuplicates()
    {
        using var scope = _app.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
        var customer = (await users.FindByNameAsync("demo_customer"))!;
        Assert.True((await users.SetUserNameAsync(customer, "tempname01")).Succeeded);
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync());
            Assert.Contains("DuplicateEmail", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            var restored = await users.FindByIdAsync(customer.Id);
            Assert.True((await users.SetUserNameAsync(restored!, "demo_customer")).Succeeded);
        }

        try
        {
            await DeleteDemoAsync();
            await seeder.SeedAsync();
            Assert.NotNull(await users.FindByNameAsync("demo_seller"));
            Assert.True(await db.Products.AnyAsync(item => item.Name == "Demo Laptop"));
        }
        finally
        {
            await seeder.SeedAsync();
        }
    }

    private static void ReadCard(ProductCard card)
    {
        Assert.True(card.Id > 0);
        Assert.False(string.IsNullOrEmpty(card.Name));
        Assert.True(card.Price > 0);
        Assert.False(string.IsNullOrEmpty(card.ImageUrl));
        Assert.False(string.IsNullOrEmpty(card.SellerName));
        Assert.False(string.IsNullOrEmpty(card.CategoryName));
        _ = card.Stock;
        _ = card.Keywords;
        _ = card.MaxQuantity;
    }

    private static void TouchModels(ShoppingDbContext db)
    {
        _ = db.Categories;
        _ = db.Products;
        _ = db.CartLines;
        _ = db.Orders;
        _ = db.OrderLines;
        _ = db.ContactMessages;
        _ = db.PasswordResetQuotas;
        var category = new Category { Id = 3, Name = "N", ImagePath = "/c.png" };
        var owner = new ApplicationUser { DisplayName = "S", UserName = "seller1", Email = "s@example.com", PhoneNumber = "9000000099", Country = "Taiwan", Region = "Taipei", City = "Taipei", Gender = "Male", Address = "A" };
        var product = new Product { Id = 4, Name = "P", Price = 1, ImageUrl = "/i", CategoryId = 3, Category = category, SellerId = "x", Seller = owner, Keywords = "k", Stock = 2 };
        category.Products.Add(product);
        var line = new CartLine { Id = 5, UserId = "u", ProductId = 4, Product = product, Quantity = 1 };
        var order = new ShopOrder { Id = 6, Number = "V1", UserId = "u", CreatedAt = DateTimeOffset.UtcNow, Total = 1 };
        var orderLine = new OrderLine { Id = 7, OrderId = 6, Order = order, ProductId = 4, ProductName = "P", UnitPrice = 1, Quantity = 1, LineTotal = 1 };
        order.Lines.Add(orderLine);
        var message = new ContactMessage { Id = 8, Name = "n", Email = "a@b.co", Message = "m", CreatedAt = DateTimeOffset.UtcNow };
        var quota = new PasswordResetQuota { UserId = "u", WindowStart = DateTimeOffset.UtcNow, Count = 1 };
        var row = new CartRow { ProductId = 4, Name = "P", Price = 1, Quantity = 1, ImageUrl = "/i", LineTotal = 1 };
        var cartPage = new CartPage { Lines = [row], Total = 1 };
        var checkout = new CheckoutPage { Cart = cartPage, DateText = "2026-10-04", Message = "unavailable", OrderNumber = "V1" };
        var summary = new OrderSummary { Number = "V1", DateText = "2026-10-04", Total = 1, Names = ["P"] };
        Assert.Equal("N", product.Category.Name);
        Assert.Equal("S", product.Seller.DisplayName);
        Assert.Equal("P", line.Product.Name);
        Assert.Same(order, orderLine.Order);
        Assert.Equal(8, message.Id);
        Assert.Equal(1, quota.Count);
        Assert.Equal("unavailable", checkout.Message);
        Assert.Equal("V1", summary.Number);
        Assert.Equal(4, cartPage.Lines[0].ProductId);
    }

    private async Task DeleteDemoAsync()
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShoppingDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var names = new[] { "demo_customer", "demo_seller", "demo_operator" };
        var ids = new List<string>();
        foreach (var name in names)
        {
            var user = await users.FindByNameAsync(name);
            if (user is not null)
            {
                ids.Add(user.Id);
            }
        }

        var carts = await db.CartLines.Where(item => ids.Contains(item.UserId)).ToListAsync();
        db.CartLines.RemoveRange(carts);
        var orders = await db.Orders.Where(item => ids.Contains(item.UserId)).Include(item => item.Lines).ToListAsync();
        db.OrderLines.RemoveRange(orders.SelectMany(item => item.Lines));
        db.Orders.RemoveRange(orders);
        var products = await db.Products.Where(item => ids.Contains(item.SellerId)).ToListAsync();
        var productIds = products.Select(item => item.Id).ToList();
        db.CartLines.RemoveRange(await db.CartLines.Where(item => productIds.Contains(item.ProductId)).ToListAsync());
        db.Products.RemoveRange(products);
        db.PasswordResetQuotas.RemoveRange(await db.PasswordResetQuotas.Where(item => ids.Contains(item.UserId)).ToListAsync());
        await db.SaveChangesAsync();
        foreach (var name in names)
        {
            var user = await users.FindByNameAsync(name);
            if (user is not null)
            {
                var deleted = await users.DeleteAsync(user);
                Assert.True(deleted.Succeeded, string.Join(",", deleted.Errors.Select(error => error.Code)));
            }
        }

        var seller = await roles.FindByNameAsync("Seller");
        if (seller is not null)
        {
            var removed = await roles.DeleteAsync(seller);
            Assert.True(removed.Succeeded, string.Join(",", removed.Errors.Select(error => error.Code)));
        }
    }

    private static async Task ClearCartAsync(ShoppingDbContext db, string userId)
    {
        var lines = await db.CartLines.Where(item => item.UserId == userId).ToListAsync();
        db.CartLines.RemoveRange(lines);
        await db.SaveChangesAsync();
    }

    private async Task<HttpClient> LoginAsync(string account)
    {
        var client = _app.CreateNoRedirectClient();
        var response = await PostAsync(client, "/account/login", await TokenAsync(client, "/account/login"), new Dictionary<string, string> { ["account"] = account, ["password"] = "Demo@1234" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static async Task<string> TokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"");
        Assert.True(match.Success, html);
        return match.Groups[1].Value;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string token, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = token;
        return client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    private static RegistrationForm Form(string user, string phone, Action<RegistrationForm>? edit = null)
    {
        var form = new RegistrationForm
        {
            DisplayName = "測試會員",
            Email = user + "@example.com",
            UserName = user,
            Password = "Test@1234",
            ConfirmPassword = "Test@1234",
            Phone = phone,
            BirthDate = "1990-01-01",
            Gender = "Female",
            Country = "Taiwan",
            Region = "Taipei",
            City = "Taipei",
            Address = "Test address",
        };
        edit?.Invoke(form);
        return form;
    }

    private static ProfileForm Profile(string phone = "9000000098")
    {
        return new ProfileForm { Phone = phone, Address = "Addr", Country = "Taiwan", Region = "Taipei", City = "Taipei" };
    }

    private static string NextUser()
    {
        return "g" + Guid.NewGuid().ToString("N")[..8];
    }

    private static string NextPhone()
    {
        return Interlocked.Increment(ref _phones).ToString(CultureInfo.InvariantCulture);
    }

    private static byte[] Png()
    {
        return [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00];
    }

    private sealed class ScriptedNumbers : IOrderNumberFactory
    {
        private readonly Queue<string> _numbers;

        public ScriptedNumbers(params string[] numbers)
        {
            _numbers = new Queue<string>(numbers);
        }

        public string Create()
        {
            return _numbers.Dequeue();
        }
    }

    private sealed class RepeatNumbers : IOrderNumberFactory
    {
        private string? _number;

        public string Create()
        {
            _number ??= "V" + Guid.NewGuid().ToString("N")[..26].ToUpperInvariant();
            return _number;
        }
    }

    private sealed class RejectPassword : IPasswordValidator<ApplicationUser>
    {
        public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "Rejected" }));
        }
    }

    private sealed class RejectingPasswordApp : ShopApp
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddSingleton<IPasswordValidator<ApplicationUser>>(new RejectPassword()));
        }
    }

    private sealed class CollidingNumberApp : ShopApp
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOrderNumberFactory>();
                services.AddSingleton<IOrderNumberFactory>(new RepeatNumbers());
            });
        }
    }
}
