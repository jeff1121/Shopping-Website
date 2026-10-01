using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;

namespace Website.Config
{
    /// <summary>
    /// 集中讀取網站的應用程式設定（寄信、Blob 儲存與圖片網址）。
    /// 值來自 Web.config 的 appSettings，並由 Configuration Builders 以同名環境變數代入 ${名稱} 權杖。
    /// </summary>
    public static class AppSettings
    {
        /// <summary>未被環境變數替換時殘留的權杖開頭。</summary>
        private const string UnresolvedTokenPrefix = "${";

        /// <summary>App Service 無法解析 Key Vault 參考時，設定值會保留的原始字串開頭。</summary>
        private const string UnresolvedKeyVaultPrefix = "@Microsoft.KeyVault(";

        /// <summary>啟動時必須具備的 appSettings 名稱。SMTP_USER、SMTP_PASSWORD 可留空（例如本機不需驗證的 SMTP 測試伺服器）。</summary>
        private static readonly string[] RequiredSettings =
        {
            "SMTP_HOST", "SMTP_PORT", "SMTP_FROM",
            "STORAGE_BLOB_ENDPOINT", "STORAGE_CONTAINER", "IMAGE_BASE_URL"
        };

        /// <summary>啟動時必須能完整代入的連線字串名稱。</summary>
        private static readonly string[] RequiredConnectionStrings =
        {
            "cmpConnectionString", "migratorConnectionString"
        };

        /// <summary>SMTP 伺服器主機名稱（ACS 為 smtp.azurecomm.net）。</summary>
        public static string SmtpHost { get { return Get("SMTP_HOST"); } }

        /// <summary>SMTP 連接埠（ACS 為 587，STARTTLS）。</summary>
        public static int SmtpPort
        {
            get { return int.Parse(Get("SMTP_PORT"), NumberStyles.Integer, CultureInfo.InvariantCulture); }
        }

        /// <summary>SMTP 驗證帳號；空白代表不需驗證。</summary>
        public static string SmtpUser { get { return GetOptional("SMTP_USER"); } }

        /// <summary>SMTP 驗證密碼（Azure 上為 Key Vault 參考 smtp-password）。</summary>
        public static string SmtpPassword { get { return GetOptional("SMTP_PASSWORD"); } }

        /// <summary>寄件者地址；ACS 要求必須屬於已連結的寄件網域，例如 DoNotReply@xxxx.azurecomm.net。</summary>
        public static string SmtpFrom { get { return Get("SMTP_FROM"); } }

        /// <summary>Blob 服務端點，例如 https://stshoppingxxxx.blob.core.windows.net/；本機 Azurite 為 http://127.0.0.1:10000/devstoreaccount1。</summary>
        public static string StorageBlobEndpoint { get { return Get("STORAGE_BLOB_ENDPOINT"); } }

        /// <summary>商品圖片的 Blob 容器名稱（products）。</summary>
        public static string StorageContainer { get { return Get("STORAGE_CONTAINER"); } }

        /// <summary>圖片對外網址的根（Front Door 端點，不含結尾斜線）；商品圖片網址為「此值/容器/賣家/檔名」。</summary>
        public static string ImageBaseUrl { get { return Get("IMAGE_BASE_URL").TrimEnd('/'); } }

        /// <summary>
        /// 檢查必要設定是否都已由環境變數代入。只回報設定名稱，不輸出任何設定值。
        /// </summary>
        /// <returns>缺漏或未解析的設定名稱；全部正常時為空集合。</returns>
        public static IList<string> FindMissing()
        {
            List<string> missing = new List<string>();
            missing.AddRange(RequiredSettings
                .Where(name => IsUnresolved(ConfigurationManager.AppSettings[name]))
                .Select(name => "appSettings/" + name));

            // 選填設定可留空，但若是無法解析的 Key Vault 參考仍視為缺漏
            missing.AddRange(new[] { "SMTP_USER", "SMTP_PASSWORD" }
                .Where(name => IsUnresolvedKeyVaultReference(ConfigurationManager.AppSettings[name]))
                .Select(name => "appSettings/" + name));

            missing.AddRange(RequiredConnectionStrings
                .Where(name => ConfigurationManager.ConnectionStrings[name] == null
                    || IsUnresolved(ConfigurationManager.ConnectionStrings[name].ConnectionString))
                .Select(name => "connectionStrings/" + name));

            return missing;
        }

        /// <summary>
        /// 啟動檢查：若有設定未代入，拋出列出設定名稱的 <see cref="ConfigurationErrorsException"/>。
        /// 由 Global.asax 的 Application_Start 呼叫。
        /// </summary>
        /// <exception cref="ConfigurationErrorsException">有必要設定缺漏或未解析。</exception>
        public static void Validate()
        {
            IList<string> missing = FindMissing();
            if (missing.Count > 0)
            {
                throw new ConfigurationErrorsException(
                    "下列設定未提供或無法解析，請設定同名環境變數或 App Service 應用程式設定："
                    + string.Join(", ", missing));
            }
        }

        /// <summary>
        /// 判斷設定值是否為 App Service 無法解析而原樣保留的 Key Vault 參考（用於選填設定）。
        /// </summary>
        /// <param name="value">設定值。</param>
        /// <returns>是未解析的 Key Vault 參考時為 true；空白或一般值為 false。</returns>
        private static bool IsUnresolvedKeyVaultReference(string value)
        {
            return value != null && value.StartsWith(UnresolvedKeyVaultPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判斷設定值是否仍為空白、未替換的 ${名稱} 權杖，或未解析的 Key Vault 參考。
        /// </summary>
        /// <param name="value">設定值。</param>
        /// <returns>未完成代入時為 true。</returns>
        private static bool IsUnresolved(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                || value.IndexOf(UnresolvedTokenPrefix, StringComparison.Ordinal) >= 0
                || value.IndexOf(UnresolvedKeyVaultPrefix, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 讀取必要設定；缺漏時拋出只含設定名稱的錯誤。
        /// </summary>
        /// <param name="name">appSettings 名稱。</param>
        /// <returns>設定值。</returns>
        /// <exception cref="ConfigurationErrorsException">設定缺漏或未解析。</exception>
        private static string Get(string name)
        {
            string value = ConfigurationManager.AppSettings[name];
            if (IsUnresolved(value))
            {
                throw new ConfigurationErrorsException("設定 appSettings/" + name + " 未提供或無法解析。");
            }

            return value.Trim();
        }

        /// <summary>
        /// 讀取可留空的設定；未替換的權杖視為空白。
        /// </summary>
        /// <param name="name">appSettings 名稱。</param>
        /// <returns>設定值，未設定時為空字串。</returns>
        private static string GetOptional(string name)
        {
            string value = ConfigurationManager.AppSettings[name];
            return IsUnresolved(value) ? string.Empty : value.Trim();
        }
    }
}
