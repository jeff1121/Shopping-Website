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
    /// 「聯絡我們」頁面（contact.aspx）的後置程式碼。
    /// 訪客填寫姓名、Email 與留言後，寫入資料表 violet_contact。
    /// </summary>
    public partial class contact : System.Web.UI.Page
    {
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
        /// 送出按鈕：將 (姓名, Email, 留言) 以參數化、指定欄位的 INSERT 寫入 violet_contact，成功後清空表單並顯示 lblErrorMsg（此處實際作為「已送出」提示訊息）。
        /// </summary>
        /// <param name="sender">觸發送出事件的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void Submit_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                return;
            }

            // txtMessage 是 HTML textarea（runat=server），需讀取 Value 而非 Text
            String strMessage = txtMessage.Value;

            // 長度上限與 violet_contact 欄位一致（姓名、Email 50 字元，訊息 500 字元）
            if (txtName.Text.Length > 50 || txtEmail.Text.Length > 50 || strMessage.Length > 500)
            {
                lblErrorMsg.Text = "Name and Email-ID allow 50 characters, Message allows 500.";
                lblErrorMsg.ForeColor = System.Drawing.Color.Red;
                lblErrorMsg.Visible = true;
                return;
            }

            Db.Execute("INSERT INTO violet_contact (uname, email, message) VALUES (@name, @email, @message)",
                Db.Param("@name", txtName.Text), Db.Param("@email", txtEmail.Text), Db.Param("@message", strMessage));

            // 清空表單並顯示送出成功訊息
            txtName.Text = "";
            txtEmail.Text = "";
            txtMessage.Value = "";
            lblErrorMsg.Text = "Message Submitted Successfully";
            lblErrorMsg.ForeColor = System.Drawing.Color.Green;
            lblErrorMsg.Visible = true;
        }
    }
}