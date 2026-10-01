using System;
using System.Security.Cryptography;

namespace Website.Data
{
    /// <summary>
    /// 密碼雜湊工具：以 PBKDF2（HMAC-SHA256，100,000 次，16 位元組 salt）產生與驗證密碼雜湊。
    /// 雜湊字串格式為「PBKDF2-SHA256$次數$salt(Base64)$雜湊(Base64)」，存放於 violet_user_login.password。
    /// </summary>
    public static class PasswordHasher
    {
        /// <summary>雜湊字串的演算法前綴。</summary>
        const string Prefix = "PBKDF2-SHA256";
        /// <summary>PBKDF2 疊代次數。</summary>
        public const int Iterations = 100000;
        /// <summary>salt 長度（位元組）。</summary>
        const int SaltSize = 16;
        /// <summary>雜湊長度（位元組）。</summary>
        const int HashSize = 32;

        /// <summary>
        /// 以隨機 salt 產生密碼雜湊字串。
        /// </summary>
        /// <param name="password">使用者輸入的密碼。</param>
        /// <returns>可直接存入資料庫的雜湊字串。</returns>
        public static string Hash(string password)
        {
            byte[] salt = new byte[SaltSize];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] hash = Derive(password, salt, Iterations);
            return Prefix + "$" + Iterations + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        /// <summary>
        /// 驗證密碼是否符合資料庫中儲存的值。
        /// 儲存值不是雜湊格式時視為舊版明碼，比對成功時 <paramref name="needsUpgrade"/> 為 true，呼叫端應改存雜湊。
        /// </summary>
        /// <param name="password">使用者輸入的密碼。</param>
        /// <param name="stored">violet_user_login.password 的值。</param>
        /// <param name="needsUpgrade">儲存值需要改存為目前格式的雜湊時為 true。</param>
        /// <returns>密碼正確時為 true。</returns>
        public static bool Verify(string password, string stored, out bool needsUpgrade)
        {
            needsUpgrade = false;
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored))
            {
                return false;
            }

            string[] parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != Prefix)
            {
                // 舊版明碼密碼
                bool match = FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(password), System.Text.Encoding.UTF8.GetBytes(stored));
                needsUpgrade = match;
                return match;
            }

            int iterations;
            byte[] salt;
            byte[] expected;
            try
            {
                iterations = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] actual = Derive(password, salt, iterations);
            bool ok = FixedTimeEquals(actual, expected);
            needsUpgrade = ok && iterations < Iterations;
            return ok;
        }

        /// <summary>
        /// 以 PBKDF2（HMAC-SHA256）計算雜湊。
        /// </summary>
        /// <param name="password">密碼。</param>
        /// <param name="salt">salt。</param>
        /// <param name="iterations">疊代次數。</param>
        /// <returns>長度為 <see cref="HashSize"/> 的雜湊。</returns>
        static byte[] Derive(string password, byte[] salt, int iterations)
        {
            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(HashSize);
            }
        }

        /// <summary>
        /// 固定時間比較兩個位元組陣列，避免以回應時間推測內容。
        /// </summary>
        /// <param name="a">第一個陣列。</param>
        /// <param name="b">第二個陣列。</param>
        /// <returns>長度與內容皆相同時為 true。</returns>
        static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }

            return diff == 0;
        }
    }
}
