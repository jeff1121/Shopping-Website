using System;
using System.Data;
using System.Data.SqlClient;

namespace Website.Data
{
    /// <summary>
    /// <see cref="UserAccounts.Create"/> 的結果。
    /// </summary>
    public enum CreateResult
    {
        /// <summary>帳號已建立。</summary>
        Created,
        /// <summary>姓名、Email、使用者名稱或電話已被使用。</summary>
        Duplicate,
        /// <summary>至少一個欄位超過資料表允許的長度。</summary>
        TooLong
    }

    /// <summary>
    /// 註冊帳號時要寫入 violet_user_login 的欄位值（會員與賣家註冊頁共用）。
    /// </summary>
    public sealed class NewUser
    {
        /// <summary>姓名（uname，各資料表的關聯鍵）。</summary>
        public string Uname { get; set; }
        /// <summary>Email。</summary>
        public string Email { get; set; }
        /// <summary>使用者名稱。</summary>
        public string Username { get; set; }
        /// <summary>密碼（明碼；<see cref="UserAccounts.Create"/> 會雜湊後再寫入）。</summary>
        public string Password { get; set; }
        /// <summary>電話（資料表為 DECIMAL(10,0)）。</summary>
        public string Phone { get; set; }
        /// <summary>生日。</summary>
        public string Dob { get; set; }
        /// <summary>國家。</summary>
        public string Country { get; set; }
        /// <summary>州／省。</summary>
        public string State { get; set; }
        /// <summary>城市。</summary>
        public string City { get; set; }
        /// <summary>性別。</summary>
        public string Gender { get; set; }
        /// <summary>地址。</summary>
        public string Address { get; set; }
        /// <summary>安全問題。</summary>
        public string SecurityQuestion { get; set; }
        /// <summary>安全問題答案。</summary>
        public string SecurityAnswer { get; set; }
    }

    /// <summary>
    /// 會員帳號（violet_user_login）的建立與 uid 指派。
    /// uid 範圍代表角色：1～4998 為一般會員，5001 以上為賣家（見 profile、sellerProfile）。
    /// </summary>
    public static class UserAccounts
    {
        /// <summary>一般會員 uid 下限（含）。</summary>
        public const int CustomerUidMin = 1;
        /// <summary>一般會員 uid 上限（不含）。</summary>
        public const int CustomerUidMax = 4999;
        /// <summary>賣家 uid 下限（含）；uid 大於 5000 即視為賣家。</summary>
        public const int SellerUidMin = 5001;
        /// <summary>賣家 uid 上限（不含）。</summary>
        public const int SellerUidMax = 10000;

        /// <summary>
        /// 判斷 uid 是否代表賣家。
        /// </summary>
        /// <param name="uid">violet_user_login.uid。</param>
        /// <returns>uid 大於 5000 時為 true。</returns>
        public static bool IsSeller(int uid)
        {
            return uid > 5000;
        }

        /// <summary>
        /// 在指定範圍內隨機抽出尚未被使用的 uid。
        /// </summary>
        /// <param name="minInclusive">下限（含）。</param>
        /// <param name="maxExclusive">上限（不含）。</param>
        /// <returns>未被使用的 uid。</returns>
        public static int GenerateUid(int minInclusive, int maxExclusive)
        {
            Random random = new Random();
            while (true)
            {
                int candidate = random.Next(minInclusive, maxExclusive);
                int used = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM violet_user_login WHERE uid=@uid", Db.Param("@uid", candidate)));
                if (used == 0)
                {
                    return candidate;
                }
            }
        }

        /// <summary>
        /// 以參數化、指定欄位的 INSERT 建立帳號；密碼以 <see cref="PasswordHasher"/> 雜湊後儲存。
        /// 姓名、Email、使用者名稱或電話重複，或欄位超過資料表長度時不寫入，改以回傳值告知頁面。
        /// </summary>
        /// <param name="user">註冊表單的欄位值（呼叫前頁面驗證控制項須已通過）。</param>
        /// <param name="uid">已由 <see cref="GenerateUid"/> 取得的 uid。</param>
        /// <returns>建立結果。</returns>
        public static CreateResult Create(NewUser user, int uid)
        {
            try
            {
                Insert(user, uid);
                return CreateResult.Created;
            }
            catch (SqlException ex) when (Db.IsDuplicateKey(ex))
            {
                return CreateResult.Duplicate;
            }
            catch (SqlException ex) when (Db.IsTruncation(ex))
            {
                return CreateResult.TooLong;
            }
        }

        /// <summary>
        /// 判斷登入帳號（使用者名稱或 Email）是否為賣家（uid &gt; 5000）。
        /// </summary>
        /// <param name="login">Session["user"] 的值；null 代表未登入。</param>
        /// <returns>帳號存在且為賣家時為 true。</returns>
        public static bool IsSellerLogin(object login)
        {
            if (login == null)
            {
                return false;
            }

            object uid = Db.Scalar("SELECT uid FROM violet_user_login WHERE username=@login OR email=@login", Db.Param("@login", login.ToString()));
            return uid != null && IsSeller(Convert.ToInt32(uid));
        }

        /// <summary>
        /// 執行建立帳號的 INSERT。
        /// </summary>
        /// <param name="user">註冊表單的欄位值。</param>
        /// <param name="uid">帳號的 uid。</param>
        private static void Insert(NewUser user, int uid)
        {
            Db.Execute("INSERT INTO violet_user_login (uname, email, username, password, phone, dob, country, state, city, gender, address, secq, seca, uid) "
                + "VALUES (@uname, @email, @username, @password, @phone, @dob, @country, @state, @city, @gender, @address, @secq, @seca, @uid)",
                Db.Param("@uname", user.Uname), Db.Param("@email", user.Email), Db.Param("@username", user.Username),
                Db.Param("@password", PasswordHasher.Hash(user.Password)), Db.Param("@phone", user.Phone), Db.Param("@dob", user.Dob),
                Db.Param("@country", user.Country), Db.Param("@state", user.State), Db.Param("@city", user.City),
                Db.Param("@gender", user.Gender), Db.Param("@address", user.Address), Db.Param("@secq", user.SecurityQuestion),
                Db.Param("@seca", user.SecurityAnswer), Db.Param("@uid", uid));
        }

        /// <summary>
        /// 驗證登入：以 username 或 email 查詢帳號（必須剛好一筆），以 <see cref="PasswordHasher.Verify"/> 比對密碼。
        /// 舊版明碼密碼比對成功時，立即改存為雜湊。
        /// </summary>
        /// <param name="login">使用者輸入的使用者名稱或 Email。</param>
        /// <param name="password">使用者輸入的密碼。</param>
        /// <returns>成功時回傳帳號姓名（uname）；失敗時回傳 null。</returns>
        public static string Authenticate(string login, string password)
        {
            DataTable account = Db.Query("SELECT uname, password FROM violet_user_login WHERE username=@name OR email=@name", Db.Param("@name", login));
            if (account.Rows.Count != 1)
            {
                return null;
            }

            string uname = account.Rows[0]["uname"].ToString();
            bool needsUpgrade;
            if (!PasswordHasher.Verify(password, account.Rows[0]["password"].ToString(), out needsUpgrade))
            {
                return null;
            }

            if (needsUpgrade)
            {
                Db.Execute("UPDATE violet_user_login SET password=@password WHERE uname=@uname",
                    Db.Param("@password", PasswordHasher.Hash(password)), Db.Param("@uname", uname));
            }

            return uname;
        }
    }
}
