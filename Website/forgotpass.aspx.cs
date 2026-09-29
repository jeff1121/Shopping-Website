using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Mail;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Website
{
    /// <summary>
    /// 「忘記密碼」頁面（forgotpass.aspx）的後置程式碼。
    /// 流程：輸入使用者名稱/Email → 顯示安全問題 → 答對後透過 Gmail SMTP 將原密碼寄到註冊信箱。
    /// </summary>
    public partial class forgotpass : System.Web.UI.Page
    {
        /// <summary>資料庫連線（連線字串需在本機自行填入）。</summary>
        SqlConnection con = new SqlConnection(<enter your database connection>);
        // 注意：以下為 static 欄位，會在所有使用者請求間共用，多人同時操作時會互相覆蓋
        /// <summary>資料庫中的安全問題答案。</summary>
        static string secans = "";
        /// <summary>帳號的 Email，作為密碼信件收件者。</summary>
        static string emailid = "";
        /// <summary>帳號的明碼密碼。</summary>
        static string pass = "";
        /// <summary>帳號的使用者名稱（username），用於信件稱呼。</summary>
        static string uname = "";

        /// <summary>
        /// 頁面載入事件：若使用者已登入，隱藏「登入/註冊」選單，
        /// 顯示登出按鈕、個人檔案與購物車圖示，並以 Session["count"] 的列數更新購物車數量徽章。
        /// </summary>
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
                DataTable dt = new DataTable();
                dt = (DataTable)Session["count"];
                if (dt != null)
                {
                    countItems.Text = dt.Rows.Count.ToString();
                }
                else
                {
                    countItems.Text = "0";
                }
            }
        }

        /// <summary>
        /// 登出按鈕：清除 Session["user"] 後導回首頁。
        /// </summary>
        protected void btnLogout_Click(object sender, EventArgs e)
        {
            Session["user"] = null;
            Response.Redirect("~/index.aspx");
        }

        /// <summary>
        /// 步驟一：依輸入的使用者名稱或 Email 查詢帳號。
        /// 找到時快取密碼、安全問題/答案與 Email，切換到安全問題面板；找不到則顯示錯誤並清空輸入。
        /// </summary>
        protected void submit_Click(object sender, EventArgs e)
        {
            SqlCommand cmd = new SqlCommand("SELECT username, password, secq, seca, email FROM violet_user_login WHERE username=@username OR email=@username", con);
            cmd.Parameters.AddWithValue("@username", txtUsername.Text);

            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            if (dr.HasRows)
            {
                while (dr.Read())
                {
                    uname = dr["username"].ToString();
                    lblSec.Text = dr["secq"].ToString();
                    pass = dr["password"].ToString();
                    secans = dr["seca"].ToString();
                    emailid = dr["email"].ToString();
                }
                con.Close();
                passwordPanel.Visible = true;
                lblErrorMsg.Visible = false;
                usernamePanel.Visible = false;
            }
            else
            {
                // 注意：此分支未關閉連線
                usernamePanel.Visible = true;
                passwordPanel.Visible = false;
                lblErrorMsg.Visible = true;
                txtUsername.Text = "";
                txtUsername.Focus();
            }
        }

        /// <summary>
        /// 步驟二：比對安全問題答案。答對時以 Gmail SMTP（smtp.gmail.com:587、SSL）寄出含原密碼的信件並導向登入頁；
        /// 答錯則顯示錯誤並清空答案欄。寄件帳號與密碼需自行替換下方佔位字串。
        /// </summary>
        protected void submitAns_Click(object sender, EventArgs e)
        {
            if (txtSecA.Text == secans)
            {
                // 佔位字串 "enter email id" / "enter password" 需替換為實際寄件帳號（Gmail 請使用應用程式密碼）
                MailMessage Msg = new MailMessage();
                Msg.From = new MailAddress("enter email id");
                Msg.To.Add(emailid);
                Msg.Subject = "Password Recovery";
                Msg.Body = "Hi " + uname + " you're password is " + pass;

                SmtpClient smtp = new SmtpClient();
                smtp.Host = "smtp.gmail.com";
                smtp.Port = 587;
                smtp.Credentials = new System.Net.NetworkCredential("enter email id", "enter password");
                smtp.EnableSsl = true;
                smtp.Send(Msg);
                Msg = null;
                lblError.Visible = false;
                lblSuccess.Visible = true;
                Response.Redirect("~/login.aspx");
            }
            else
            {
                lblSuccess.Visible = false;
                lblError.Visible = true;
                txtSecA.Text = "";
                txtSecA.Focus();
            }
        }
    }
}
