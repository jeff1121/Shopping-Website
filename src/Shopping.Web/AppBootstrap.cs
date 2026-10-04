using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Shopping.Data;
using Shopping.Web.Security;
using Shopping.Web.Services;

namespace Shopping.Web;

/// <summary>建立與設定網站。</summary>
public static class AppBootstrap
{
    /// <summary>建立組態與 Kestrel。</summary>
    public static WebApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(ApplyKestrelLimits);
        return builder;
    }

    /// <summary>關閉伺服器版本標頭，並把請求本文上限設為 5 MB。</summary>
    public static void ApplyKestrelLimits(Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions options)
    {
        options.AddServerHeader = false;
        options.Limits.MaxRequestBodySize = 5 * 1024 * 1024;
    }

    /// <summary>組裝網站。正式環境缺變數時在 Run 之前拋出。</summary>
    public static WebApplication Build(string[] args)
    {
        var builder = CreateBuilder(args);
        var settings = AppSettings.Load(builder.Environment, builder.Configuration);
        Register(builder, settings);
        var app = builder.Build();
        Initialize(app);
        ConfigurePipeline(app, settings);
        return app;
    }

    /// <summary>設定中介層。</summary>
    public static void ConfigurePipeline(WebApplication app, AppSettings settings)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/error/500");
        }

        app.UseStatusCodePagesWithReExecute("/error/{0}");
        if (app.Environment.IsProduction())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseMiddleware<CultureCookieMiddleware>();
        app.UseRequestLocalization();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.MapControllers();
        if (settings.ExposeFault)
        {
            app.MapGet("/test/fault", (HttpContext _) => throw new InvalidOperationException("測試用例外"));
        }
    }

    private static void Register(WebApplicationBuilder builder, AppSettings settings)
    {
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddDbContext<ShoppingDbContext>(options => options.UseSqlServer(settings.AppConnectionString));
        builder.Services.AddSingleton(new DatabaseMigrator(settings.MigratorConnectionString, DatabaseMigrator.LoadFromAssembly(typeof(DatabaseMigrator).Assembly)));
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromSeconds(settings.LockoutSeconds);
                options.Lockout.AllowedForNewUsers = true;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<ShoppingDbContext>()
            .AddDefaultTokenProviders();
        builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromSeconds(settings.PasswordResetTokenSeconds);
        });
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/account/login";
            options.AccessDeniedPath = "/error/403";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
        builder.Services.AddAntiforgery(options => options.SuppressXFrameOptionsHeader = true);
        builder.Services.AddHsts(options => options.ExcludedHosts.Clear());
        builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 5 * 1024 * 1024);
        var cultures = new[] { new CultureInfo("zh-Hant"), new CultureInfo("en") };
        builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture("zh-Hant");
            options.SupportedCultures = cultures;
            options.SupportedUICultures = cultures;
            options.RequestCultureProviders = [new QueryStringRequestCultureProvider(), new CookieRequestCultureProvider()];
        });
        builder.Services.AddLocalization();
        builder.Services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement, UnicodeRanges.CjkUnifiedIdeographs, UnicodeRanges.CjkSymbolsandPunctuation, UnicodeRanges.HalfwidthandFullwidthForms));
        builder.Services.AddSingleton(ImageStore.CreateClient(settings.BlobEndpoint, settings.Container, settings.AzureClientId));
        builder.Services.AddSingleton(sp => new ImageStore(sp.GetRequiredService<Azure.Storage.Blobs.BlobContainerClient>(), settings.ImageBaseUrl));
        if (builder.Environment.IsProduction())
        {
            builder.Services.AddSingleton<IShopMailer>(new SmtpShopMailer(settings));
        }
        else
        {
            builder.Services.AddSingleton<MemoryMailbox>();
            builder.Services.AddSingleton<IShopMailer>(sp => sp.GetRequiredService<MemoryMailbox>());
        }

        builder.Services.AddSingleton<IOrderNumberFactory, UlidOrderNumberFactory>();
        builder.Services.AddSingleton<IResetLog, ResetLog>();
        builder.Services.AddScoped<CatalogService>();
        builder.Services.AddScoped<CartService>();
        builder.Services.AddScoped<CheckoutService>();
        builder.Services.AddScoped<AccountService>();
        builder.Services.AddScoped<LoginService>();
        builder.Services.AddScoped<PasswordResetService>();
        builder.Services.AddScoped<SellerService>();
        builder.Services.AddScoped<OperatorService>();
        builder.Services.AddScoped<ContactService>();
        builder.Services.AddScoped<InvoiceService>();
        builder.Services.AddScoped(sp => new DemoSeeder(
            sp.GetRequiredService<UserManager<ApplicationUser>>(),
            sp.GetRequiredService<RoleManager<IdentityRole>>(),
            sp.GetRequiredService<ShoppingDbContext>(),
            settings.ImageBaseUrl));
        builder.Services.AddScoped<CartCountFilter>();
        builder.Services.AddControllersWithViews(options =>
        {
            options.Filters.AddService<CartCountFilter>();
            options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
        })
            .AddRazorRuntimeCompilation();
    }

    private static void Initialize(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<DatabaseMigrator>().ApplyAsync().GetAwaiter().GetResult();
        services.GetRequiredService<DemoSeeder>().SeedAsync().GetAwaiter().GetResult();
        services.GetRequiredService<ImageStore>().EnsureContainerAsync().GetAwaiter().GetResult();
    }
}
