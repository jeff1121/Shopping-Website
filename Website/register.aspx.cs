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
    /// 「一般會員註冊」頁面（register.aspx）的後置程式碼。
    /// 將表單資料寫入 violet_user_login，再為新帳號產生 1～4998 的唯一 uid（uid ≤ 5000 視為一般會員）。
    /// </summary>
    public partial class register : System.Web.UI.Page
    {

        /// <summary>資料庫連線（透過 Db 從 Web.config 的 cmpConnectionString 讀取）。</summary>
        readonly SqlConnection con = Db.CreateConnection();

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
        /// <param name="sender">觸發登出事件的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void btnLogout_Click(object sender, EventArgs e)
        {
            Session["user"] = null;
            Response.Redirect("~/index.aspx");
        }

        /// <summary>
        /// 註冊送出按鈕：以參數化查詢將 14 個欄位依順序寫入 violet_user_login（uid 先暫定為 0），
        /// 再呼叫 <see cref="generateUID"/> 指派唯一 uid，最後導向登入頁。密碼以明碼儲存。
        /// </summary>
        /// <param name="sender">觸發送出事件的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void Submit_Click(object sender, EventArgs e)
        {
            // txtAddress 是 HTML textarea（runat=server），需讀取 Value
            String strAddress = txtAddress.Value;

            // 位置式 INSERT，欄位順序必須與 violet_user_login 資料表定義一致。
            SqlCommand cmd = new SqlCommand("INSERT INTO violet_user_login VALUES (@name, @email, @username, @password, @phone, @dob, @country, @state, @city, @gender, @address, @secq, @seca, @uid)", con);
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
            cmd.Parameters.AddWithValue("@uid", 0000);

            // 先以 uid=0 建立帳號，再由 generateUID() 改成一般會員 uid。
            con.Open();
            cmd.ExecuteNonQuery();
            con.Close();

            generateUID();

            Response.Redirect("~/login.aspx");
        }

        /// <summary>
        /// 隨機產生 1～4998 且尚未被使用的 uid，並更新到剛註冊的帳號（以 txtUsername 辨識）。
        /// uid 的範圍用來區分角色：≤ 5000 為一般會員、&gt; 5000 為賣家（見 profile / sellerProfile）。
        /// </summary>
        public void generateUID()
        {
            bool flag = true;
            Random random = new Random();
            // 重複抽號直到找到未被使用的 uid
            while (flag == true)
            {
                int num = random.Next(1, 4999);
                SqlCommand check = new SqlCommand("SELECT COUNT(*) FROM violet_user_login where uid=@num", con);
                check.Parameters.AddWithValue("@num", num);
                con.Open();
                int uid = (int)check.ExecuteScalar();
                if (uid > 0)
                {
                    // 注意：此處 continue 前未關閉連線，下一輪 con.Open() 會拋出例外
                    continue;
                }
                else
                {
                    SqlCommand cmd = new SqlCommand("update violet_user_login set uid=@num where username=@username", con);
                    cmd.Parameters.AddWithValue("@num", num);
                    cmd.Parameters.AddWithValue("@username", txtUsername.Text);
                    cmd.ExecuteNonQuery();
                    flag = false;
                    con.Close();
                }
            }

        }

    }
}