using System;
using System.Collections.Generic;
using System.Data;
using Website.Data;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Website
{
    /// <summary>
    /// 「會員登入」頁面（login.aspx）的後置程式碼。
    /// 以使用者名稱或 Email 搭配密碼驗證身分，成功後建立 Session 並從 violet_cart 還原已儲存的購物車。
    /// </summary>
    public partial class login : System.Web.UI.Page
    {
        /// <summary>
        /// 頁面載入事件：登入頁不需要額外初始化。
        /// </summary>
        /// <param name="sender">觸發頁面載入事件的物件。</param>
        /// <param name="e">頁面載入事件資料。</param>
        protected void Page_Load(object sender, EventArgs e)
        {
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
        /// 登入按鈕：以 username 或 email 查詢 violet_user_login 取得姓名與密碼（明碼比對）。
        /// 查無帳號或密碼不符時顯示錯誤訊息；成功時設定 Session["uname"]（姓名，為各表關聯鍵）、
        /// Session["user"]（登入時輸入的帳號文字），並以 <see cref="CartSession.LoadFromDatabase"/> 還原購物車後導向首頁。
        /// </summary>
        /// <param name="sender">觸發送出事件的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void Submit_Click(object sender, EventArgs e)
        {
            DataTable account = Db.Query("SELECT uname, password FROM violet_user_login WHERE username=@name OR email=@name", Db.Param("@name", txtName.Text));

            if (account.Rows.Count == 1 && account.Rows[0]["password"].ToString() == txtPassword.Text)
            {
                string name = account.Rows[0]["uname"].ToString();
                Session["uname"] = name;
                Session["user"] = txtName.Text;
                CartSession.LoadFromDatabase(Session, name);
                Response.Redirect("~/index.aspx");
            }
            else
            {
                txtName.Focus();
                lblErrorMsg.Visible = true;
            }
        }
    }
}
