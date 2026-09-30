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
    /// 「結帳」頁面（checkout.aspx）的後置程式碼。
    /// 顯示購物車明細、產生訂單編號與日期；按下「Place Order」後將每一項寫入 violet_order，
    /// 並清除該使用者在 violet_cart 的資料。無任何金流介接。
    /// </summary>
    public partial class checkout : System.Web.UI.Page
    {
        /// <summary>資料庫連線（透過 Db 從 Web.config 的 cmpConnectionString 讀取）。</summary>
        SqlConnection con = Db.CreateConnection();

        /// <summary>
        /// 頁面載入事件：已登入時更新頁首圖示與購物車徽章；首次載入時填入訂單明細、
        /// 以今天日期作為訂單日期並產生訂單編號。未登入時不顯示任何訂單資料。
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

                if (!IsPostBack)
                {
                    filldata();
                    orderDate.Text = DateTime.Now.ToShortDateString();
                    calculateOrderID();
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
        /// 下單按鈕：切換為「訂單完成」面板，將購物車每一列以相同訂單編號寫入 violet_order
        /// （欄位順序 uname, pname, orderID, orderDate, quantity, total），最後刪除該使用者的 violet_cart 資料。
        /// 注意：未清空 Session["count"]，因此下單後購物車徽章仍會顯示舊數量，直到重新登入。
        /// </summary>
        /// <param name="sender">觸發下單事件的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void btnCheckout_Click(object sender, EventArgs e)
        {
            btnCheckout.Visible = false;
            Panel1.Visible = false;
            completeOrder.Visible = true;
            orderIDCompleted.Text = orderID.Text;
            orderDateCompleted.Text = orderDate.Text;

            DataTable dt;
            dt = (DataTable)Session["count"];

            for (int i = 0; i <= dt.Rows.Count - 1; i++)
            {
                // 查出登入者姓名（每一列都重複查詢一次）
                String uname = "";
                con.Open();
                SqlCommand cmd = new SqlCommand("SELECT uname FROM violet_user_login WHERE username=@user OR email=@user", con);
                cmd.Parameters.AddWithValue("@user", Session["user"].ToString());
                SqlDataReader read = cmd.ExecuteReader();
                while (read.Read())
                {
                    uname = read["uname"].ToString();
                }
                con.Close();
                
                // 位置式 INSERT（字串串接，存在 SQL Injection 風險）
                String updatepass = "INSERT INTO violet_order VALUES('" + uname.ToString() + "','" + dt.Rows[i]["pname"] + "','" + orderID.Text + "','" + orderDate.Text + "'," + dt.Rows[i]["quantity"] + "," + dt.Rows[i]["total"] + ")";
                con.Open();
                SqlCommand cmd1 = new SqlCommand();
                cmd1.CommandText = updatepass;
                cmd1.Connection = con;
                cmd1.ExecuteNonQuery();
                con.Close();
            }

            // 清除該使用者已結帳的購物車資料
            SqlCommand cmd2 = new SqlCommand();
            String delete = "DELETE FROM violet_cart WHERE uname='" + Session["uname"] + "'";
            con.Open();
            cmd2.CommandText = delete;
            cmd2.Connection = con;
            cmd2.ExecuteNonQuery();
            con.Close();
        }

        /// <summary>
        /// 填入結帳明細 GridView。
        /// 一般情況（無 ?id=）直接顯示 Session["count"] 的購物車內容；
        /// 若帶有 ?id=商品&amp;quantity=數量，則會將該商品追加到 Session 購物車後再顯示（不寫入 violet_cart）。
        /// 表尾顯示 Grand Total。
        /// </summary>
        public void filldata()
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

            if (Request.QueryString["id"] != null)
            {
                if (Session["count"] == null)
                {
                    dr = dt.NewRow();
                    String myquery = "SELECT * FROM violet_products where pname='" + Request.QueryString["id"] + "'";
                    SqlCommand cmd = new SqlCommand();
                    cmd.CommandText = myquery;
                    cmd.Connection = con;
                    SqlDataAdapter da = new SqlDataAdapter();
                    da.SelectCommand = cmd;
                    DataSet ds = new DataSet();
                    da.Fill(ds);
                    dr["sno"] = 1;
                    dr["pimage"] = ds.Tables[0].Rows[0]["pimage"].ToString();
                    dr["pname"] = ds.Tables[0].Rows[0]["pname"].ToString();
                    dr["price"] = ds.Tables[0].Rows[0]["price"].ToString();
                    dr["quantity"] = Request.QueryString["quantity"];

                    decimal price = Convert.ToDecimal(ds.Tables[0].Rows[0]["price"].ToString());
                    int quantity = Convert.ToInt32(Request.QueryString["quantity"].ToString());
                    decimal total = price * quantity;
                    dr["total"] = total;

                    dr["uname"] = ds.Tables[0].Rows[0]["uname"].ToString();
                    dt.Rows.Add(dr);
                    GridView1.DataSource = dt;
                    GridView1.DataBind();
                    Session["count"] = dt;

                    if (GridView1.Rows.Count > 0)
                    {
                        GridView1.FooterRow.Cells[4].Text = "Grand Total";
                        GridView1.FooterRow.Cells[5].Text = grandTotal().ToString();
                    }
                }
                else
                {
                    dt = (DataTable)Session["count"];
                    int sr;
                    sr = dt.Rows.Count;

                    dr = dt.NewRow();
                    String myquery = "SELECT * FROM violet_products where pname='" + Request.QueryString["id"] + "'";
                    SqlCommand cmd = new SqlCommand();
                    cmd.CommandText = myquery;
                    cmd.Connection = con;
                    SqlDataAdapter da = new SqlDataAdapter();
                    da.SelectCommand = cmd;
                    DataSet ds = new DataSet();
                    da.Fill(ds);
                    dr["sno"] = sr + 1;
                    dr["pname"] = ds.Tables[0].Rows[0]["pname"].ToString();
                    dr["pimage"] = ds.Tables[0].Rows[0]["pimage"].ToString();
                    dr["price"] = ds.Tables[0].Rows[0]["price"].ToString();
                    dr["quantity"] = Request.QueryString["quantity"];

                    decimal price = Convert.ToDecimal(ds.Tables[0].Rows[0]["price"].ToString());
                    int quantity = Convert.ToInt32(Request.QueryString["quantity"].ToString());
                    decimal total = price * quantity;
                    dr["total"] = total;

                    dr["uname"] = ds.Tables[0].Rows[0]["uname"].ToString();
                    dt.Rows.Add(dr);
                    GridView1.DataSource = dt;
                    GridView1.DataBind();
                    Session["count"] = dt;

                    if (GridView1.Rows.Count > 0)
                    {
                        GridView1.FooterRow.Cells[4].Text = "Grand Total";
                        GridView1.FooterRow.Cells[5].Text = grandTotal().ToString();
                    }
                }
            }
            else
            {
                dt = (DataTable)Session["count"];
                GridView1.DataSource = dt;
                GridView1.DataBind();
                if (GridView1.Rows.Count > 0)
                {
                    GridView1.FooterRow.Cells[4].Text = "Grand Total";
                    GridView1.FooterRow.Cells[5].Text = grandTotal().ToString();
                }
            }
        }

        /// <summary>
        /// 計算購物車（Session["count"]）所有列 total 欄位的加總金額。
        /// </summary>
        /// <returns>購物車總金額。</returns>
        public decimal grandTotal()
        {
            DataTable dt = new DataTable();
            dt = (DataTable)Session["count"];
            decimal grandTotal = 0;
            int i = 0;
            int n = dt.Rows.Count;
            while (i < n)
            {
                grandTotal = grandTotal + Convert.ToDecimal(dt.Rows[i]["total"].ToString());
                i = i + 1;
            }
            return grandTotal;
        }

        /// <summary>
        /// 產生訂單編號並顯示於 orderID 標籤。
        /// 格式："#" + 時 + 分 + 秒 + 日 + 月 + 年（皆未補零）+ 5 碼隨機英數字。
        /// 隨機字元取自 61 個字元的字元集（不含數字 0）。
        /// </summary>
        public void calculateOrderID()
        {
            String pass = "abcdefghijklmnopqrstuvwxyz123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            Random r = new Random();
            char[] mypass = new char[5];
            for (int i = 0; i < 5; i++)
            {
                mypass[i] = pass[(int)( 61 * r.NextDouble() )];
            }
            String orderid;
            orderid = "#" + DateTime.Now.Hour.ToString() + DateTime.Now.Minute.ToString() + DateTime.Now.Second.ToString() + DateTime.Now.Day.ToString() + DateTime.Now.Month.ToString() + DateTime.Now.Year.ToString() + new string(mypass);

            orderID.Text = orderid;

        }
    }
}