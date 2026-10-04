using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Shopping.Data;
using Shopping.Web.Localization;
using Shopping.Web.Security;
using Shopping.Web.Services;

namespace Shopping.Web.Controllers;

/// <summary>目錄、分類、關於與部落格。</summary>
public sealed class HomeController : Controller
{
    private readonly CatalogService _catalog;

    /// <summary>建立控制器。</summary>
    public HomeController(CatalogService catalog)
    {
        _catalog = catalog;
    }

    /// <summary>目錄。</summary>
    [HttpGet("/")]
    public async Task<IActionResult> Index(string? keyword, string? category, string? sort, CancellationToken cancellationToken)
    {
        int? categoryId = null;
        if (int.TryParse(category, out var parsed))
        {
            categoryId = parsed;
        }

        var products = await _catalog.SearchAsync(keyword, categoryId, sort, cancellationToken);
        ViewData["Keyword"] = keyword ?? "";
        ViewData["Sort"] = sort ?? "random";
        ViewData["CategoryId"] = categoryId;
        return View(products);
    }

    /// <summary>分類。</summary>
    [HttpGet("/categories")]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken)
    {
        return View(await _catalog.CategoriesAsync(cancellationToken));
    }

    /// <summary>關於。</summary>
    [HttpGet("/about")]
    public IActionResult About()
    {
        return View();
    }

    /// <summary>部落格。</summary>
    [HttpGet("/blog")]
    public IActionResult Blog()
    {
        return View();
    }
}

/// <summary>商品詳情。</summary>
public sealed class ProductController : Controller
{
    private readonly CatalogService _catalog;

    /// <summary>建立控制器。</summary>
    public ProductController(CatalogService catalog)
    {
        _catalog = catalog;
    }

    /// <summary>詳情。不存在是 404。</summary>
    [HttpGet("/products/{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken)
    {
        var product = await _catalog.FindAsync(id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        return View(product);
    }
}

/// <summary>帳戶。</summary>
public sealed class AccountController : Controller
{
    private readonly AccountService _accounts;
    private readonly LoginService _login;
    private readonly PasswordResetService _resets;
    private readonly InvoiceService _invoices;
    private readonly SellerService _seller;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IStringLocalizer<SharedResources> _text;

    /// <summary>建立控制器。</summary>
    public AccountController(AccountService accounts, LoginService login, PasswordResetService resets, InvoiceService invoices, SellerService seller, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, IStringLocalizer<SharedResources> text)
    {
        _accounts = accounts;
        _login = login;
        _resets = resets;
        _invoices = invoices;
        _seller = seller;
        _users = users;
        _signIn = signIn;
        _text = text;
    }

    /// <summary>註冊頁。</summary>
    [HttpGet("/account/register")]
    public IActionResult Register()
    {
        if (SignedIn())
        {
            return Redirect("/");
        }

        return View(new RegistrationForm());
    }

    /// <summary>註冊。</summary>
    [HttpPost("/account/register")]
    public async Task<IActionResult> RegisterPost(RegistrationForm form, CancellationToken cancellationToken)
    {
        if (SignedIn())
        {
            return Redirect("/");
        }

        var errors = await _accounts.RegisterAsync(form, cancellationToken);
        if (errors.Count > 0)
        {
            Apply(errors);
            return View("Register", form);
        }

        return Redirect("/account/login");
    }

    /// <summary>登入頁。</summary>
    [HttpGet("/account/login")]
    public IActionResult Login(string? returnUrl)
    {
        if (SignedIn())
        {
            return Redirect("/");
        }

        ViewData["ReturnUrl"] = ReturnUrls.LocalOrHome(returnUrl);
        return View();
    }

    /// <summary>登入。</summary>
    [HttpPost("/account/login")]
    public async Task<IActionResult> LoginPost(string? account, string? password, string? returnUrl)
    {
        if (SignedIn())
        {
            return Redirect("/");
        }

        var result = await _login.LoginAsync(account, password);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, _text["LoginFailed"].Value);
            ViewData["ReturnUrl"] = ReturnUrls.LocalOrHome(returnUrl);
            ViewData["Account"] = account ?? "";
            return View("Login");
        }

        return Redirect(ReturnUrls.LocalOrHome(returnUrl));
    }

    /// <summary>登出。</summary>
    [HttpPost("/account/logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return Redirect("/");
    }

    /// <summary>忘記密碼。</summary>
    [HttpGet("/account/forgot")]
    public IActionResult Forgot()
    {
        if (SignedIn())
        {
            return Redirect("/");
        }

        return View();
    }

    /// <summary>申請重設。</summary>
    [HttpPost("/account/forgot")]
    public async Task<IActionResult> ForgotPost(string? account, CancellationToken cancellationToken)
    {
        if (SignedIn())
        {
            return Redirect("/");
        }

        var ask = await _resets.RequestAsync(account, $"{Request.Scheme}://{Request.Host}/account/reset", cancellationToken);
        ViewData["ResetMessage"] = ask == ResetAsk.Unavailable ? "MailUnavailable" : "ForgotSent";
        return View("Forgot");
    }

    /// <summary>重設頁。</summary>
    [HttpGet("/account/reset")]
    public IActionResult Reset(string? user, string? token)
    {
        if (SignedIn())
        {
            return Redirect("/");
        }

        ViewData["UserId"] = user ?? "";
        ViewData["Token"] = token ?? "";
        return View();
    }

    /// <summary>設定新密碼。</summary>
    [HttpPost("/account/reset")]
    public async Task<IActionResult> ResetPost(string? user, string? token, string? password, string? confirmPassword, CancellationToken cancellationToken)
    {
        if (SignedIn())
        {
            return Redirect("/");
        }

        var errors = await _resets.ResetAsync(user, token, password, confirmPassword, cancellationToken);
        if (errors.Count > 0)
        {
            Apply(errors);
            ViewData["UserId"] = user ?? "";
            ViewData["Token"] = token ?? "";
            return View("Reset");
        }

        ViewData["ResetDone"] = true;
        return View("Reset");
    }

    /// <summary>個人資料。</summary>
    [HttpGet("/account")]
    [Authorize]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var id = UserId();
        var user = await _users.FindByIdAsync(id);
        if (user is null)
        {
            return Challenge();
        }

        ViewData["Orders"] = await _invoices.ListAsync(id, cancellationToken);
        ViewData["Products"] = User.IsInRole("Seller") ? await _seller.MineAsync(id, cancellationToken) : Array.Empty<Product>();
        return View("Index", user);
    }

    /// <summary>更新個人資料。</summary>
    [HttpPost("/account")]
    [Authorize]
    public async Task<IActionResult> IndexPost(ProfileForm form, CancellationToken cancellationToken)
    {
        var id = UserId();
        var errors = await _accounts.UpdateProfileAsync(id, form, cancellationToken);
        if (errors.Count > 0)
        {
            Apply(errors);
        }

        return await Index(cancellationToken);
    }

    /// <summary>訂單 PDF。</summary>
    [HttpGet("/account/orders/{number}/invoice")]
    [Authorize]
    public async Task<IActionResult> Invoice(string number, CancellationToken cancellationToken)
    {
        var bytes = await _invoices.RenderAsync(UserId(), number, cancellationToken);
        if (bytes is null)
        {
            return NotFound();
        }

        return File(bytes, "application/pdf", "OrderInvoice.pdf");
    }

    private bool SignedIn()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) is not null;
    }

    private string UserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    }

    private void Apply(IReadOnlyDictionary<string, string> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(error.Key, _text[error.Value].Value);
        }
    }
}

/// <summary>購物車。</summary>
[Authorize]
public sealed class CartController : Controller
{
    private readonly CartService _cart;

    /// <summary>建立控制器。</summary>
    public CartController(CartService cart)
    {
        _cart = cart;
    }

    /// <summary>購物車。</summary>
    [HttpGet("/cart")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await _cart.ReadAsync(UserId(), cancellationToken));
    }

    /// <summary>加入。</summary>
    [HttpPost("/cart")]
    public async Task<IActionResult> Add(int productId, int quantity, CancellationToken cancellationToken)
    {
        var status = await _cart.AddAsync(UserId(), productId, quantity, cancellationToken);
        if (status == CartAddStatus.Ok)
        {
            return Redirect("/cart");
        }

        return Redirect("/products/" + productId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>更新或移除。</summary>
    [HttpPost("/cart/{productId:int}")]
    public async Task<IActionResult> Change(int productId, string? action, int quantity, CancellationToken cancellationToken)
    {
        if (action == "remove")
        {
            await _cart.RemoveAsync(UserId(), productId, cancellationToken);
        }
        else
        {
            await _cart.UpdateAsync(UserId(), productId, quantity, cancellationToken);
        }

        return Redirect("/cart");
    }

    private string UserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    }
}

/// <summary>結帳。</summary>
[Authorize]
public sealed class CheckoutController : Controller
{
    private readonly CheckoutService _checkout;
    private readonly CartService _cart;
    private readonly TimeProvider _clock;

    /// <summary>建立控制器。</summary>
    public CheckoutController(CheckoutService checkout, CartService cart, TimeProvider clock)
    {
        _checkout = checkout;
        _cart = cart;
        _clock = clock;
    }

    /// <summary>結帳頁。空購物車回購物車。</summary>
    [HttpGet("/checkout")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var cart = await _cart.ReadAsync(UserId(), cancellationToken);
        if (cart.Lines.Count == 0)
        {
            return Redirect("/cart");
        }

        return View(Page(cart, null, null));
    }

    /// <summary>成立訂單。</summary>
    [HttpPost("/checkout")]
    public async Task<IActionResult> Place(CancellationToken cancellationToken)
    {
        var result = await _checkout.PlaceAsync(UserId(), cancellationToken);
        if (result is CheckoutResult.Empty)
        {
            return Redirect("/cart");
        }

        var cart = await _cart.ReadAsync(UserId(), cancellationToken);
        if (result is CheckoutResult.Placed placed)
        {
            return View("Index", Page(cart, null, placed.Number));
        }

        if (result is CheckoutResult.Insufficient insufficient)
        {
            return View("Index", Page(cart, insufficient.ProductName, null));
        }

        return View("Index", Page(cart, "unavailable", null));
    }

    private string UserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    }

    private CheckoutPage Page(CartPage cart, string? message, string? number)
    {
        return new CheckoutPage { Cart = cart, DateText = TaipeiClock.Format(_clock.GetUtcNow()), Message = message, OrderNumber = number };
    }
}

/// <summary>賣家上架與維護。</summary>
[Authorize(Roles = "Seller")]
public sealed class SellerController : Controller
{
    private readonly SellerService _seller;
    private readonly CatalogService _catalog;
    private readonly IStringLocalizer<SharedResources> _text;

    /// <summary>建立控制器。</summary>
    public SellerController(SellerService seller, CatalogService catalog, IStringLocalizer<SharedResources> text)
    {
        _seller = seller;
        _catalog = catalog;
        _text = text;
    }

    /// <summary>上架頁。</summary>
    [HttpGet("/seller/products/new")]
    public async Task<IActionResult> New(CancellationToken cancellationToken)
    {
        ViewData["Categories"] = await _catalog.CategoriesAsync(cancellationToken);
        return View(new ProductForm());
    }

    /// <summary>上架。</summary>
    [HttpPost("/seller/products/new")]
    public async Task<IActionResult> NewPost(ProductForm form, IFormFile? image, CancellationToken cancellationToken)
    {
        Stream? stream = null;
        string? fileName = null;
        if (image is not null)
        {
            stream = image.OpenReadStream();
            fileName = image.FileName;
        }

        var errors = await _seller.CreateAsync(UserId(), User.Identity!.Name!, form, stream, fileName, cancellationToken);
        if (stream is not null)
        {
            await stream.DisposeAsync();
        }
        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                ModelState.AddModelError(error.Key, _text[error.Value].Value);
            }

            ViewData["Categories"] = await _catalog.CategoriesAsync(cancellationToken);
            return View("New", form);
        }

        return Redirect("/account");
    }

    /// <summary>修改或刪除。</summary>
    [HttpPost("/seller/products/{id:int}")]
    public async Task<IActionResult> Edit(int id, string? action, string? price, string? stock, string? keywords, bool confirm, CancellationToken cancellationToken)
    {
        SellerEditStatus status;
        if (action == "delete")
        {
            status = await _seller.DeleteAsync(UserId(), id, confirm, cancellationToken);
        }
        else if (action == "update")
        {
            status = await _seller.UpdateAsync(UserId(), id, price, stock, keywords, cancellationToken);
        }
        else
        {
            return Redirect("/account");
        }

        if (status == SellerEditStatus.NotFound)
        {
            return NotFound();
        }

        return Redirect("/account");
    }

    private string UserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    }
}

/// <summary>管理者授予賣家。</summary>
[Authorize(Roles = "Operator")]
public sealed class OperatorController : Controller
{
    private readonly OperatorService _operators;

    /// <summary>建立控制器。</summary>
    public OperatorController(OperatorService operators)
    {
        _operators = operators;
    }

    /// <summary>尋找會員。</summary>
    [HttpGet("/operator/sellers")]
    public async Task<IActionResult> Index(string? query)
    {
        ViewData["Query"] = query ?? "";
        ViewData["Member"] = string.IsNullOrWhiteSpace(query) ? null : await _operators.FindAsync(query);
        return View("Index");
    }

    /// <summary>變更角色。Operator 一律拒絕。</summary>
    [HttpPost("/operator/sellers")]
    public async Task<IActionResult> Change(string? targetId, string? role, string? action, string? query)
    {
        if (role == "Operator")
        {
            var actor = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _operators.ChangeOperatorAsync(actor, targetId ?? "", action == "grant");
            ViewData["Rejected"] = true;
        }
        else if (action == "grant" && !string.IsNullOrEmpty(targetId))
        {
            await _operators.ChangeSellerAsync(targetId, true);
        }
        else if (action == "revoke" && !string.IsNullOrEmpty(targetId))
        {
            await _operators.ChangeSellerAsync(targetId, false);
        }

        return await Index(query);
    }
}

/// <summary>聯絡我們。</summary>
public sealed class ContactController : Controller
{
    private readonly ContactService _contact;
    private readonly IStringLocalizer<SharedResources> _text;

    /// <summary>建立控制器。</summary>
    public ContactController(ContactService contact, IStringLocalizer<SharedResources> text)
    {
        _contact = contact;
        _text = text;
    }

    /// <summary>表單。</summary>
    [HttpGet("/contact")]
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>送出。</summary>
    [HttpPost("/contact")]
    public async Task<IActionResult> IndexPost(string? name, string? email, string? message, CancellationToken cancellationToken)
    {
        var errors = await _contact.SendAsync(name, email, message, cancellationToken);
        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                ModelState.AddModelError(error.Key, _text[error.Value].Value);
            }

            ViewData["Name"] = name ?? "";
            ViewData["Email"] = email ?? "";
            ViewData["Message"] = message ?? "";
            return View("Index");
        }

        ViewData["Sent"] = true;
        return View("Index");
    }
}

/// <summary>語系 Cookie。</summary>
public sealed class CultureController : Controller
{
    /// <summary>寫入語系並回到站內網址。</summary>
    [HttpPost("/culture")]
    public IActionResult Set(string? culture, string? returnUrl)
    {
        var normalized = CultureCodes.Normalize(culture);
        Response.Cookies.Append(
            Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName,
            Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(new Microsoft.AspNetCore.Localization.RequestCulture(normalized)),
            new CookieOptions { IsEssential = true, Secure = true, SameSite = SameSiteMode.Lax, HttpOnly = false, Path = "/" });
        return Redirect(ReturnUrls.LocalOrHome(returnUrl));
    }
}

/// <summary>錯誤頁。不讀資料庫。</summary>
public sealed class ErrorController : Controller
{
    /// <summary>依狀態碼顯示一般文案。</summary>
    [Route("/error/{code:int}")]
    public IActionResult Status(int code)
    {
        Response.StatusCode = code;
        if (code == 500)
        {
            return View("Error500");
        }

        if (code == 403)
        {
            return View("Error403");
        }

        return View("Error404");
    }
}
