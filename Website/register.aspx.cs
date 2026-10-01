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
    /// 「一般會員註冊」頁面（register.aspx）的後置程式碼。
    /// 將表單資料寫入 violet_user_login，再為新帳號產生 1～4998 的唯一 uid（uid ≤ 5000 視為一般會員）。
    /// </summary>
    public partial class register : System.Web.UI.Page
    {

        /// <summary>
        /// 頁面載入事件：處理共用頁首的登入狀態顯示；首次載入時將生日驗證器的比較值設為今天，
        /// 確保生日不會晚於今天。
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

            if (!Page.IsPostBack)
            {
                validateAge.ValueToCompare = DateTime.Today.ToShortDateString();
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
        /// 註冊送出按鈕：以 <see cref="UserAccounts.GenerateUid"/> 抽出 1～4998 間未使用的一般會員 uid，
        /// 再以 <see cref="UserAccounts.Create"/> 建立帳號，最後導向登入頁。密碼以 PBKDF2 雜湊儲存。
        /// </summary>
        /// <param name="sender">觸發送出事件的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void Submit_Click(object sender, EventArgs e)
        {
            int uid = UserAccounts.GenerateUid(UserAccounts.CustomerUidMin, UserAccounts.CustomerUidMax);
            UserAccounts.Create(new NewUser
            {
                Uname = txtName.Text,
                Email = txtEmail.Text,
                Username = txtUsername.Text,
                Password = txtPassword.Text,
                Phone = txtPhone.Text,
                Dob = txtDOB.Text,
                Country = selectCountry.SelectedItem.Text,
                State = selectState.SelectedItem.Text,
                City = selectCity.SelectedItem.Text,
                Gender = selectGender.SelectedItem.Text,
                // txtAddress 是 HTML textarea（runat=server），需讀取 Value
                Address = txtAddress.Value,
                SecurityQuestion = txtSecurityQ.SelectedItem.Text,
                SecurityAnswer = txtSecurityA.Text
            }, uid);

            Response.Redirect("~/login.aspx");
        }
    }
}