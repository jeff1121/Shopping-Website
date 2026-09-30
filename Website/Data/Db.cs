using System.Configuration;
using System.Data.SqlClient;

namespace Website.Data
{
    /// <summary>
    /// 提供集中建立資料庫連線的輔助方法。
    /// </summary>
    public static class Db
    {
        /// <summary>
        /// 建立供網站執行期使用的資料庫連線。
        /// </summary>
        /// <returns>以 Web.config 的 cmpConnectionString 建立的 SQL Server 連線。</returns>
        public static SqlConnection CreateConnection()
        {
            return new SqlConnection(GetConnectionString("cmpConnectionString"));
        }

        /// <summary>
        /// 建立供資料庫遷移工具使用的資料庫連線。
        /// </summary>
        /// <returns>以 Web.config 的 migratorConnectionString 建立的 SQL Server 連線。</returns>
        public static SqlConnection CreateMigratorConnection()
        {
            return new SqlConnection(GetConnectionString("migratorConnectionString"));
        }

        /// <summary>
        /// 讀取指定名稱的連線字串，缺少設定時拋出明確錯誤。
        /// </summary>
        /// <param name="name">Web.config connectionStrings 中的設定名稱。</param>
        /// <returns>連線字串內容。</returns>
        private static string GetConnectionString(string name)
        {
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings[name];
            if (settings == null)
            {
                throw new ConfigurationErrorsException("找不到 Web.config 的 connectionStrings/" + name + " 設定。");
            }

            return settings.ConnectionString;
        }
    }
}
