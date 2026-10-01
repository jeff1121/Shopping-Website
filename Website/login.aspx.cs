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
        /// 登入按鈕：以 <see cref="UserAccounts.Authenticate"/> 驗證帳號密碼（PBKDF2 雜湊；舊明碼成功登入時自動升級）。
        /// 成功時設定 Session["uname"]、Session["user"]，以 <see cref="CartSession.LoadFromDatabase"/> 還原購物車後導向首頁；
        /// 失敗時顯示錯誤訊息。
        /// </summary>
        /// <param name="sender">觸發送出事件的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void Submit_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                return;
            }

            string name = UserAccounts.Authenticate(txtName.Text, txtPassword.Text);
            if (name != null)
            {
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
