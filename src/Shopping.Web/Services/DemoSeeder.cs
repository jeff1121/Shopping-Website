using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shopping.Data;

namespace Shopping.Web.Services;

/// <summary>示範帳號與商品。可重複執行。</summary>
public sealed class DemoSeeder
{
    /// <summary>示範密碼。只出現在種子與文件，資料庫存雜湊。</summary>
    public const string Password = "Demo@1234";

    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly ShoppingDbContext _db;
    private readonly string _imageBaseUrl;

    /// <summary>建立種子。</summary>
    public DemoSeeder(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, ShoppingDbContext db, string imageBaseUrl)
    {
        _users = users;
        _roles = roles;
        _db = db;
        _imageBaseUrl = imageBaseUrl;
    }

    /// <summary>圖片網址。沒有 IMAGE_BASE_URL 時用站內路徑。</summary>
    public static string ProductImage(string? baseUrl, string file)
    {
        if (string.IsNullOrEmpty(baseUrl))
        {
            return "/images/products/demo_seller/" + file;
        }

        return baseUrl.TrimEnd('/') + "/products/demo_seller/" + file;
    }

    /// <summary>補上角色、三個帳號與四項商品。</summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var role in new[] { "Customer", "Seller", "Operator" })
        {
            if (!await _roles.RoleExistsAsync(role))
            {
                await _roles.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureUserAsync("demo_customer", "demo_customer@example.com", "Demo Customer", "9000000001", new DateOnly(1995, 5, 5), "Female", "Demo customer address", seller: false, op: false);
        await EnsureUserAsync("demo_seller", "demo_seller@example.com", "Demo Seller", "9000000002", new DateOnly(1990, 1, 1), "Male", "Demo seller address", seller: true, op: false);
        await EnsureUserAsync("demo_operator", "demo_operator@example.com", "Demo Operator", "9000000003", new DateOnly(1985, 1, 1), "Others", "Demo operator address", seller: false, op: true);
        var seller = (await _users.FindByNameAsync("demo_seller"))!;
        await EnsureProductAsync("Demo Smart Watch", 2999m, "Computer Accessories", 20, "watch smart wearable demo", "watch.png", seller.Id, cancellationToken);
        await EnsureProductAsync("Demo Laser Printer", 8999m, "Computer Accessories", 5, "printer office demo", "printer.png", seller.Id, cancellationToken);
        await EnsureProductAsync("Demo Laptop", 32999m, "Computer", 10, "laptop notebook demo", "laptop.png", seller.Id, cancellationToken);
        await EnsureProductAsync("Demo Arcade Machine", 15999m, "Computer", 0, "arcade game demo", "arcade.png", seller.Id, cancellationToken);
    }

    private async Task EnsureUserAsync(string name, string email, string display, string phone, DateOnly birth, string gender, string address, bool seller, bool op)
    {
        if (await _users.FindByNameAsync(name) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = name,
            Email = email,
            EmailConfirmed = true,
            DisplayName = display,
            PhoneNumber = phone,
            DateOfBirth = birth,
            Gender = gender,
            Country = "Taiwan",
            Region = "Taipei",
            City = "Taipei",
            Address = address,
        };
        var created = await _users.CreateAsync(user, Password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", created.Errors.Select(error => error.Code)));
        }

        await _users.AddToRoleAsync(user, "Customer");
        if (seller)
        {
            await _users.AddToRoleAsync(user, "Seller");
        }

        if (op)
        {
            await _users.AddToRoleAsync(user, "Operator");
        }
    }

    private async Task EnsureProductAsync(string name, decimal price, string categoryName, int stock, string keywords, string file, string sellerId, CancellationToken cancellationToken)
    {
        if (await _db.Products.AnyAsync(item => item.Name == name, cancellationToken))
        {
            return;
        }

        var category = await _db.Categories.FirstAsync(item => item.Name == categoryName, cancellationToken);
        _db.Products.Add(new Product
        {
            Name = name,
            Price = price,
            CategoryId = category.Id,
            SellerId = sellerId,
            Keywords = keywords,
            ImageUrl = ProductImage(_imageBaseUrl, file),
            Stock = stock,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
