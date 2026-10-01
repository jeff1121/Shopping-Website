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
    /// 「賣家登入」頁面（sellerSignIn.aspx）的後置程式碼。
    /// 同樣以 violet_user_login 驗證帳密；與 login.aspx 不同的是只設定 Session["user"]，
    /// 不設定 Session["uname"] 也不還原購物車。
    /// </summary>
    /// <remarks>
    /// 此頁沒有檢查 uid 是否 &gt; 5000，因此任何 violet_user_login 帳號只要密碼相符都可登入並導向首頁。
    /// </remarks>
    public partial class sellerSignIn : System.Web.UI.Page
    {
        /// <summary>
        /// 頁面載入事件：若使用者已登入，隱藏「登入/註冊」選單，
        /// 顯示登出按鈕、個人檔案與購物車圖示，並以 Session["count"] 的列數更新購物車數量徽章。
        /// </summary>
        /// <param name="sender">ASP.NET Web Forms 傳入的事件來源。</param>
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
        /// <param name="sender">登出按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        /// <remarks>不清除 Session["count"] 或 Session["uname"]。</remarks>
        protected void btnLogout_Click(object sender, EventArgs e)
        {
            Session["user"] = null;
            Response.Redirect("~/index.aspx");
        }

        /// <summary>
        /// 登入按鈕：與 login.aspx 相同，以 <see cref="UserAccounts.Authenticate"/> 驗證帳號密碼；
        /// 成功時設定 Session["uname"]、Session["user"] 並還原購物車後導向首頁；失敗則顯示錯誤訊息。
        /// </summary>
        /// <param name="sender">Submit 按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void Submit_Click(object sender, EventArgs e)
        {
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