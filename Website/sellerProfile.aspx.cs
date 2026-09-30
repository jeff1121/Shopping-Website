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
    /// 「賣家個人資料」頁面（sellerProfile.aspx）的後置程式碼。
    /// 以 violet_user_login 讀取登入者資料；程式碼阻擋 uid &lt; 5000 的帳號並導向 profile.aspx，
    /// 因此 uid = 5000 也會通過，和專案其他頁面「uid &gt; 5000 才是賣家」的慣例不完全一致。目前更新/送出功能尚未實作完成。
    /// </summary>
    /// <remarks>
    /// 此頁不顯示購物車圖示或徽章，只切換登入/註冊選單與登出按鈕；一般賣家管理實際上多在 profile.aspx 完成。
    /// </remarks>
    public partial class sellerProfile : System.Web.UI.Page
    {
        /// <summary>資料庫連線（透過 Db 從 Web.config 的 cmpConnectionString 讀取）。</summary>
        readonly SqlConnection con = Db.CreateConnection();
        /// <summary>目前登入帳號的 uid，用於判斷是否為賣家。</summary>
        int uid = 0000;

        /// <summary>
        /// 頁面載入事件：未登入則導向登入頁；首次載入時讀取帳號資料填入表單。
        /// uid &lt; 5000 （一般會員）會被導向 profile.aspx；uid = 5000 因條件寫法會被允許停留。
        /// 最後停用所有輸入欄位並隱藏送出按鈕。
        /// </summary>
        /// <param name="sender">ASP.NET Web Forms 傳入的事件來源。</param>
        /// <param name="e">頁面載入事件資料。</param>
        /// <remarks>
        /// 讀取 Session["user"] 後以 username 或 email 查詢 violet_user_login；uid 只在首次載入設定，
        /// PostBack 時欄位值回到 0，導致同頁事件可能先被導向 profile.aspx。
        /// </remarks>
        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["user"] != null)
            {
                btnLogout.Visible = true;
                Menu1.Visible = false;
            }
            else
            {
                Response.Redirect("~/login.aspx");
            }

            SqlCommand cmd = new SqlCommand("SELECT * FROM violet_user_login where username=@name OR email=@name", con);
            cmd.Parameters.AddWithValue("@name", Session["user"]);

            if (!Page.IsPostBack)
            {
                validateAge.ValueToCompare = DateTime.Today.ToShortDateString();
                con.Open();
                SqlDataReader read = cmd.ExecuteReader();
                while (read.Read())
                {
                    txtName.Text = read["uname"].ToString();
                    txtEmail.Text = read["email"].ToString();
                    txtUsername.Text = Session["user"].ToString();
                    txtPhone.Text = read["phone"].ToString();
                    txtDOB.Text = read["dob"].ToString();
                    txtDOB.Text = txtDOB.Text.Substring(0, 10);
                    selectCountry.Text = read["country"].ToString();
                    selectState.Text = read["state"].ToString();
                    selectCity.Text = read["city"].ToString();
                    lblGender.Text = read["gender"].ToString();
                    txtAddress.InnerText = read["address"].ToString();
                    uid = Convert.ToInt32(read["uid"]);
                }
                con.Close();
            }

            // 注意：uid 只在首次載入時讀取，PostBack 時為 0，會被導向 profile.aspx
            if (uid < 5000)
            {
                Response.Redirect("~/profile.aspx");
            }

            //Disable Fields
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