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
    /// 「會員登入」頁面（login.aspx）的後置程式碼。
    /// 以使用者名稱或 Email 搭配密碼驗證身分，成功後建立 Session 並從 violet_cart 還原已儲存的購物車。
    /// </summary>
    public partial class login : System.Web.UI.Page
    {
        /// <summary>資料庫連線（連線字串需在本機自行填入）。</summary>
        SqlConnection con = new SqlConnection(<enter your database connection>);

        /// <summary>
        /// 頁面載入事件：登入頁不需要額外初始化。
        /// </summary>
        /// <param name="sender">觸發頁面載入事件的物件。</param>
        /// <param name="e">頁面載入事件資料。</param>
        protected void Page_Load(object sender, EventArgs e)
        {

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
        /// 登入按鈕：以 username 或 email 查詢 violet_user_login 取得姓名與密碼（明碼比對）。
        /// 成功時設定 Session["uname"]（姓名，為各表關聯鍵）、Session["user"]（登入時輸入的帳號文字），
        /// 並呼叫 <see cref="fillsavedCart"/> 還原購物車後導向首頁；失敗則顯示錯誤訊息。
        /// </summary>
        /// <param name="sender">觸發送出事件的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void Submit_Click(object sender, EventArgs e)
        {
            SqlCommand cmd = new SqlCommand("SELECT uname, password FROM violet_user_login WHERE username=@name OR email=@name", con);
            cmd.Parameters.AddWithValue("@name", txtName.Text);

            con.Open();
            SqlDataReader read = cmd.ExecuteReader();
            string pass = "";
            string name = "";
            while (read.Read())
            {
                name = read["uname"].ToString();
                pass = read["password"].ToString();
            }
            con.Close();

            // 注意：查無帳號時 pass 為空字串，若密碼欄也空白會誤判為成功（密碼欄有前端必填驗證）
            if (pass == txtPassword.Text)
            {
                Session["uname"] = name;
                Session["user"] = txtName.Text;
                Session["count"] = null;
                fillsavedCart();
                Response.Redirect("~/index.aspx");
            }
            else
            {
                txtName.Focus();
                lblErrorMsg.Visible = true;
            }
        }

        /// <summary>
        /// 從 violet_cart 讀出目前使用者（Session["uname"]）已儲存的購物車列，
        /// 重建為購物車 DataTable（欄位 sno, pimage, pname, price, quantity, total, uname）並存入 Session["count"]。
        /// sno 會重新由 1 開始編號，total 以 price × quantity 重新計算。
        /// </summary>
        /// <remarks>
        /// 注意：violet_cart 另有 sname（賣家）欄位，但此方法沒有讀取 sname，而是把 violet_cart.uname（買家姓名）放進 DataTable 的 uname 欄；
        /// 因 cart.aspx 的 GridView 將該欄標成 Seller Name，登入還原後畫面會把買家誤顯示為賣家。
        /// </remarks>
        private void fillsavedCart()
        {
            DataTable dt = new DataTable();
            DataRow dr;
            dt.Columns.Add("sno");
            dt.Columns.Add("pimage");
            dt.Columns.Add("pname");
            dt.Columns.Add("price");
            dt.Columns.Add("quantity");
            dt.Columns.Add("total");
            dt.Columns.Add("uname");
            

            // 注意：字串串接 SQL，存在 SQL Injection 風險
            String myquery = "SELECT * from violet_cart where uname='" + Session["uname"].ToString() + "'";
            SqlCommand cmd = new SqlCommand();
            cmd.CommandText = myquery;
            cmd.Connection = con;
            SqlDataAdapter da = new SqlDataAdapter();
            da.SelectCommand = cmd;
            DataSet ds = new DataSet();
            da.Fill(ds);
            if (ds.Tables[0].Rows.Count > 0)
            {
                int i = 0;
                int counter = ds.Tables[0].Rows.Count;
                while (i < counter)
                {
                    dr = dt.NewRow();
                    dr["sno"] = i + 1;
                    dr["pimage"] = ds.Tables[0].Rows[i]["pimage"].ToString();
                    dr["pname"] = ds.Tables[0].Rows[i]["pname"].ToString();
                    dr["price"] = ds.Tables[0].Rows[i]["price"].ToString();
                    dr["quantity"] = ds.Tables[0].Rows[i]["quantity"].ToString();
                    // 注意：這裡讀的是 violet_cart.uname（買家），不是 violet_cart.sname（賣家），會造成還原後 Seller Name 欄位語意錯置。
                    dr["uname"] = ds.Tables[0].Rows[i]["uname"].ToString();
                    decimal price1 = Convert.ToDecimal(ds.Tables[0].Rows[i]["price"].ToString());
                    int quantity1 = Convert.ToInt16(ds.Tables[0].Rows[i]["quantity"].ToString());
                    Decimal totalprice1 = price1 * quantity1;
                    dr["total"] = totalprice1;
                    
                    dt.Rows.Add(dr);
                    i = i + 1;
                }

            }
            else
            {
                Session["count"] = null;
            }
            Session["count"] = dt;
        }
    }
}