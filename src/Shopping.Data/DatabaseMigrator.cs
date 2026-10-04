using System.Globalization;
using System.Reflection;
using Microsoft.Data.SqlClient;

namespace Shopping.Data;

/// <summary>一個內嵌 SQL 腳本。</summary>
public sealed record SqlScript(string Name, string Sql);

/// <summary>在啟動時依序套用內嵌 SQL，並用應用程式鎖避免同時改結構。</summary>
public sealed class DatabaseMigrator
{
    private readonly string _connectionString;
    private readonly IReadOnlyList<SqlScript> _scripts;
    private readonly int _lockTimeoutMs;

    /// <summary>建立 migration 執行器。</summary>
    public DatabaseMigrator(string connectionString, IReadOnlyList<SqlScript> scripts, int lockTimeoutMs = 60000)
    {
        _connectionString = connectionString;
        _scripts = scripts;
        _lockTimeoutMs = lockTimeoutMs;
    }

    /// <summary>從組件載入內嵌 SQL。沒有腳本時丟出例外。</summary>
    public static IReadOnlyList<SqlScript> LoadFromAssembly(Assembly assembly)
    {
        const string marker = ".Migrations.";
        var names = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        if (names.Count == 0)
        {
            throw new InvalidOperationException("沒有內嵌 SQL 腳本。");
        }

        var scripts = new List<SqlScript>();
        foreach (var name in names)
        {
            var index = name.LastIndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
            {
                throw new InvalidOperationException("腳本資源名稱不符。");
            }

            var scriptName = name[(index + marker.Length)..];
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            scripts.Add(new SqlScript(scriptName, reader.ReadToEnd()));
        }

        return scripts;
    }

    /// <summary>套用尚未執行的腳本。</summary>
    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var lockCommand = new SqlCommand(
            "DECLARE @result int; EXEC @result = sp_getapplock @Resource = N'shop_migrations', @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = @timeout; SELECT @result;",
            connection);
        lockCommand.CommandTimeout = 120;
        lockCommand.Parameters.AddWithValue("@timeout", _lockTimeoutMs);
        var locked = Convert.ToInt32(await lockCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        if (locked < 0)
        {
            throw new InvalidOperationException("無法取得 migration 鎖。");
        }

        try
        {
            await ExecuteAsync(connection, "IF OBJECT_ID(N'dbo.shop_schema_version', N'U') IS NULL CREATE TABLE dbo.shop_schema_version (ScriptName nvarchar(260) NOT NULL CONSTRAINT PK_shop_schema_version PRIMARY KEY, AppliedAt datetimeoffset NOT NULL);", cancellationToken);
            foreach (var script in _scripts)
            {
                var applied = await ExecuteScalarAsync(connection, "SELECT COUNT(*) FROM dbo.shop_schema_version WHERE ScriptName = @name", cancellationToken, new SqlParameter("@name", script.Name));
                if (Convert.ToInt32(applied) > 0)
                {
                    continue;
                }

                await ExecuteAsync(connection, script.Sql, cancellationToken);
                await ExecuteAsync(connection, "INSERT INTO dbo.shop_schema_version (ScriptName, AppliedAt) VALUES (@name, SYSUTCDATETIME())", cancellationToken, new SqlParameter("@name", script.Name));
            }
        }
        finally
        {
            await ExecuteAsync(connection, "EXEC sp_releaseapplock @Resource = N'shop_migrations', @LockOwner = 'Session';", cancellationToken);
        }
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken, params SqlParameter[] parameters)
    {
        await using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = 120;
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<object?> ExecuteScalarAsync(SqlConnection connection, string sql, CancellationToken cancellationToken, params SqlParameter[] parameters)
    {
        await using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = 120;
        command.Parameters.AddRange(parameters);
        return await command.ExecuteScalarAsync(cancellationToken);
    }
}
