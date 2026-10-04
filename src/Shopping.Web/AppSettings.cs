using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Shopping.Web;

/// <summary>網站啟動設定。正式環境只讀環境變數。</summary>
public sealed class AppSettings
{
    /// <summary>應用程式連線字串。</summary>
    public required string AppConnectionString { get; init; }

    /// <summary>Migration 連線字串。</summary>
    public required string MigratorConnectionString { get; init; }

    /// <summary>SMTP 主機。</summary>
    public required string SmtpHost { get; init; }

    /// <summary>SMTP 埠。</summary>
    public int SmtpPort { get; init; }

    /// <summary>SMTP 帳號。</summary>
    public required string SmtpUser { get; init; }

    /// <summary>SMTP 密碼。</summary>
    public required string SmtpPassword { get; init; }

    /// <summary>寄件者。</summary>
    public required string SmtpFrom { get; init; }

    /// <summary>Blob 端點。</summary>
    public required string BlobEndpoint { get; init; }

    /// <summary>容器名稱。</summary>
    public required string Container { get; init; }

    /// <summary>圖片網址根。</summary>
    public required string ImageBaseUrl { get; init; }

    /// <summary>受控識別用戶端識別碼。</summary>
    public required string AzureClientId { get; init; }

    /// <summary>鎖定秒數。</summary>
    public int LockoutSeconds { get; init; }

    /// <summary>重設權杖秒數。</summary>
    public int PasswordResetTokenSeconds { get; init; }

    /// <summary>是否註冊測試用拋出點。</summary>
    public bool ExposeFault { get; init; }

    /// <summary>正式環境必填的環境變數名稱。</summary>
    public static readonly string[] RequiredVariables =
    [
        "SQL_SERVER",
        "SQL_DATABASE",
        "SQL_USER",
        "SQL_PASSWORD",
        "SQL_ENCRYPT",
        "SQL_MIGRATOR_USER",
        "SQL_MIGRATOR_PASSWORD",
        "SMTP_HOST",
        "SMTP_PORT",
        "SMTP_USER",
        "SMTP_PASSWORD",
        "SMTP_FROM",
        "STORAGE_BLOB_ENDPOINT",
        "STORAGE_CONTAINER",
        "IMAGE_BASE_URL",
        "AZURE_CLIENT_ID",
    ];

    /// <summary>依環境載入設定。正式環境缺變數時拋出。</summary>
    public static AppSettings Load(IWebHostEnvironment environment, IConfiguration configuration)
    {
        if (environment.IsProduction())
        {
            var missing = MissingVariables();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException("缺少環境變數 " + string.Join(", ", missing));
            }

            return FromEnvironment();
        }

        return FromConfiguration(configuration);
    }

    /// <summary>列出尚未設定的正式環境變數。</summary>
    public static IReadOnlyList<string> MissingVariables()
    {
        var missing = new List<string>();
        foreach (var name in RequiredVariables)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
            {
                missing.Add(name);
            }
        }

        return missing;
    }

    /// <summary>組 SQL 連線字串。</summary>
    public static string BuildConnection(string server, string database, string user, string password, string encrypt)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            UserID = user,
            Password = password,
            Encrypt = encrypt.Equals("True", StringComparison.OrdinalIgnoreCase),
            TrustServerCertificate = true,
        };
        return builder.ConnectionString;
    }

    private static AppSettings FromEnvironment()
    {
        return new AppSettings
        {
            AppConnectionString = BuildConnection(Read("SQL_SERVER"), Read("SQL_DATABASE"), Read("SQL_USER"), Read("SQL_PASSWORD"), Read("SQL_ENCRYPT")),
            MigratorConnectionString = BuildConnection(Read("SQL_SERVER"), Read("SQL_DATABASE"), Read("SQL_MIGRATOR_USER"), Read("SQL_MIGRATOR_PASSWORD"), Read("SQL_ENCRYPT")),
            SmtpHost = Read("SMTP_HOST"),
            SmtpPort = ParsePort(Read("SMTP_PORT")),
            SmtpUser = Read("SMTP_USER"),
            SmtpPassword = Read("SMTP_PASSWORD"),
            SmtpFrom = Read("SMTP_FROM"),
            BlobEndpoint = Read("STORAGE_BLOB_ENDPOINT"),
            Container = Read("STORAGE_CONTAINER"),
            ImageBaseUrl = Read("IMAGE_BASE_URL"),
            AzureClientId = Read("AZURE_CLIENT_ID"),
            LockoutSeconds = 900,
            PasswordResetTokenSeconds = 1800,
            ExposeFault = false,
        };
    }

    private static AppSettings FromConfiguration(IConfiguration configuration)
    {
        var server = Pick(configuration, "Sql:Server", "SQL_SERVER");
        var database = Pick(configuration, "Sql:Database", "SQL_DATABASE");
        var encrypt = Pick(configuration, "Sql:Encrypt", "SQL_ENCRYPT");
        if (string.IsNullOrEmpty(encrypt))
        {
            encrypt = "False";
        }

        var lockout = ParseOr(configuration["LockoutSeconds"], 900);
        var tokenSeconds = ParseOr(configuration["PasswordResetTokenSeconds"], 1800);
        return new AppSettings
        {
            AppConnectionString = BuildConnection(server, database, Pick(configuration, "Sql:User", "SQL_USER"), Pick(configuration, "Sql:Password", "SQL_PASSWORD"), encrypt),
            MigratorConnectionString = BuildConnection(server, database, Pick(configuration, "Sql:MigratorUser", "SQL_MIGRATOR_USER"), Pick(configuration, "Sql:MigratorPassword", "SQL_MIGRATOR_PASSWORD"), encrypt),
            SmtpHost = Pick(configuration, "Smtp:Host", "SMTP_HOST"),
            SmtpPort = ParsePort(Pick(configuration, "Smtp:Port", "SMTP_PORT", "2525")),
            SmtpUser = Pick(configuration, "Smtp:User", "SMTP_USER"),
            SmtpPassword = Pick(configuration, "Smtp:Password", "SMTP_PASSWORD"),
            SmtpFrom = Pick(configuration, "Smtp:From", "SMTP_FROM", "shop@example.com"),
            BlobEndpoint = Pick(configuration, "Storage:BlobEndpoint", "STORAGE_BLOB_ENDPOINT"),
            Container = Pick(configuration, "Storage:Container", "STORAGE_CONTAINER", "products"),
            ImageBaseUrl = Pick(configuration, "Image:BaseUrl", "IMAGE_BASE_URL"),
            AzureClientId = Pick(configuration, "Azure:ClientId", "AZURE_CLIENT_ID"),
            LockoutSeconds = lockout,
            PasswordResetTokenSeconds = tokenSeconds,
            ExposeFault = string.Equals(configuration["Testing:ExposeFault"], "true", StringComparison.OrdinalIgnoreCase),
        };
    }

    private static string Read(string name)
    {
        return Environment.GetEnvironmentVariable(name)!;
    }

    /// <summary>組態優先，其次環境變數，最後預設值。</summary>
    public static string Pick(IConfiguration configuration, string key, string envName, string fallback = "")
    {
        var configured = configuration[key];
        if (!string.IsNullOrEmpty(configured))
        {
            return configured;
        }

        var fromEnv = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrEmpty(fromEnv))
        {
            return fromEnv;
        }

        return fallback;
    }

    private static int ParsePort(string value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
        {
            throw new InvalidOperationException("缺少環境變數 SMTP_PORT");
        }

        return port;
    }

    private static int ParseOr(string? value, int fallback)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }
}
