using System;
using Website.Data;

namespace Website
{
    /// <summary>
    /// 「重設密碼」頁面（resetPassword.aspx）的後置程式碼。
    /// 以忘記密碼信中的一次性權杖（查詢字串 token）驗證後，將密碼改為新密碼的 PBKDF2 雜湊；權杖使用一次即失效。
    /// </summary>
    public partial class resetPassword : System.Web.UI.Page
    {
        /// <summary>查詢字串中的重設權杖。</summary>
        string Token
        {
            get { return Request.QueryString["token"]; }
        }

        /// <summary>
        /// 頁面載入事件：已登入時更新頁首圖示與購物車徽章；首次載入時檢查權杖，無效則只顯示 invalidPanel。
        /// </summary>
        /// <param name="sender">觸發頁面載入事件的物件。</param>
        /// <param name="e">頁面載入事件資料。</param>
        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["user"] != null)
            {
                btnLogout.Visible = true;
                Menu1.Visible = false;
                profileIcon.Visible = true;
                cartIcon.Visible = true;
                countItems.Visible = true;
                countItems.Text = CartSession.Count(Session).ToString();
            }

            if (!Page.IsPostBack && !PasswordResets.IsValid(Token))
            {
                ShowInvalid();
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
        /// 送出新密碼：驗證通過後以 <see cref="PasswordResets.Reset"/> 在同一交易中作廢權杖並更新密碼；
        /// 成功顯示 donePanel，權杖已失效則顯示 invalidPanel。
        /// </summary>
        /// <param name="sender">btnReset 按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void btnReset_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                return;
            }

            if (PasswordResets.Reset(Token, txtPassword.Text))
            {
                resetPanel.Visible = false;
                donePanel.Visible = true;
            }
            else
            {
                ShowInvalid();
            }
        }

        /// <summary>
        /// 只顯示權杖無效的提示面板。
        /// </summary>
        void ShowInvalid()
        {
            resetPanel.Visible = false;
            invalidPanel.Visible = true;
        }
    }
}
