using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using DbUp;
using DbUp.Engine;
using Website.Config;

namespace Website.Data
{
    /// <summary>
    /// 網站啟動時套用資料庫 migration（Plan.md §10、ADR-0002）。
    /// 使用 DbUp 依名稱順序執行內嵌於組件的 <c>Website.Migrations.*.sql</c>，
    /// 已執行的腳本記錄在 <c>dbo.SchemaVersions</c>，不會重複執行。
    /// </summary>
    public static class DatabaseMigrator
    {
        /// <summary>內嵌 migration 腳本的資源名稱前綴（RootNamespace + 資料夾）。</summary>
        private const string ScriptPrefix = "Website.Migrations.";

        /// <summary>sp_getapplock 的鎖定名稱；多個執行個體同時啟動時只讓一個執行 migration。</summary>
        private const string LockResource = "Website.DatabaseMigration";

        /// <summary>等待其他執行個體完成 migration 的最長時間（毫秒）。</summary>
        private const int LockTimeoutMilliseconds = 120000;

        /// <summary>
        /// 以 migrator 帳號連線，取得應用程式鎖後執行尚未套用的 migration。
        /// 每支腳本各自一個交易；任一支失敗時該腳本回復，並拋出例外讓網站進入啟動失敗狀態。
        /// </summary>
        /// <exception cref="InvalidOperationException">取得鎖逾時、連線失敗或腳本執行失敗。</exception>
        public static void Run()
        {
            try
            {
                using (SqlConnection lockConnection = Db.CreateMigratorConnection())
                {
                    lockConnection.Open();
                    AcquireLock(lockConnection);
                    try
                    {
                        Upgrade(lockConnection.ConnectionString);
                    }
                    finally
                    {
                        ReleaseLock(lockConnection);
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("資料庫 migration 失敗：無法連線或取得鎖定。", ex);
            }
        }

        /// <summary>
        /// 設定並執行 DbUp：內嵌腳本、每支腳本一個交易、替換 <c>$IMAGE_BASE_URL$</c>，記錄輸出到 Trace。
        /// </summary>
        /// <param name="connectionString">migrator 連線字串。</param>
        /// <exception cref="InvalidOperationException">任一支腳本執行失敗。</exception>
        private static void Upgrade(string connectionString)
        {
            UpgradeEngine engine = DeployChanges.To
                .SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly(), IsMigrationScript)
                .WithTransactionPerScript()
                .WithVariable("IMAGE_BASE_URL", AppSettings.ImageBaseUrl)
                .JournalToSqlTable("dbo", "SchemaVersions")
                .LogToTrace()
                .Build();

            DatabaseUpgradeResult result = engine.PerformUpgrade();
            if (!result.Successful)
            {
                string script = result.ErrorScript == null ? "（未知）" : result.ErrorScript.Name;
                throw new InvalidOperationException("資料庫 migration 失敗，腳本：" + script, result.Error);
            }

            Trace.TraceInformation("資料庫 migration 完成，本次執行 {0} 支腳本。", result.Scripts.Count());
        }

        /// <summary>
        /// 判斷內嵌資源是否為 migration 腳本。
        /// </summary>
        /// <param name="resourceName">內嵌資源名稱。</param>
        /// <returns>位於 Migrations 資料夾的 .sql 檔時為 true。</returns>
        private static bool IsMigrationScript(string resourceName)
        {
            return resourceName.StartsWith(ScriptPrefix, StringComparison.Ordinal)
                && resourceName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 以 Session 擁有者取得排他應用程式鎖；鎖會在釋放或連線關閉時解除。
        /// </summary>
        /// <param name="connection">已開啟、專供鎖定使用的連線。</param>
        /// <exception cref="InvalidOperationException">等待逾時或遭死結犧牲。</exception>
        private static void AcquireLock(SqlConnection connection)
        {
            using (SqlCommand cmd = new SqlCommand("sys.sp_getapplock", connection))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = (LockTimeoutMilliseconds / 1000) + 30;
                cmd.Parameters.AddWithValue("@Resource", LockResource);
                cmd.Parameters.AddWithValue("@LockMode", "Exclusive");
                cmd.Parameters.AddWithValue("@LockOwner", "Session");
                cmd.Parameters.AddWithValue("@LockTimeout", LockTimeoutMilliseconds);
                SqlParameter returnValue = cmd.Parameters.Add("@ReturnValue", SqlDbType.Int);
                returnValue.Direction = ParameterDirection.ReturnValue;
                cmd.ExecuteNonQuery();

                int status = (int)returnValue.Value;
                if (status < 0)
                {
                    throw new InvalidOperationException("資料庫 migration 無法取得鎖定，sp_getapplock 回傳 " + status + "。");
                }
            }
        }

        /// <summary>
        /// 釋放 <see cref="AcquireLock"/> 取得的應用程式鎖。
        /// </summary>
        /// <param name="connection">取得鎖的同一條連線。</param>
        private static void ReleaseLock(SqlConnection connection)
        {
            using (SqlCommand cmd = new SqlCommand("sys.sp_releaseapplock", connection))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Resource", LockResource);
                cmd.Parameters.AddWithValue("@LockOwner", "Session");
                cmd.ExecuteNonQuery();
            }
        }
    }
}
