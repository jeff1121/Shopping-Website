using System;
using System.Collections.Generic;
using System.Data;
using Website.Config;
using Website.Data;
using System.Linq;
using System.Net.Mail;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Website
{
    /// <summary>
    /// 「忘記密碼」頁面（forgotpass.aspx）的後置程式碼。
    /// 流程：輸入使用者名稱/Email → 顯示安全問題 → 答對後以 <see cref="PasswordResets"/> 產生一次性權杖，
    /// 透過 SMTP（設定見 <see cref="AppSettings"/>）寄出 resetPassword.aspx 的重設連結。不再寄出密碼。
    /// </summary>
    /// <remarks>
    /// 查到的帳號姓名存於 ViewState（防竄改），安全問題答案每次都重新查詢，不保留在伺服器共用欄位；
    /// 同一 Session 答錯 <see cref="MaxAttempts"/> 次後須重新輸入帳號。
    /// </remarks>
    public partial class forgotpass : System.Web.UI.Page
    {
        /// <summary>同一 Session 允許答錯安全問題的次數。</summary>
        const int MaxAttempts = 5;

        /// <summary>Session 中記錄安全問題答錯次數的 key。</summary>
        const string AttemptsKey = "resetAttempts";

        /// <summary>步驟一查到的帳號姓名（uname），存於 ViewState。</summary>
        string ResetUname
        {
            get { return ViewState["resetUname"] as string; }
            set { ViewState["resetUname"] = value; }
        }

        /// <summary>
        /// 頁面載入事件：若使用者已登入，隱藏「登入/註冊」選單，
        /// 顯示登出按鈕、個人檔案與購物車圖示，並以 Session["count"] 的列數更新購物車數量徽章。
        /// </summary>
        /// <param name="sender">觸發頁面載入事件的物件。</param>
        /// <param name="e">頁面載入事件資料。</param>
        protected void Page_Load(object sender, EventArgs e)
        {
            // Session["user"] 不為 null 代表已登入
            if (Session["user"] != null)
            {
                btnLogout.Visible = true;
                Menu1.Visible = false;
                profileIcon.Visible = true;
                cartIcon.Visible = true;
                countItems.Visible = true;
                countItems.Text = CartSession.Count(Session).ToString();
            }
        }

        /// <summary>
        /// 登出按鈕：清除 Session["user"] 後導回首頁。
        /// </summary>
        /// <param name="sender">觸發登出事件的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void btnLogout_Click(object sender, EventArgs e)
        {
            Session["user"] = null;
            Response.Redirect("~/index.aspx");
        }

        /// <summary>
        /// 步驟一：依輸入的使用者名稱或 Email 查詢帳號。
        /// 找到時記下帳號姓名並顯示安全問題；找不到則顯示錯誤並清空輸入。
        /// </summary>
        /// <param name="sender">觸發帳號查詢的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void submit_Click(object sender, EventArgs e)
        {
            DataTable account = Db.Query("SELECT uname, secq FROM violet_user_login WHERE username=@name OR email=@name", Db.Param("@name", txtUsername.Text));
            if (account.Rows.Count == 1)
            {
                ResetUname = account.Rows[0]["uname"].ToString();
                lblSec.Text = account.Rows[0]["secq"].ToString();
                passwordPanel.Visible = true;
                lblErrorMsg.Visible = false;
                usernamePanel.Visible = false;
            }
            else
            {
                usernamePanel.Visible = true;
                passwordPanel.Visible = false;
                lblErrorMsg.Visible = true;
                txtUsername.Text = "";
                txtUsername.Focus();
            }
        }

        /// <summary>
        /// 步驟二：比對安全問題答案。答對時產生一次性權杖並寄出重設連結（有效 <see cref="PasswordResets.LifetimeMinutes"/> 分鐘），
        /// 顯示「已寄出」訊息；答錯則顯示錯誤並清空答案欄，超過 <see cref="MaxAttempts"/> 次回到步驟一。
        /// </summary>
        /// <param name="sender">觸發安全答案送出的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void submitAns_Click(object sender, EventArgs e)
        {
            int attempts = Session[AttemptsKey] == null ? 0 : (int)Session[AttemptsKey];
            DataTable account = ResetUname == null || attempts >= MaxAttempts
                ? new DataTable()
                : Db.Query("SELECT username, email, seca FROM violet_user_login WHERE uname=@uname", Db.Param("@uname", ResetUname));
            if (account.Rows.Count != 1)
            {
                ResetUname = null;
                passwordPanel.Visible = false;
                usernamePanel.Visible = true;
                lblErrorMsg.Text = attempts >= MaxAttempts ? "Too many incorrect answers. Please try again later." : "Incorrect Username / Email-ID";
                lblErrorMsg.Visible = true;
                return;
            }

            DataRow row = account.Rows[0];
            if (txtSecA.Text != row["seca"].ToString())
            {
                Session[AttemptsKey] = attempts + 1;
                lblSuccess.Visible = false;
                lblError.Visible = true;
                txtSecA.Text = "";
                txtSecA.Focus();
                return;
            }

            Session[AttemptsKey] = null;
            string token = PasswordResets.Issue(ResetUname);
            string link = Request.Url.GetLeftPart(UriPartial.Authority) + ResolveUrl("~/resetPassword.aspx") + "?token=" + HttpUtility.UrlEncode(token);
            SendResetMail(row["email"].ToString(), row["username"].ToString(), link);

            ResetUname = null;
            lblSec.Visible = false;
            txtSecA.Visible = false;
            submitAns.Visible = false;
            lblError.Visible = false;
            lblSuccess.Visible = true;
        }

        /// <summary>
        /// 以 SMTP（SMTP_HOST、SMTP_PORT，連接埠 25 以外一律 STARTTLS）寄出重設連結。
        /// 寄件者為 SMTP_FROM；SMTP_USER 有值時才使用帳密驗證。
        /// </summary>
        /// <param name="to">收件者 Email。</param>
        /// <param name="username">信件中稱呼的使用者名稱。</param>
        /// <param name="link">重設連結。</param>
        static void SendResetMail(string to, string username, string link)
        {
            using (MailMessage msg = new MailMessage())
            using (SmtpClient smtp = new SmtpClient(AppSettings.SmtpHost, AppSettings.SmtpPort))
            {
                msg.From = new MailAddress(AppSettings.SmtpFrom);
                msg.To.Add(to);
                msg.Subject = "Password Reset";
                msg.Body = "Hi " + username + ",\r\n\r\nOpen the link below within " + PasswordResets.LifetimeMinutes
                    + " minutes to set a new password. The link can only be used once.\r\n\r\n" + link
                    + "\r\n\r\nIf you did not request this, you can ignore this email.";

                if (!string.IsNullOrEmpty(AppSettings.SmtpUser))
                {
                    smtp.Credentials = new System.Net.NetworkCredential(AppSettings.SmtpUser, AppSettings.SmtpPassword);
                }

                // 連接埠 25 視為本機測試伺服器（不加密），其餘一律 STARTTLS
                smtp.EnableSsl = AppSettings.SmtpPort != 25;
                smtp.Send(msg);
            }
        }
    }
}
