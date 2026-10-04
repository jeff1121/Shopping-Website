using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Shopping.Data;
using Shopping.Web.Security;
using UglyToad.PdfPig;

namespace Shopping.Web.Tests;

/// <summary>不需要網站主機的規則與報告。</summary>
public sealed class PureTests
{
    /// <summary>NEWID 對應不能在用戶端執行。</summary>
    [Fact]
    public void NewIdIsDatabaseOnly()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ShoppingDbContext.NewId());
        Assert.Contains("NEWID", error.Message, StringComparison.Ordinal);
    }

    /// <summary>顯示名稱、帳號、電話與性別的邊界。</summary>
    [Fact]
    public void InputRulesAcceptTheDocumentedShapes()
    {
        Assert.True(InputRules.TryDisplayName(" 陳小明 ", out var name));
        Assert.Equal("陳小明", name);
        Assert.False(InputRules.TryDisplayName(null, out _));
        Assert.False(InputRules.TryDisplayName("bad1", out _));
        Assert.False(InputRules.TryDisplayName("", out _));
        Assert.False(InputRules.TryDisplayName("   ", out _));
        Assert.False(InputRules.TryDisplayName(new string('名', 51), out _));
        Assert.True(InputRules.TryDisplayName("A B", out _));
        Assert.False(InputRules.IsUserName(null));
        Assert.False(InputRules.IsUserName(""));
        Assert.False(InputRules.IsUserName(new string('a', 21)));
        Assert.False(InputRules.IsUserName("has_underscore"));
        Assert.False(InputRules.IsUserName("["));
        Assert.False(InputRules.IsUserName("{"));
        Assert.False(InputRules.IsUserName(" "));
        Assert.True(InputRules.IsUserName("aA1"));
        Assert.True(InputRules.IsUserName("abc123"));
        Assert.False(InputRules.IsPhone(null));
        Assert.False(InputRules.IsPhone("123"));
        Assert.False(InputRules.IsPhone("/234567890"));
        Assert.False(InputRules.IsPhone("123456789a"));
        Assert.True(InputRules.IsPhone("0912345678"));
        Assert.False(InputRules.IsGender(null));
        Assert.True(InputRules.IsGender("Male"));
        Assert.True(InputRules.IsGender("Female"));
        Assert.True(InputRules.IsGender("Others"));
        Assert.False(InputRules.IsGender("nope"));
    }

    /// <summary>Email 接受較長的頂層網域，並拒絕空白、過長、多個 @ 與格式錯誤。</summary>
    [Fact]
    public void EmailRules()
    {
        Assert.True(InputRules.TryEmail(" person@example.museum ", out var email));
        Assert.Equal("person@example.museum", email);
        Assert.False(InputRules.TryEmail("  ", out _));
        Assert.False(InputRules.TryEmail(new string('a', 40) + "@example.com", out _));
        Assert.False(InputRules.TryEmail("a@b@c.com", out _));
        Assert.False(InputRules.TryEmail("not-an-email", out _));
        Assert.False(InputRules.TryEmail("a@", out _));
        Assert.False(InputRules.TryEmail("Person <user@example.com>", out _));
    }

    /// <summary>密碼必須同時有大小寫、數字與指定符號。</summary>
    [Fact]
    public void PasswordRules()
    {
        Assert.False(InputRules.IsPassword(null));
        Assert.False(InputRules.IsPassword(""));
        Assert.False(InputRules.IsPassword("Ab1!"));
        Assert.False(InputRules.IsPassword("abcdefg1!"));
        Assert.False(InputRules.IsPassword("ABCDEFG1!"));
        Assert.False(InputRules.IsPassword("Abcdefgh!"));
        Assert.False(InputRules.IsPassword("Abcdefg1"));
        Assert.False(InputRules.IsPassword("Abcdefg1 "));
        Assert.True(InputRules.IsPassword("Abcdef1@"));
        Assert.True(InputRules.IsPassword("Abcdef1$"));
        Assert.True(InputRules.IsPassword("Abcdef1!"));
        Assert.True(InputRules.IsPassword("Abcdef1%"));
        Assert.True(InputRules.IsPassword("Abcdef1*"));
        Assert.True(InputRules.IsPassword("Abcdef1?"));
        Assert.True(InputRules.IsPassword("Abcdef1&"));
    }

    /// <summary>生日、必填、價格、庫存與關鍵字。</summary>
    [Fact]
    public void ValueRules()
    {
        var today = new DateOnly(2026, 10, 4);
        Assert.False(InputRules.TryBirthDate(" ", today, out _));
        Assert.False(InputRules.TryBirthDate("nope", today, out _));
        Assert.False(InputRules.TryBirthDate("2026-10-05", today, out _));
        Assert.True(InputRules.TryBirthDate("2026-10-04", today, out var date));
        Assert.Equal(today, date);
        Assert.False(InputRules.TryRequired(" ", 10, out _));
        Assert.False(InputRules.TryRequired("123456", 5, out _));
        Assert.True(InputRules.TryRequired(" ok ", 5, out var text));
        Assert.Equal("ok", text);
        Assert.False(InputRules.TryProductName(null, out _));
        Assert.True(InputRules.TryProductName("Laptop", out _));
        Assert.False(InputRules.TryPrice(" ", out _));
        Assert.False(InputRules.TryPrice("abc", out _));
        Assert.False(InputRules.TryPrice("-1", out _));
        Assert.False(InputRules.TryPrice("1.234", out _));
        Assert.True(InputRules.TryPrice("1.20", out var price));
        Assert.Equal(1.20m, price);
        Assert.False(InputRules.IsPrice(-1));
        Assert.True(InputRules.IsPrice(0));
        Assert.False(InputRules.TryStock("abc", out _));
        Assert.False(InputRules.TryStock("-1", out _));
        Assert.True(InputRules.TryStock("0", out var stock));
        Assert.Equal(0, stock);
        Assert.True(InputRules.IsKeywords(null));
        Assert.True(InputRules.IsKeywords("watch"));
        Assert.False(InputRules.IsKeywords(new string('k', 501)));
    }

    /// <summary>站內網址、語系與台北日期。</summary>
    [Fact]
    public void NavigationAndClock()
    {
        Assert.Equal("/", ReturnUrls.LocalOrHome(null));
        Assert.Equal("/", ReturnUrls.LocalOrHome(""));
        Assert.Equal("/cart", ReturnUrls.LocalOrHome("/cart"));
        Assert.Equal("/", ReturnUrls.LocalOrHome("//evil"));
        Assert.Equal("/", ReturnUrls.LocalOrHome("https://evil.test"));
        Assert.Equal("en", CultureCodes.Normalize("en"));
        Assert.Equal("zh-Hant", CultureCodes.Normalize("zh-Hant"));
        Assert.Equal("zh-Hant", CultureCodes.Normalize("fr"));
        Assert.Equal("zh-Hant", CultureCodes.Normalize(null));
        var limits = new Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions();
        AppBootstrap.ApplyKestrelLimits(limits);
        Assert.False(limits.AddServerHeader);
        Assert.Equal(5 * 1024 * 1024, limits.Limits.MaxRequestBodySize);
        Assert.Equal(CancellationToken.None, Program.ExitToken());
        Environment.SetEnvironmentVariable("SHOP_EXIT_AFTER_START", "1");
        try
        {
            Assert.True(Program.ExitToken().CanBeCanceled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHOP_EXIT_AFTER_START", null);
        }
        var text = TaipeiClock.Format(new DateTimeOffset(2026, 10, 4, 16, 30, 0, TimeSpan.Zero));
        Assert.Equal("2026-10-05", text);
        Assert.Equal(new DateOnly(2026, 10, 4), TaipeiClock.UtcToday(new FixedTime(new DateTimeOffset(2026, 10, 4, 1, 0, 0, TimeSpan.Zero))));
        var number = new UlidOrderNumberFactory().Create();
        Assert.Equal(27, number.Length);
        Assert.StartsWith("V", number, StringComparison.Ordinal);
    }

    /// <summary>圖片副檔名與檔頭的每一種分支。</summary>
    [Fact(DisplayName = "FR-83 image signatures")]
    public void ImageSignaturesCoverEveryBranch()
    {
        Assert.False(ImageSignatures.IsSupportedExtension(null));
        Assert.False(ImageSignatures.IsSupportedExtension(""));
        Assert.True(ImageSignatures.IsSupportedExtension(".JPG"));
        Assert.True(ImageSignatures.IsSupportedExtension(".jpeg"));
        Assert.True(ImageSignatures.IsSupportedExtension(".png"));
        Assert.True(ImageSignatures.IsSupportedExtension(".GIF"));
        Assert.True(ImageSignatures.IsSupportedExtension(".webp"));
        Assert.False(ImageSignatures.IsSupportedExtension(".txt"));
        Assert.False(ImageSignatures.HasValidSignature(new MemoryStream([1, 2]), ".txt"));
        Assert.False(ImageSignatures.HasValidSignature(new MemoryStream([1]), ".jpg"));
        Assert.True(ImageSignatures.HasValidSignature(new MemoryStream([0xFF, 0xD8, 0xFF, 0x00]), ".jpg"));
        Assert.False(ImageSignatures.HasValidSignature(new MemoryStream([0x00, 0xD8, 0xFF]), ".jpeg"));
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Assert.True(ImageSignatures.HasValidSignature(new MemoryStream(png), ".png"));
        Assert.False(ImageSignatures.HasValidSignature(new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x00, 0x00]), ".png"));
        Assert.True(ImageSignatures.HasValidSignature(new MemoryStream("GIF87a rest"u8.ToArray()), ".gif"));
        Assert.True(ImageSignatures.HasValidSignature(new MemoryStream("GIF89a rest"u8.ToArray()), ".gif"));
        Assert.False(ImageSignatures.HasValidSignature(new MemoryStream("GIF99a rest"u8.ToArray()), ".gif"));
        var webp = new byte[16];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(webp, 0);
        Encoding.ASCII.GetBytes("WEBP").CopyTo(webp, 8);
        Assert.True(ImageSignatures.HasValidSignature(new MemoryStream(webp), ".webp"));
        var notWebp = new byte[16];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(notWebp, 0);
        Assert.False(ImageSignatures.HasValidSignature(new MemoryStream(notWebp), ".webp"));
        Assert.False(ImageSignatures.HasValidSignature(new MemoryStream([0x00, 0x00, 0x00, 0x00]), ".webp"));
        var stuck = new StuckStream([0xFF, 0xD8, 0xFF, 0x00]);
        Assert.True(ImageSignatures.HasValidSignature(stuck, ".jpg"));
        Assert.Equal("image/jpeg", ImageStore.ContentTypeFor(".jpg"));
        Assert.Equal("image/jpeg", ImageStore.ContentTypeFor(".JPEG"));
        Assert.Equal("image/png", ImageStore.ContentTypeFor(".png"));
        Assert.Equal("image/gif", ImageStore.ContentTypeFor(".gif"));
        Assert.Equal("image/webp", ImageStore.ContentTypeFor(".webp"));
        Assert.Throws<InvalidOperationException>(() => ImageStore.ContentTypeFor(".txt"));
    }

    /// <summary>PDF 含訂單編號、品名與總額。</summary>
    [Fact]
    public void InvoicePdfContainsTheOrder()
    {
        var bytes = InvoicePdf.Render("VORDER", "2026-10-04", [new InvoiceLine("Demo Laptop", 10m, 2, 20m)], 20m);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes[..4]), StringComparison.Ordinal);
        using var document = PdfDocument.Open(bytes);
        var text = string.Join('\n', document.GetPages().Select(page => page.Text));
        Assert.Contains("VORDER", text, StringComparison.Ordinal);
        Assert.Contains("Demo Laptop", text, StringComparison.Ordinal);
        Assert.Contains("20.00", text, StringComparison.Ordinal);
        var empty = InvoicePdf.Render("VEMPTY", "2026-10-04", [], 0m);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(empty[..4]), StringComparison.Ordinal);
    }

    /// <summary>覆蓋率判斷的失敗原因。</summary>
    [Fact]
    public void CoverageReportRejectsIncompleteXml()
    {
        Assert.Contains("不是 Cobertura", CoverageReport.FailureReason("<not"), StringComparison.Ordinal);
        Assert.Equal("缺少套件 Shopping.Web", CoverageReport.FailureReason("<coverage></coverage>"));
        Assert.Equal("缺少套件 Shopping.Data", CoverageReport.FailureReason(Report(Package("Shopping.Web", "1", "1"))));
        Assert.Contains("line-rate", CoverageReport.FailureReason(Report(Package("Shopping.Web", "0.999", "1"), Package("Shopping.Data", "1", "1"))), StringComparison.Ordinal);
        Assert.Contains("branch-rate", CoverageReport.FailureReason(Report(Package("Shopping.Web", "1", null), Package("Shopping.Data", "1", "1"))), StringComparison.Ordinal);
        Assert.Contains("line-rate", CoverageReport.FailureReason(Report(Package("Shopping.Web", "nope", "1"), Package("Shopping.Data", "1", "1"))), StringComparison.Ordinal);
        Assert.Contains("line-rate", CoverageReport.FailureReason(Report(Package("Shopping.Web", null, "1"), Package("Shopping.Data", "1", "1"))), StringComparison.Ordinal);
        Assert.Null(CoverageReport.FailureReason(Report(Package("Shopping.Web", "1", "1"), Package("Shopping.Web.Controllers", "1", "1"), Package("Shopping.Data", "1", "1"), "<package name=\"Other\" line-rate=\"0\" branch-rate=\"0\" />")));
        Assert.Equal("缺少套件 Shopping.Web", CoverageReport.FailureReason("<coverage><package line-rate=\"1\" branch-rate=\"1\" /></coverage>"));
    }

    /// <summary>追溯缺少編號、檔案不存在與空白計畫。</summary>
    [Fact]
    public void RequirementTraceReportsGaps()
    {
        var required = new HashSet<string>(StringComparer.Ordinal) { "FR-01", "FR-02" };
        var blank = RequirementTrace.Check("  ", "FR-01", "FR-01", required);
        Assert.True(blank.Unreadable);
        var missing = RequirementTrace.Check("see FR-01 and FR-02", "FR-01", null, required);
        Assert.Contains("FR-02", missing.MissingUnit);
        Assert.Contains("FR-01", missing.MissingE2E);
        Assert.False(missing.Unreadable);
        var gaps = RequirementTrace.Check("x FR-1 FA FRX1 FR- FR-7yy FR-12", null, "", required);
        Assert.Contains("FR-1", gaps.MissingUnit);
        Assert.Contains("FR-7", gaps.MissingUnit);
        Assert.Contains("FR-12", gaps.MissingUnit);
        var tiny = RequirementTrace.Check("FR", "", "FR-01", required);
        Assert.Empty(tiny.MissingUnit);
        var ok = RequirementTrace.Check("FR-01 FR-09", "FR-01 FR-09", "FR-01", required);
        Assert.Empty(ok.MissingUnit);
        Assert.Empty(ok.MissingE2E);
        var path = Path.Combine(Path.GetTempPath(), "missing-trace-" + Guid.NewGuid().ToString("N") + ".txt");
        Assert.Null(RequirementTrace.ReadIfExists(path));
        File.WriteAllText(path, "FR-01");
        try
        {
            Assert.Equal("FR-01", RequirementTrace.ReadIfExists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>設定載入的正式環境、預設值與連線字串。</summary>
    [Fact]
    public void AppSettingsCoverEnvironmentBranches()
    {
        var snapshot = Snapshot();
        try
        {
            ClearRequired();
            var production = new HostEnv("Production");
            var missing = Assert.Throws<InvalidOperationException>(() => AppSettings.Load(production, new ConfigurationBuilder().Build()));
            Assert.Contains("SQL_SERVER", missing.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("Test_only", missing.Message, StringComparison.Ordinal);
            Environment.SetEnvironmentVariable("SQL_SERVER", "set");
            var partial = AppSettings.MissingVariables();
            Assert.Contains("SQL_DATABASE", partial);
            Assert.DoesNotContain("SQL_SERVER", partial);
            SetRequired();
            var loaded = AppSettings.Load(production, new ConfigurationBuilder().Build());
            Assert.Contains("shopping.example", loaded.AppConnectionString, StringComparison.Ordinal);
            Assert.False(loaded.ExposeFault);
            Assert.Equal(900, loaded.LockoutSeconds);
            Assert.Equal(1800, loaded.PasswordResetTokenSeconds);
            Assert.Equal("smtp.example", loaded.SmtpHost);
            Assert.Equal(587, loaded.SmtpPort);
            Assert.Equal("user", loaded.SmtpUser);
            Assert.Equal("secret", loaded.SmtpPassword);
            Assert.Equal("shop@example.com", loaded.SmtpFrom);
            Assert.Contains("blob.core.windows.net", loaded.BlobEndpoint, StringComparison.Ordinal);
            Assert.Equal("products", loaded.Container);
            Assert.Equal("https://images.example", loaded.ImageBaseUrl);
            Assert.Equal("client", loaded.AzureClientId);
            Assert.Contains("shopping_migrator", loaded.MigratorConnectionString, StringComparison.Ordinal);
            var testing = AppSettings.Load(new HostEnv("Testing"), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sql:Server"] = "127.0.0.1,14333",
                ["Sql:Database"] = "ShoppingTests",
                ["Sql:User"] = "shopping_app",
                ["Sql:Password"] = "secret",
                ["Sql:MigratorUser"] = "shopping_migrator",
                ["Sql:MigratorPassword"] = "secret",
                ["Sql:Encrypt"] = "true",
                ["Smtp:Port"] = "2525",
                ["LockoutSeconds"] = "2",
                ["Testing:ExposeFault"] = "True",
            }).Build());
            Assert.Contains("Encrypt=True", testing.AppConnectionString, StringComparison.OrdinalIgnoreCase);
            Assert.True(testing.ExposeFault);
            Assert.Equal(2, testing.LockoutSeconds);
            Environment.SetEnvironmentVariable("SMTP_PORT", null);
            var defaults = AppSettings.Load(new HostEnv("Development"), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sql:Server"] = "127.0.0.1",
                ["Sql:Database"] = "ShoppingTests",
                ["Sql:User"] = "shopping_app",
                ["Sql:MigratorUser"] = "shopping_migrator",
                ["LockoutSeconds"] = "nope",
            }).Build());
            Assert.Equal(900, defaults.LockoutSeconds);
            Assert.Equal(2525, defaults.SmtpPort);
            Assert.False(defaults.ExposeFault);
            Environment.SetEnvironmentVariable("SQL_ENCRYPT", "");
            var blankEncrypt = AppSettings.Load(new HostEnv("Development"), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sql:Server"] = "127.0.0.1",
                ["Sql:Database"] = "ShoppingTests",
                ["Sql:User"] = "shopping_app",
                ["Sql:MigratorUser"] = "shopping_migrator",
                ["Sql:Encrypt"] = "",
            }).Build());
            Assert.Contains("Encrypt=False", blankEncrypt.AppConnectionString, StringComparison.OrdinalIgnoreCase);
            var config = new ConfigurationBuilder().Build();
            Environment.SetEnvironmentVariable("SQL_PASSWORD", "");
            Assert.Equal("fallback", AppSettings.Pick(config, "Missing", "SQL_PASSWORD", "fallback"));
            Environment.SetEnvironmentVariable("SQL_PASSWORD", "from-env");
            Assert.Equal("from-env", AppSettings.Pick(config, "Missing", "SQL_PASSWORD"));
            Assert.Equal("configured", AppSettings.Pick(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Sql:Password"] = "configured" }).Build(), "Sql:Password", "SQL_PASSWORD"));
            Assert.False(AppSettings.BuildConnection("s", "d", "u", "p", "False").Contains("Encrypt=True", StringComparison.OrdinalIgnoreCase));
            Assert.Throws<InvalidOperationException>(() => AppSettings.Load(new HostEnv("Testing"), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sql:Server"] = "s",
                ["Sql:Database"] = "d",
                ["Sql:User"] = "u",
                ["Sql:MigratorUser"] = "m",
                ["Smtp:Port"] = "nope",
            }).Build()));
        }
        finally
        {
            Restore(snapshot);
        }
    }

    /// <summary>SMTP 成功、認證失敗與連線失敗。</summary>
    [Fact]
    public async Task SmtpSenderCoversConnectAuthAndSuccess()
    {
        var sender = new SmtpEmailSender();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var served = Task.Run(() => ServeSmtp(listener, true));
        await sender.SendAsync("127.0.0.1", port, "ok", "ok", "shop@example.com", "person@example.com", "subject", "body", MailKit.Security.SecureSocketOptions.None);
        await served;
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        served = Task.Run(() => ServeSmtp(listener, false));
        var auth = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync("127.0.0.1", port, "bad", "bad", "shop@example.com", "person@example.com", "subject", "body", MailKit.Security.SecureSocketOptions.None));
        Assert.Contains("認證失敗", auth.Message, StringComparison.Ordinal);
        await served;
        var connect = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync("127.0.0.1", 1, "ok", "ok", "shop@example.com", "person@example.com", "subject", "body", MailKit.Security.SecureSocketOptions.StartTls));
        Assert.Contains("連線失敗", connect.Message, StringComparison.Ordinal);
    }

    /// <summary>示範圖片網址在有無基底時不同。</summary>
    [Fact]
    public void DemoImagePaths()
    {
        Assert.Equal("/images/products/demo_seller/laptop.png", Shopping.Web.Services.DemoSeeder.ProductImage("", "laptop.png"));
        Assert.Equal("/images/products/demo_seller/laptop.png", Shopping.Web.Services.DemoSeeder.ProductImage(null, "laptop.png"));
        Assert.Equal("https://cdn.example/products/demo_seller/laptop.png", Shopping.Web.Services.DemoSeeder.ProductImage("https://cdn.example/", "laptop.png"));
    }

    private static string Report(params string[] packages)
    {
        return "<coverage>" + string.Join(string.Empty, packages) + "</coverage>";
    }

    private static string Package(string name, string? line, string? branch)
    {
        var element = new XElement("package", new XAttribute("name", name));
        if (line is not null)
        {
            element.Add(new XAttribute("line-rate", line));
        }

        if (branch is not null)
        {
            element.Add(new XAttribute("branch-rate", branch));
        }

        return element.ToString();
    }

    private static async Task ServeSmtp(TcpListener listener, bool accept)
    {
        using var tcp = await listener.AcceptTcpClientAsync();
        listener.Stop();
        await using var stream = tcp.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };
        await writer.WriteLineAsync("220 ready");
        while (await reader.ReadLineAsync() is string line)
        {
            var upper = line.ToUpperInvariant();
            if (upper.StartsWith("EHLO", StringComparison.Ordinal) || upper.StartsWith("HELO", StringComparison.Ordinal))
            {
                await writer.WriteLineAsync("250-test");
                await writer.WriteLineAsync("250 AUTH LOGIN PLAIN");
            }
            else if (upper.StartsWith("AUTH", StringComparison.Ordinal))
            {
                if (upper.Contains("PLAIN", StringComparison.Ordinal) && upper.Split(' ').Length == 2)
                {
                    await writer.WriteLineAsync("334 ");
                    await reader.ReadLineAsync();
                }

                await writer.WriteLineAsync("334 VXNlcm5hbWU6");
                await reader.ReadLineAsync();
                await writer.WriteLineAsync("334 UGFzc3dvcmQ6");
                await reader.ReadLineAsync();
                await writer.WriteLineAsync(accept ? "235 ok" : "535 no");
                if (!accept)
                {
                    break;
                }
            }
            else if (upper.StartsWith("MAIL", StringComparison.Ordinal) || upper.StartsWith("RCPT", StringComparison.Ordinal))
            {
                await writer.WriteLineAsync("250 ok");
            }
            else if (upper == "DATA")
            {
                await writer.WriteLineAsync("354 go");
                while (await reader.ReadLineAsync() is string body && body != ".")
                {
                }

                await writer.WriteLineAsync("250 sent");
            }
            else if (upper.StartsWith("QUIT", StringComparison.Ordinal))
            {
                await writer.WriteLineAsync("221 bye");
                break;
            }
            else
            {
                await writer.WriteLineAsync("250 ok");
            }
        }
    }

    private static Dictionary<string, string?> Snapshot()
    {
        return AppSettings.RequiredVariables.ToDictionary(name => name, Environment.GetEnvironmentVariable, StringComparer.Ordinal);
    }

    private static void ClearRequired()
    {
        foreach (var name in AppSettings.RequiredVariables)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    private static void SetRequired()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SQL_SERVER"] = "shopping.example",
            ["SQL_DATABASE"] = "shop",
            ["SQL_USER"] = "shopping_app",
            ["SQL_PASSWORD"] = "secret",
            ["SQL_ENCRYPT"] = "True",
            ["SQL_MIGRATOR_USER"] = "shopping_migrator",
            ["SQL_MIGRATOR_PASSWORD"] = "secret",
            ["SMTP_HOST"] = "smtp.example",
            ["SMTP_PORT"] = "587",
            ["SMTP_USER"] = "user",
            ["SMTP_PASSWORD"] = "secret",
            ["SMTP_FROM"] = "shop@example.com",
            ["STORAGE_BLOB_ENDPOINT"] = "https://example.blob.core.windows.net",
            ["STORAGE_CONTAINER"] = "products",
            ["IMAGE_BASE_URL"] = "https://images.example",
            ["AZURE_CLIENT_ID"] = "client",
        };
        foreach (var pair in values)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    private static void Restore(Dictionary<string, string?> snapshot)
    {
        foreach (var pair in snapshot)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    private sealed class HostEnv : IWebHostEnvironment
    {
        public HostEnv(string name)
        {
            EnvironmentName = name;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Shopping.Web";

        public string ContentRootPath { get; set; } = ".";

        public string WebRootPath { get; set; } = "wwwroot";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FixedTime : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTime(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }

    private sealed class StuckStream : MemoryStream
    {
        public StuckStream(byte[] data)
            : base(data)
        {
        }

        public override bool CanSeek => false;
    }
}
