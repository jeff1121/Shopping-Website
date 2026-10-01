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
    /// 「賣家註冊」頁面（sellerRegister.aspx）的後置程式碼。
    /// 表單與一般會員註冊相同，同樣寫入 violet_user_login（並未使用 violet_seller_login）。
    /// </summary>
    /// <remarks>
    /// 此頁不會產生或寫入 uid，也不會寫入 violet_seller_login；在含 uid 的實際資料表結構下，位置式 INSERT 容易因欄位數不符失敗。
    /// 若需成為賣家，仍需在資料庫手動把 violet_user_login.uid 設為 &gt; 5000。
    /// </remarks>
    public partial class sellerRegister : System.Web.UI.Page
    {
        /// <summary>
        /// 頁面載入事件：處理共用頁首的登入狀態顯示；首次載入時將生日驗證器的比較值設為今天。
        /// </summary>
        /// <param name="sender">ASP.NET Web Forms 傳入的事件來源。</param>
        /// <param name="e">頁面載入事件資料。</param>
        /// <remarks>登入時讀取 Session["count"] 作為購物車徽章；未登入也可使用註冊表單。</remarks>
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
        /// <param name="sender">登出按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        /// <remarks>不清除 Session["count"] 或 Session["uname"]。</remarks>
        protected void btnLogout_Click(object sender, EventArgs e)
        {
            Session["user"] = null;
            Response.Redirect("~/index.aspx");
        }

        /// <summary>
        /// 註冊送出按鈕：以 <see cref="UserAccounts.GenerateUid"/> 抽出 5001 以上未使用的賣家 uid，
        /// 再以 <see cref="UserAccounts.Create"/> 建立帳號，最後導向登入頁。
        /// </summary>
        /// <param name="sender">Submit 按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void Submit_Click(object sender, EventArgs e)
        {
            // 伺服器端再次執行驗證控制項，避免略過瀏覽器端驗證直接送出
            if (!Page.IsValid)
            {
                return;
            }

            int uid = UserAccounts.GenerateUid(UserAccounts.SellerUidMin, UserAccounts.SellerUidMax);
            CreateResult result = UserAccounts.Create(new NewUser
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

            if (result == CreateResult.Created)
            {
                Response.Redirect("~/login.aspx");
                return;
            }

            lblRegisterError.Text = result == CreateResult.Duplicate
                ? "An account with this Name, Email-ID, Username or Phone Number already exists."
                : "One or more fields are too long. Name and Email-ID allow 50 characters, Username and Security Answer allow 20.";
            lblRegisterError.Visible = true;
        }
    }
}