using System;

namespace Website.Data
{
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
        /// <summary>密碼。</summary>
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
        /// 以參數化、指定欄位的 INSERT 建立帳號。
        /// </summary>
        /// <param name="user">註冊表單的欄位值。</param>
        /// <param name="uid">已由 <see cref="GenerateUid"/> 取得的 uid。</param>
        public static void Create(NewUser user, int uid)
        {
            Db.Execute("INSERT INTO violet_user_login (uname, email, username, password, phone, dob, country, state, city, gender, address, secq, seca, uid) "
                + "VALUES (@uname, @email, @username, @password, @phone, @dob, @country, @state, @city, @gender, @address, @secq, @seca, @uid)",
                Db.Param("@uname", user.Uname), Db.Param("@email", user.Email), Db.Param("@username", user.Username),
                Db.Param("@password", user.Password), Db.Param("@phone", user.Phone), Db.Param("@dob", user.Dob),
                Db.Param("@country", user.Country), Db.Param("@state", user.State), Db.Param("@city", user.City),
                Db.Param("@gender", user.Gender), Db.Param("@address", user.Address), Db.Param("@secq", user.SecurityQuestion),
                Db.Param("@seca", user.SecurityAnswer), Db.Param("@uid", uid));
        }
    }
}
