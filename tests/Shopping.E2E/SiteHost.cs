using System.Diagnostics;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using NUnit.Framework;
using Shopping.Web;
using Shopping.Web.Services;

namespace Shopping.E2E;

/// <summary>以測試組態啟動網站與 Chromium。</summary>
[SetUpFixture]
public sealed class SiteHost
{
    /// <summary>應用程式連線字串。</summary>
    public const string AppConnectionString = "Server=127.0.0.1,14333;Database=ShoppingTests;User ID=shopping_app;Password=Test_only_9xQ;Encrypt=False;TrustServerCertificate=True";

    private static WebApplication? _app;

    /// <summary>網站根網址。</summary>
    public static string BaseUrl { get; private set; } = "";

    /// <summary>記憶體信箱。</summary>
    public static MemoryMailbox Mail { get; private set; } = null!;

    /// <summary>瀏覽器。</summary>
    public static IBrowser Browser { get; private set; } = null!;

    private static IPlaywright? _playwright;

    /// <summary>準備 SQL、憑證與網站。</summary>
    [OneTimeSetUp]
    public async Task Start()
    {
        EnsureDatabase();
        var root = FindRepoRoot();
        var web = Path.Combine(root, "src", "Shopping.Web");
        var pfx = Path.Combine(AppContext.BaseDirectory, "e2e-dev.pfx");
        var key = Path.Combine(AppContext.BaseDirectory, "e2e-key.pem");
        var cert = Path.Combine(AppContext.BaseDirectory, "e2e-cert.pem");
        await RunAsync("openssl", "req", "-x509", "-newkey", "rsa:2048", "-keyout", key, "-out", cert, "-days", "2", "-nodes", "-subj", "/CN=localhost");
        await RunAsync("openssl", "pkcs12", "-export", "-out", pfx, "-inkey", key, "-in", cert, "-passout", "pass:Test_only_9xQ");

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("SQL_PASSWORD", "Test_only_9xQ");
        Environment.SetEnvironmentVariable("SQL_MIGRATOR_PASSWORD", "Test_only_9xQ");
        Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Path", pfx);
        Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Password", "Test_only_9xQ");
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "https://127.0.0.1:0");
        Directory.SetCurrentDirectory(web);
        _app = AppBootstrap.Build(
        [
            "--environment", "Testing",
            "--contentRoot", web,
            "--applicationName", "Shopping.Web",
        ]);
        await _app.StartAsync();
        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        BaseUrl = addresses!.Addresses.First();
        Mail = _app.Services.GetRequiredService<MemoryMailbox>();
        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    /// <summary>關閉瀏覽器與網站。</summary>
    [OneTimeTearDown]
    public async Task Stop()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        _playwright?.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    /// <summary>開一個忽略憑證錯誤的瀏覽內容。</summary>
    public static async Task<IBrowserContext> OpenAsync(int width)
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            BaseURL = BaseUrl,
            ViewportSize = new ViewportSize { Width = width, Height = 900 },
        });
        return context;
    }

    /// <summary>計算某個 Email 的聯絡訊息。</summary>
    public static int CountContact(string email)
    {
        using var connection = new SqlConnection(AppConnectionString);
        connection.Open();
        using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.shop_contact_message WHERE Email = @email", connection);
        command.Parameters.AddWithValue("@email", email);
        return (int)command.ExecuteScalar()!;
    }

    private static async Task RunAsync(string file, params string[] args)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = file,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(file + " 結束碼 " + process.ExitCode + " " + await output + await error);
        }
    }

    private static void EnsureDatabase()
    {
        using var probe = new TcpClient();
        var connected = probe.ConnectAsync("127.0.0.1", 14333).Wait(TimeSpan.FromSeconds(2));
        if (!connected || !probe.Connected)
        {
            throw new InvalidOperationException("測試 SQL 未啟動。請先執行 docker compose -f docker-compose.tests.yml up -d");
        }

        var master = "Server=127.0.0.1,14333;Database=master;User ID=sa;Password=Test_only_9xQ;Encrypt=False;TrustServerCertificate=True";
        var sql = File.ReadAllText(Path.Combine(FindRepoRoot(), "docker", "init-test-users.sql"));
        using var connection = new SqlConnection(master);
        connection.Open();
        foreach (var batch in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(batch))
            {
                continue;
            }

            using var command = new SqlCommand(batch, connection);
            command.ExecuteNonQuery();
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docker-compose.tests.yml")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("找不到 docker-compose.tests.yml");
    }
}
