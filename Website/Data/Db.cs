using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace Website.Data
{
    /// <summary>
    /// 提供集中建立資料庫連線與執行參數化查詢的輔助方法。
    /// 每次呼叫都以 using 建立並釋放連線，SQL 一律為常數字串，使用者輸入只能經 <see cref="Param"/> 傳入。
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
        /// 建立查詢參數；值為 null 時改用 <see cref="DBNull.Value"/>。
        /// </summary>
        /// <param name="name">參數名稱（含 @）。</param>
        /// <param name="value">參數值。</param>
        /// <returns>可傳給 <see cref="Query"/>、<see cref="Execute"/>、<see cref="Scalar"/> 的參數。</returns>
        public static SqlParameter Param(string name, object value)
        {
            return new SqlParameter(name, value ?? DBNull.Value);
        }

        /// <summary>
        /// 執行查詢並以 <see cref="DataTable"/> 傳回全部結果列。
        /// </summary>
        /// <param name="sql">SQL 語句（常數字串，變數以參數表示）。</param>
        /// <param name="parameters">查詢參數。</param>
        /// <returns>查詢結果；沒有資料時為空表。</returns>
        public static DataTable Query(string sql, params SqlParameter[] parameters)
        {
            using (SqlConnection con = CreateConnection())
            using (SqlCommand cmd = new SqlCommand(sql, con))
            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
            {
                cmd.Parameters.AddRange(parameters);
                DataTable table = new DataTable();
                adapter.Fill(table);
                return table;
            }
        }

        /// <summary>
        /// 執行 INSERT、UPDATE、DELETE 等不傳回結果列的語句。
        /// </summary>
        /// <param name="sql">SQL 語句（常數字串，變數以參數表示）。</param>
        /// <param name="parameters">查詢參數。</param>
        /// <returns>受影響的列數。</returns>
        public static int Execute(string sql, params SqlParameter[] parameters)
        {
            using (SqlConnection con = CreateConnection())
            using (SqlCommand cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddRange(parameters);
                con.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 執行查詢並傳回第一列第一欄的值。
        /// </summary>
        /// <param name="sql">SQL 語句（常數字串，變數以參數表示）。</param>
        /// <param name="parameters">查詢參數。</param>
        /// <returns>第一列第一欄的值；沒有資料時為 null。</returns>
        public static object Scalar(string sql, params SqlParameter[] parameters)
        {
            using (SqlConnection con = CreateConnection())
            using (SqlCommand cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddRange(parameters);
                con.Open();
                object value = cmd.ExecuteScalar();
                return value == DBNull.Value ? null : value;
            }
        }

        /// <summary>
        /// 以登入時輸入的帳號（使用者名稱或 Email）查出會員姓名 uname（各資料表的關聯鍵）。
        /// </summary>
        /// <param name="login">Session["user"] 的值。</param>
        /// <returns>會員姓名；查無帳號或未登入時為空字串。</returns>
        public static string GetUname(object login)
        {
            if (login == null)
            {
                return "";
            }

            object uname = Scalar("SELECT uname FROM violet_user_login WHERE username=@login OR email=@login", Param("@login", login.ToString()));
            return uname == null ? "" : uname.ToString();
        }

        /// <summary>
        /// 判斷例外是否為違反主索引鍵或唯一條件約束（SQL Server 錯誤 2627、2601），例如名稱或 Email 重複。
        /// </summary>
        /// <param name="ex">執行 SQL 時拋出的例外。</param>
        /// <returns>任一錯誤為重複鍵值時為 true。</returns>
        public static bool IsDuplicateKey(SqlException ex)
        {
            return HasErrorNumber(ex, 2627, 2601);
        }

        /// <summary>
        /// 判斷例外是否為字串超過欄位長度而無法寫入（SQL Server 錯誤 8152、2628）。
        /// </summary>
        /// <param name="ex">執行 SQL 時拋出的例外。</param>
        /// <returns>任一錯誤為字串截斷時為 true。</returns>
        public static bool IsTruncation(SqlException ex)
        {
            return HasErrorNumber(ex, 8152, 2628);
        }

        /// <summary>
        /// 檢查例外中的任一 SQL 錯誤是否為指定的錯誤編號。
        /// </summary>
        /// <param name="ex">執行 SQL 時拋出的例外。</param>
        /// <param name="numbers">要比對的 SQL Server 錯誤編號。</param>
        /// <returns>找到任一相符編號時為 true。</returns>
        private static bool HasErrorNumber(SqlException ex, params int[] numbers)
        {
            foreach (SqlError error in ex.Errors)
            {
                if (Array.IndexOf(numbers, error.Number) >= 0)
                {
                    return true;
                }
            }

            return false;
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
