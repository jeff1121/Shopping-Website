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
                // Session["count"] 存放購物車 DataTable，列數即購物車品項數
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
        /// <param name="sender">觸發登出事件的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void btnLogout_Click(object sender, EventArgs e)
        {
            Session["user"] = null;
            Response.Redirect("~/index.aspx");
        }

        /// <summary>
        /// 送出按鈕：以參數化查詢將 (姓名, Email, 留言) 依欄位順序寫入 violet_contact，
        /// 成功後清空表單並顯示 lblErrorMsg（此處實際作為「已送出」提示訊息）。
        /// </summary>
        /// <param name="sender">觸發送出事件的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void Submit_Click(object sender, EventArgs e)
        {
            // txtMessage 是 HTML textarea（runat=server），需讀取 Value 而非 Text
            String strMessage = txtMessage.Value;

            // 本頁在方法內建立區域連線物件，透過 Db 從 Web.config 的 cmpConnectionString 讀取。
            SqlConnection con = Db.CreateConnection();

            // 未指定欄位名稱，依 violet_contact 欄位順序 uname, email, message 寫入。
            SqlCommand cmd = new SqlCommand("INSERT INTO violet_contact VALUES (@name, @email, @message)", con);
            cmd.Parameters.AddWithValue("@name", txtName.Text);
            cmd.Parameters.AddWithValue("@email", txtEmail.Text);
            cmd.Parameters.AddWithValue("@message", strMessage);

            // 執行留言寫入。
            con.Open();
            cmd.ExecuteNonQuery();
            con.Close();

            // 清空表單並顯示送出成功訊息
            txtName.Text = "";
            txtEmail.Text = "";
            txtMessage.Value = "";
            lblErrorMsg.Visible = true;
        }
    }
}