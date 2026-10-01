using System;
using System.Security.Cryptography;
using System.Text;

namespace Website.Data
{
    /// <summary>
    /// 一次性重設密碼權杖（violet_password_reset）。
    /// 權杖為 32 位元組隨機值（Base64URL），資料庫只存其 SHA-256 雜湊；有效期限 <see cref="LifetimeMinutes"/> 分鐘，使用一次即失效。
    /// </summary>
    public static class PasswordResets
    {
        /// <summary>權杖有效分鐘數。</summary>
        public const int LifetimeMinutes = 30;

        /// <summary>
        /// 為帳號建立新權杖；同一帳號先前未使用的權杖會一併作廢。
        /// </summary>
        /// <param name="uname">帳號姓名（violet_user_login.uname）。</param>
        /// <returns>要放進重設連結的原始權杖。</returns>
        public static string Issue(string uname)
        {
            byte[] bytes = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            string token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            Db.Execute("DELETE FROM violet_password_reset WHERE uname=@uname AND used_at IS NULL; "
                + "INSERT INTO violet_password_reset (token_hash, uname, expires_at) VALUES (@hash, @uname, DATEADD(MINUTE, @minutes, SYSUTCDATETIME()))",
                Db.Param("@uname", uname), Db.Param("@hash", HashToken(token)), Db.Param("@minutes", LifetimeMinutes));
            return token;
        }

        /// <summary>
        /// 判斷權杖是否存在、未使用且未過期。
        /// </summary>
        /// <param name="token">重設連結中的權杖。</param>
        /// <returns>可使用時為 true。</returns>
        public static bool IsValid(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            object count = Db.Scalar("SELECT COUNT(*) FROM violet_password_reset WHERE token_hash=@hash AND used_at IS NULL AND expires_at > SYSUTCDATETIME()",
                Db.Param("@hash", HashToken(token)));
            return Convert.ToInt32(count) > 0;
        }

        /// <summary>
        /// 在同一個交易中將權杖標記為已使用，並把該帳號的密碼改為新密碼的雜湊。
        /// </summary>
        /// <param name="token">重設連結中的權杖。</param>
        /// <param name="newPassword">新密碼（明碼，於此雜湊）。</param>
        /// <returns>權杖有效且密碼已更新時為 true。</returns>
        public static bool Reset(string token, string newPassword)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(newPassword))
            {
                return false;
            }

            object uname = Db.Scalar("SET XACT_ABORT ON; BEGIN TRANSACTION; "
                + "DECLARE @uname VARCHAR(50); "
                + "UPDATE violet_password_reset SET used_at = SYSUTCDATETIME(), @uname = uname "
                + "WHERE token_hash=@hash AND used_at IS NULL AND expires_at > SYSUTCDATETIME(); "
                + "IF @uname IS NOT NULL UPDATE violet_user_login SET password=@password WHERE uname=@uname; "
                + "COMMIT TRANSACTION; "
                + "SELECT @uname;",
                Db.Param("@hash", HashToken(token)), Db.Param("@password", PasswordHasher.Hash(newPassword)));
            return uname != null;
        }

        /// <summary>
        /// 計算權杖的 SHA-256 雜湊（64 個小寫十六進位字元）。
        /// </summary>
        /// <param name="token">原始權杖。</param>
        /// <returns>雜湊字串。</returns>
        static string HashToken(string token)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
                StringBuilder sb = new StringBuilder(64);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                }

                return sb.ToString();
            }
        }
    }
}
