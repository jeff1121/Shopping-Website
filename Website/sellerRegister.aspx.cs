using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
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
        /// 註冊送出按鈕：以參數化查詢寫入 13 個欄位到 violet_user_login 後導向登入頁。
        /// 注意：此處未帶入 uid，若資料表含 uid 欄位（與 register.aspx 相同的 14 欄結構）會因欄位數不符而失敗；
        /// 且程式不會指派 &gt; 5000 的賣家 uid，需手動於資料庫設定。
        /// </summary>
        /// <param name="sender">Submit 按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        /// <remarks>
        /// INSERT 依 violet_user_login 欄位順序寫入 uname、email、username、password、phone、dob、country、state、city、gender、address、secq、seca；
        /// txtAddress 是 HTML textarea，必須讀取 Value 而不是 Text。
        /// </remarks>
        protected void Submit_Click(object sender, EventArgs e)
        {
            // txtAddress 是 HTML textarea（runat=server），需讀取 Value
            String strAddress = txtAddress.Value;

            //Connection（透過 Db 從 Web.config 的 cmpConnectionString 讀取）
            SqlConnection con = Db.CreateConnection();

            //Insertion
            SqlCommand cmd = new SqlCommand("INSERT INTO violet_user_login VALUES (@name, @email, @username, @password, @phone, @dob, @country, @state, @city, @gender, @address, @secq, @seca)", con);
            cmd.Parameters.AddWithValue("@name", txtName.Text);
            cmd.Parameters.AddWithValue("@username", txtUsername.Text);
            cmd.Parameters.AddWithValue("@password", txtPassword.Text);
            cmd.Parameters.AddWithValue("@email", txtEmail.Text);
            cmd.Parameters.AddWithValue("@phone", txtPhone.Text);
            cmd.Parameters.AddWithValue("@dob", txtDOB.Text);
            cmd.Parameters.AddWithValue("@country", selectCountry.SelectedItem.ToString());
            cmd.Parameters.AddWithValue("@state", selectState.SelectedItem.ToString());
            cmd.Parameters.AddWithValue("@city", selectCity.SelectedItem.ToString());
            cmd.Parameters.AddWithValue("@gender", selectGender.SelectedItem.Text.ToString());
            cmd.Parameters.AddWithValue("@address", strAddress);
            cmd.Parameters.AddWithValue("@secq", txtSecurityQ.SelectedItem.ToString());
            cmd.Parameters.AddWithValue("@seca", txtSecurityA.Text);

            //Executing Query
            con.Open();
            cmd.ExecuteNonQuery();
            con.Close();

            Response.Redirect("~/login.aspx");
        }
    }
}