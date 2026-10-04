using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Shopping.Data;

/// <summary>會員、賣家與管理者共用的登入帳號。</summary>
public sealed class ApplicationUser : IdentityUser
{
    /// <summary>顯示名稱。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>生日。</summary>
    public DateOnly DateOfBirth { get; set; }

    /// <summary>國家。</summary>
    public string Country { get; set; } = "";

    /// <summary>州或省。</summary>
    public string Region { get; set; } = "";

    /// <summary>城市。</summary>
    public string City { get; set; } = "";

    /// <summary>性別代碼：Male、Female 或 Others。</summary>
    public string Gender { get; set; } = "";

    /// <summary>地址。</summary>
    public string Address { get; set; } = "";
}

/// <summary>商品分類。</summary>
public sealed class Category
{
    /// <summary>識別碼。</summary>
    public int Id { get; set; }

    /// <summary>名稱。</summary>
    public string Name { get; set; } = "";

    /// <summary>站內圖片路徑。</summary>
    public string ImagePath { get; set; } = "";

    /// <summary>分類下的商品。</summary>
    public List<Product> Products { get; } = new();
}

/// <summary>賣家上架的商品。</summary>
public sealed class Product
{
    /// <summary>識別碼。</summary>
    public int Id { get; set; }

    /// <summary>名稱。</summary>
    public string Name { get; set; } = "";

    /// <summary>價格。</summary>
    public decimal Price { get; set; }

    /// <summary>圖片網址。</summary>
    public string ImageUrl { get; set; } = "";

    /// <summary>分類識別碼。</summary>
    public int CategoryId { get; set; }

    /// <summary>分類。</summary>
    public Category? Category { get; set; }

    /// <summary>賣家使用者識別碼。</summary>
    public string SellerId { get; set; } = "";

    /// <summary>賣家。</summary>
    public ApplicationUser? Seller { get; set; }

    /// <summary>搜尋關鍵字。</summary>
    public string? Keywords { get; set; }

    /// <summary>庫存。</summary>
    public int Stock { get; set; }
}

/// <summary>會員購物車的一列。</summary>
public sealed class CartLine
{
    /// <summary>識別碼。</summary>
    public int Id { get; set; }

    /// <summary>會員識別碼。</summary>
    public string UserId { get; set; } = "";

    /// <summary>商品識別碼。</summary>
    public int ProductId { get; set; }

    /// <summary>商品。</summary>
    public Product? Product { get; set; }

    /// <summary>數量。</summary>
    public int Quantity { get; set; }
}

/// <summary>一筆已成立的訂單。</summary>
public sealed class ShopOrder
{
    /// <summary>識別碼。</summary>
    public int Id { get; set; }

    /// <summary>對外編號。</summary>
    public string Number { get; set; } = "";

    /// <summary>下單會員。</summary>
    public string UserId { get; set; } = "";

    /// <summary>建立時間（UTC）。</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>總額。</summary>
    public decimal Total { get; set; }

    /// <summary>明細。</summary>
    public List<OrderLine> Lines { get; } = new();
}

/// <summary>訂單明細。品名與單價是下單當下的快照。</summary>
public sealed class OrderLine
{
    /// <summary>識別碼。</summary>
    public int Id { get; set; }

    /// <summary>訂單識別碼。</summary>
    public int OrderId { get; set; }

    /// <summary>訂單。</summary>
    public ShopOrder? Order { get; set; }

    /// <summary>商品識別碼。商品刪除後改為空值。</summary>
    public int? ProductId { get; set; }

    /// <summary>品名快照。</summary>
    public string ProductName { get; set; } = "";

    /// <summary>單價快照。</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>數量。</summary>
    public int Quantity { get; set; }

    /// <summary>小計。</summary>
    public decimal LineTotal { get; set; }
}

/// <summary>聯絡我們留言。</summary>
public sealed class ContactMessage
{
    /// <summary>識別碼。</summary>
    public int Id { get; set; }

    /// <summary>姓名。</summary>
    public string Name { get; set; } = "";

    /// <summary>Email。</summary>
    public string Email { get; set; } = "";

    /// <summary>內容。</summary>
    public string Message { get; set; } = "";

    /// <summary>寫入時間（UTC）。</summary>
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>重設密碼信的每小時配額。</summary>
public sealed class PasswordResetQuota
{
    /// <summary>使用者識別碼。</summary>
    public string UserId { get; set; } = "";

    /// <summary>視窗起點（UTC）。</summary>
    public DateTimeOffset WindowStart { get; set; }

    /// <summary>這個視窗已成功寄出的封數。</summary>
    public int Count { get; set; }
}

/// <summary>網站資料庫。</summary>
public sealed class ShoppingDbContext : IdentityDbContext<ApplicationUser>
{
    /// <summary>建立資料庫內容。</summary>
    public ShoppingDbContext(DbContextOptions<ShoppingDbContext> options)
        : base(options)
    {
    }

    /// <summary>分類。</summary>
    public DbSet<Category> Categories => Set<Category>();

    /// <summary>商品。</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>購物車。</summary>
    public DbSet<CartLine> CartLines => Set<CartLine>();

    /// <summary>訂單。</summary>
    public DbSet<ShopOrder> Orders => Set<ShopOrder>();

    /// <summary>訂單明細。</summary>
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    /// <summary>聯絡留言。</summary>
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    /// <summary>重設密碼配額。</summary>
    public DbSet<PasswordResetQuota> PasswordResetQuotas => Set<PasswordResetQuota>();

    /// <summary>SQL Server 的 NEWID()。每一列各算一次，只能在查詢裡轉譯。</summary>
    [DbFunction(Name = "NEWID", IsBuiltIn = true)]
    public static Guid NewId()
    {
        throw new InvalidOperationException("NEWID 只能由資料庫計算。");
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>(user =>
        {
            user.ToTable("shop_user");
            user.Property(item => item.UserName).HasMaxLength(20);
            user.Property(item => item.NormalizedUserName).HasMaxLength(20);
            user.Property(item => item.Email).HasMaxLength(50);
            user.Property(item => item.NormalizedEmail).HasMaxLength(50);
            user.Property(item => item.PhoneNumber).HasMaxLength(10);
            user.Property(item => item.DisplayName).HasMaxLength(50).IsRequired();
            user.Property(item => item.DateOfBirth).HasColumnType("date");
            user.Property(item => item.Country).HasMaxLength(50).IsRequired();
            user.Property(item => item.Region).HasMaxLength(50).IsRequired();
            user.Property(item => item.City).HasMaxLength(50).IsRequired();
            user.Property(item => item.Gender).HasMaxLength(20).IsRequired();
            user.Property(item => item.Address).HasMaxLength(500).IsRequired();
            user.HasIndex(item => item.PhoneNumber).IsUnique();
        });

        builder.Entity<Category>(category =>
        {
            category.ToTable("shop_category");
            category.Property(item => item.Name).HasMaxLength(50).IsRequired();
            category.Property(item => item.ImagePath).HasMaxLength(250).IsRequired();
            category.HasIndex(item => item.Name).IsUnique();
        });

        builder.Entity<Product>(product =>
        {
            product.ToTable("shop_product");
            product.Property(item => item.Name).HasMaxLength(50).IsRequired();
            product.Property(item => item.Price).HasColumnType("decimal(18,2)");
            product.Property(item => item.ImageUrl).HasMaxLength(250).IsRequired();
            product.Property(item => item.Keywords).HasMaxLength(500);
            product.HasIndex(item => item.Name).IsUnique();
            product.HasOne(item => item.Category).WithMany(item => item.Products).HasForeignKey(item => item.CategoryId);
            product.HasOne(item => item.Seller).WithMany().HasForeignKey(item => item.SellerId);
        });

        builder.Entity<CartLine>(line =>
        {
            line.ToTable("shop_cart_line");
            line.HasIndex(item => new { item.UserId, item.ProductId }).IsUnique();
            line.HasOne(item => item.Product).WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Cascade);
            line.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ShopOrder>(order =>
        {
            order.ToTable("shop_order");
            order.Property(item => item.Number).HasMaxLength(32).IsRequired();
            order.Property(item => item.Total).HasColumnType("decimal(18,2)");
            order.HasIndex(item => item.Number).IsUnique();
            order.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId);
        });

        builder.Entity<OrderLine>(line =>
        {
            line.ToTable("shop_order_line");
            line.Property(item => item.ProductName).HasMaxLength(50).IsRequired();
            line.Property(item => item.UnitPrice).HasColumnType("decimal(18,2)");
            line.Property(item => item.LineTotal).HasColumnType("decimal(18,2)");
            line.HasOne(item => item.Order).WithMany(item => item.Lines).HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Cascade);
            line.HasOne<Product>().WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ContactMessage>(message =>
        {
            message.ToTable("shop_contact_message");
            message.Property(item => item.Name).HasMaxLength(50).IsRequired();
            message.Property(item => item.Email).HasMaxLength(50).IsRequired();
            message.Property(item => item.Message).HasMaxLength(500).IsRequired();
        });

        builder.Entity<PasswordResetQuota>(quota =>
        {
            quota.ToTable("shop_password_reset_quota");
            quota.HasKey(item => item.UserId);
        });
    }
}
