using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
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
    public partial class sellerSignIn : System.Web.UI.Page
    {
        /// <summary>
        /// 頁面載入事件：若使用者已登入，隱藏「登入/註冊」選單，
        /// 顯示登出按鈕、個人檔案與購物車圖示，並以 Session["count"] 的列數更新購物車數量徽章。
        /// </summary>
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
        }

        /// <summary>
        /// 登出按鈕：清除 Session["user"] 後導回首頁。
        /// </summary>
        protected void btnLogout_Click(object sender, EventArgs e)
        {
            Session["user"] = null;
            Response.Redirect("~/index.aspx");
        }

        /// <summary>
        /// 登入按鈕：以 username 或 email 查詢密碼並明碼比對；成功後設定 Session["user"] 並導向首頁，
        /// 失敗則顯示錯誤訊息。
        /// </summary>
        protected void Submit_Click(object sender, EventArgs e)
        {
            //Connection（連線字串需在本機自行填入）
            SqlConnection con = new SqlConnection(<enter your database connection>);

            SqlCommand cmd = new SqlCommand("SELECT password FROM violet_user_login WHERE username=@name OR email=@name", con);
            cmd.Parameters.AddWithValue("@name", txtName.Text);

            con.Open();
            SqlDataReader read = cmd.ExecuteReader();
            string pass = "";
            while (read.Read())
            {
                pass = read["password"].ToString();
            }
            con.Close();

            if (pass == txtPassword.Text)
            {
                Session["user"] = txtName.Text;
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