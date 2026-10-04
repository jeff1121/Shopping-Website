using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Shopping.Web.Tests;

/// <summary>準備測試 SQL，並以 Testing 環境啟動網站。</summary>
public class ShopApp : WebApplicationFactory<Program>
{
    /// <summary>應用程式連線字串。</summary>
    public const string AppConnectionString = "Server=127.0.0.1,14333;Database=ShoppingTests;User ID=shopping_app;Password=Test_only_9xQ;Encrypt=False;TrustServerCertificate=True";

    /// <summary>Migration 連線字串。</summary>
    public const string MigratorConnectionString = "Server=127.0.0.1,14333;Database=ShoppingTests;User ID=shopping_migrator;Password=Test_only_9xQ;Encrypt=False;TrustServerCertificate=True";

    /// <summary>建立主機前先確認 SQL。</summary>
    public ShopApp()
    {
        EnsureDatabase();
        Environment.SetEnvironmentVariable("SQL_PASSWORD", "Test_only_9xQ");
        Environment.SetEnvironmentVariable("SQL_MIGRATOR_PASSWORD", "Test_only_9xQ");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sql:Password"] = "Test_only_9xQ",
                ["Sql:MigratorPassword"] = "Test_only_9xQ",
                ["Testing:ExposeFault"] = "true",
                ["LockoutSeconds"] = "2",
            });
        });
    }

    /// <summary>不跟隨重新導向的用戶端。</summary>
    public HttpClient CreateNoRedirectClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });
    }

    /// <summary>連接埠沒開就失敗，不略過測試。</summary>
    public static void EnsureDatabase()
    {
        using var probe = new TcpClient();
        try
        {
            var connected = probe.ConnectAsync("127.0.0.1", 14333).Wait(TimeSpan.FromSeconds(2));
            if (!connected || !probe.Connected)
            {
                throw new InvalidOperationException("測試 SQL 未啟動。請先執行 docker compose -f docker-compose.tests.yml up -d");
            }
        }
        catch (Exception exception) when (exception is SocketException or InvalidOperationException)
        {
            throw new InvalidOperationException("測試 SQL 未啟動。請先執行 docker compose -f docker-compose.tests.yml up -d", exception);
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

    /// <summary>從測試組件往上找到含 docker-compose 的根目錄。</summary>
    public static string FindRepoRoot()
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
