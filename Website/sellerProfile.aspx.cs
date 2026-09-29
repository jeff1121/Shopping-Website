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
    /// 「賣家個人資料」頁面（sellerProfile.aspx）的後置程式碼。
    /// 僅允許 uid ≥ 5000 的帳號停留，其餘導向 profile.aspx。目前更新/送出功能尚未實作完成。
    /// </summary>
    public partial class sellerProfile : System.Web.UI.Page
    {
        /// <summary>資料庫連線（連線字串需在本機自行填入）。</summary>
        readonly SqlConnection con = new SqlConnection(<enter your database connection>);
        /// <summary>目前登入帳號的 uid，用於判斷是否為賣家。</summary>
        int uid = 0000;

        /// <summary>
        /// 頁面載入事件：未登入則導向登入頁；首次載入時讀取帳號資料填入表單。
        /// uid &lt; 5000 （一般會員）會被導向 profile.aspx。最後停用所有輸入欄位並隱藏送出按鈕。
        /// </summary>
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
        protected void Update_Click(object sender, EventArgs e)
        {
            txtPhone.Enabled = true;
        }

        /// <summary>
        /// 送出按鈕：尚未實作。
        /// </summary>
        protected void Submit_Click(object sender, EventArgs e)
        {
            
        }

        /// <summary>
        /// 停用表單中所有個人資料輸入欄位（唯讀模式）。
        /// </summary>
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
        protected void btnLogout_Click(object sender, EventArgs e)
        {
            Session["user"] = null;
            Response.Redirect("~/index.aspx");
        }
    }
}