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
    /// 「賣家個人資料」頁面（sellerProfile.aspx）的後置程式碼。
    /// 以 violet_user_login 讀取登入者資料；非賣家（<see cref="UserAccounts.IsSeller"/>，uid ≤ 5000）會被導向 profile.aspx。
    /// 目前更新/送出功能尚未實作完成。
    /// </summary>
    /// <remarks>
    /// 此頁不顯示購物車圖示或徽章，只切換登入/註冊選單與登出按鈕；一般賣家管理實際上多在 profile.aspx 完成。
    /// </remarks>
    public partial class sellerProfile : System.Web.UI.Page
    {
        /// <summary>
        /// 目前登入帳號的 uid，用於判斷是否為賣家。存於 ViewState，PostBack 後仍保留。
        /// </summary>
        int uid
        {
            get { return ViewState["uid"] == null ? 0 : (int)ViewState["uid"]; }
            set { ViewState["uid"] = value; }
        }

        /// <summary>
        /// 頁面載入事件：未登入則導向登入頁；首次載入時讀取帳號資料填入表單。
        /// 非賣家（uid ≤ 5000）會被導向 profile.aspx。最後停用所有輸入欄位並隱藏送出按鈕。
        /// </summary>
        /// <param name="sender">ASP.NET Web Forms 傳入的事件來源。</param>
        /// <param name="e">頁面載入事件資料。</param>
        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["user"] == null)
            {
                Response.Redirect("~/login.aspx");
                return;
            }

            btnLogout.Visible = true;
            Menu1.Visible = false;

            if (!Page.IsPostBack)
            {
                validateAge.ValueToCompare = DateTime.Today.ToShortDateString();
                DataTable account = Db.Query("SELECT * FROM violet_user_login WHERE username=@name OR email=@name", Db.Param("@name", Session["user"].ToString()));
                if (account.Rows.Count == 1)
                {
                    DataRow read = account.Rows[0];
                    txtName.Text = read["uname"].ToString();
                    txtEmail.Text = read["email"].ToString();
                    txtUsername.Text = Session["user"].ToString();
                    txtPhone.Text = read["phone"].ToString();
                    // 以伺服器區域設定的短日期格式顯示，與 validateAge 的比較值格式一致
                    txtDOB.Text = Convert.ToDateTime(read["dob"]).ToShortDateString();
                    selectCountry.Text = read["country"].ToString();
                    selectState.Text = read["state"].ToString();
                    selectCity.Text = read["city"].ToString();
                    lblGender.Text = read["gender"].ToString();
                    txtAddress.InnerText = read["address"].ToString();
                    uid = Convert.ToInt32(read["uid"]);
                }
            }

            if (!UserAccounts.IsSeller(uid))
            {
                Response.Redirect("~/profile.aspx");
                return;
            }

            disableInput();
            Submit.Visible = false;
        }

        /// <summary>
        /// 更新按鈕：目前僅開放電話欄位可編輯（功能未完成）。
        /// </summary>
        /// <param name="sender">Update 按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        /// <remarks>沒有顯示 Submit 按鈕，也沒有把修改寫回資料庫。</remarks>
        protected void Update_Click(object sender, EventArgs e)
        {
            txtPhone.Enabled = true;
        }

        /// <summary>
        /// 送出按鈕：尚未實作。
        /// </summary>
        /// <param name="sender">Submit 按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        /// <remarks>目前不讀寫任何 Session 或資料表。</remarks>
        protected void Submit_Click(object sender, EventArgs e)
        {
            
        }

        /// <summary>
        /// 停用表單中所有個人資料輸入欄位（唯讀模式）。
        /// </summary>
        /// <remarks>此方法只影響 UI 啟用狀態，不會保存或重載資料。</remarks>
        public void disableInput()
        {
            txtName.Enabled = false;
            txtEmail.Enabled = false;
            txtUsername.Enabled = false;
            txtPhone.Enabled = false;
            txtDOB.Enabled = false;
            txtAddress.Disabled = true;
            selectCountry.Enabled = false;
            selectCity.Enabled = false;
            selectState.Enabled = false;
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
    }
}