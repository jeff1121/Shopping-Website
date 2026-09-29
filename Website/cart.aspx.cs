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
    /// 「購物車」頁面（cart.aspx）的後置程式碼。
    /// 購物車同時存在兩處：Session["count"]（DataTable，欄位 sno, pimage, pname, price, quantity, total, uname）
    /// 與資料表 violet_cart；本頁負責新增、修改數量、移除品項，並同步調整 violet_products 的庫存。
    /// </summary>
    public partial class cart : System.Web.UI.Page
    {
        /// <summary>資料庫連線（連線字串需在本機自行填入）。</summary>
        SqlConnection con = new SqlConnection(<enter your database connection>);
        /// <summary>
        /// 標記要加入的商品是否已存在於購物車（由 <see cref="checkdesignid"/> 設定）。
        /// 注意：為 static，會在所有使用者請求間共用。
        /// </summary>
        static Boolean availabledesignid = false;
        /// <summary>目前登入者的姓名（violet_user_login.uname），為 violet_cart 的買家欄位。</summary>
        String uname = "";

        /// <summary>
        /// 頁面載入事件：
        /// 已登入時更新頁首圖示、查出登入者姓名 uname、更新購物車徽章，首次載入時呼叫 <see cref="filldata"/>；
        /// 未登入時顯示「請先登入」提示。
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

                // 以登入時輸入的帳號或 Email 查出姓名
                SqlCommand cmd = new SqlCommand("SELECT uname FROM violet_user_login WHERE username=@name OR email=@name", con);
                cmd.Parameters.AddWithValue("@name", Session["user"]);
                con.Open();
                SqlDataReader read = cmd.ExecuteReader();
                while (read.Read())
                {
                    uname = read["uname"].ToString();
                }
                con.Close();

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
                }
            }
            else
            {
                countItems.Text = "";
                lblEmpty.Text = "Login First to add itmes to your cart";
                lblEmpty.Visible = true;
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
        /// 購物車 GridView 「Remove Item(s)」刪除事件：
        /// 1. 依被點選列第一欄的 sno 找到 Session 購物車中的對應列；
        /// 2. 將該列數量加回 violet_products 庫存，從 DataTable 與 violet_cart 刪除；
        /// 3. 將剩餘列的 sno 重新編號為 1..N 並同步回 violet_cart；
        /// 4. 寫回 Session 後重新導向本頁。
        /// </summary>
        protected void GridView1_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            string productName = "";
            DataTable dt = new DataTable();
            dt = (DataTable)Session["count"];

            for (int i = 0; i <= dt.Rows.Count - 1; i++)
            {
                int sr;
                int sr1;
                string qdata;
                string dtdata;
                productName = dt.Rows[i]["pname"].ToString();
                sr = Convert.ToInt32(dt.Rows[i]["sno"].ToString());
                TableCell cell = GridView1.Rows[e.RowIndex].Cells[0];
                qdata = cell.Text;
                dtdata = sr.ToString();
                sr1 = Convert.ToInt32(qdata);

                if (sr == sr1)
                {
                    //Updating stock after deletion
                    int j = Convert.ToInt32(dt.Rows[i]["quantity"].ToString());
                    String updateQuantity = "UPDATE violet_products SET stock=stock+" + j +" WHERE pname='" + productName + "'";
                    SqlCommand cmd1 = new SqlCommand(updateQuantity, con);
                    //Executing Query
                    con.Open();
                    cmd1.ExecuteNonQuery();
                    con.Close();

                    dt.Rows[i].Delete();
                    dt.AcceptChanges();

                    String update = "DELETE FROM violet_cart WHERE uname='" + uname + "' AND pname='" + productName + "' AND sno=" + sr;
                    SqlCommand cmd = new SqlCommand(update, con);
                    //Executing Query
                    con.Open();
                    cmd.ExecuteNonQuery();
                    con.Close();

                    break;
                }
            }

            // 重新編號剩餘品項的 sno，並同步到 violet_cart
            for (int i = 1; i <= dt.Rows.Count; i++)
            {
                productName = dt.Rows[i-1]["pname"].ToString();
                dt.Rows[i - 1]["sno"] = i;
                dt.AcceptChanges();

                String update = "UPDATE violet_cart SET sno=" + Convert.ToInt32(dt.Rows[i - 1]["sno"].ToString()) + " WHERE uname='" + uname + "' AND pname='" + productName + "'";
                SqlCommand cmd = new SqlCommand(update, con);
                //Executing Query
                con.Open();
                cmd.ExecuteNonQuery();
                con.Close();

            }

            Session["count"] = dt;
            Response.Redirect("~/cart.aspx");
        }

        /// <summary>
        /// 購物車 GridView 「Modify」選取事件：隱藏清單與結帳按鈕，顯示數量編輯面板，
        /// 並以被選列的 sno 呼叫 <see cref="modify"/> 載入該品項資料。
        /// </summary>
        protected void GridView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnCheckout.Visible = false;
            GridView1.Visible = false;
            editQuantity.Visible = true;
            lblEmpty.Visible = false;
            modify(GridView1.SelectedRow.Cells[0].Text);
        }

        /// <summary>
        /// 編輯面板中數量下拉選單變更事件：以單價（Label5）× 數量重新計算小計並顯示於 Label6。
        /// </summary>
        protected void DropDownList1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int q;
            q = Convert.ToInt32(DropDownList1.Text);
            decimal cost;
            cost = Convert.ToDecimal(Label5.Text);
            decimal totalcost;
            totalcost = cost * q;
            Label6.Text = totalcost.ToString();
        }

        /// <summary>
        /// 編輯面板「Update」按鈕：
        /// 1. 以 Session["oldQuantity"] 與新數量的差額調整 violet_products 庫存（stock + 舊數量 - 新數量）；
        /// 2. 更新 Session 購物車對應列與 violet_cart 的數量與小計；
        /// 3. 重新導向本頁。
        /// </summary>
        protected void btnUpdate_Click(object sender, EventArgs e)
        {
            DataTable dt;

            dt = (DataTable)Session["count"];

            for (int i = 0; i <= dt.Rows.Count - 1; i++)
            {
                int sr;
                int sr1;
                sr = Convert.ToInt32(dt.Rows[i]["sno"].ToString());

                sr1 = Convert.ToInt32(Label3.Text);

                if (sr == sr1)
                {
                    //Updating stock after updating cart
                    int j = Convert.ToInt32(DropDownList1.Text);
                    String updateQuantity = "UPDATE violet_products SET stock=(stock+" + Convert.ToInt32(Session["oldQuantity"].ToString()) + "-" + j + ") WHERE pname='" + Label4.Text + "'";
                    SqlCommand cmd1 = new SqlCommand(updateQuantity, con);
                    //Executing Query
                    con.Open();
                    cmd1.ExecuteNonQuery();
                    con.Close();

                    dt.Rows[i]["sno"] = Label3.Text;
                    dt.Rows[i]["pname"] = Label4.Text;
                    dt.Rows[i]["quantity"] = DropDownList1.Text;
                    dt.Rows[i]["price"] = Label5.Text;
                    dt.Rows[i]["total"] = Label6.Text;
                    dt.AcceptChanges();

                    String update = "UPDATE violet_cart SET quantity=" + Convert.ToInt32(dt.Rows[i]["quantity"].ToString()) + ", total=" + Convert.ToDecimal(dt.Rows[i]["total"].ToString()) + " WHERE uname='" + uname + "' AND pname='" + Label4.Text + "'";
                    SqlCommand cmd = new SqlCommand(update, con);
                    //Executing Query
                    con.Open();
                    cmd.ExecuteNonQuery();
                    con.Close();

                    break;
                }
            }
            Response.Redirect("~/cart.aspx");

        }

        /// <summary>
        /// 填入購物車 GridView。
        /// 若 Session["addproduct"] 為 "true"（由首頁加入購物車導向而來）且帶有 ?id=&amp;quantity=：
        /// - 購物車為空：建立新 DataTable，加入第一項並寫入 violet_cart；
        /// - 商品已存在：呼叫 <see cref="updatequantity"/> 累加數量（注意：此分支不會同步 violet_cart）；
        /// - 商品不存在：追加新列並寫入 violet_cart。
        /// 否則直接顯示 Session["count"] 的內容，無資料時顯示「購物車是空的」。表尾顯示 Grand Total。
        /// 處理後會將 Session["addproduct"] 重設為 "false"，避免重新整理時重複加入。
        /// </summary>
        public void filldata()
        {
            // 注意：直接進入本頁且未曾經過首頁時 Session["addproduct"] 可能為 null，會拋出 NullReferenceException
            if(Session["addproduct"].ToString() == "true")
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
                Session["addproduct"] = "false";

                if (Request.QueryString["id"] != null)
                {
                    btnCheckout.Visible = true;
                    // 情況一：購物車為空，建立第一筆
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

                        savecartdetail(uname, 1, ds.Tables[0].Rows[0]["pimage"].ToString(), ds.Tables[0].Rows[0]["pname"].ToString(), Convert.ToDecimal(ds.Tables[0].Rows[0]["price"].ToString()), Convert.ToInt32(Request.QueryString["quantity"].ToString()), total, ds.Tables[0].Rows[0]["uname"].ToString());

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
                        checkdesignid();
                        // 情況二：商品已在購物車中，只累加數量
                        if (availabledesignid == true)
                        {
                            updatequantity();
                            DataTable dt1;
                            dt1 = (DataTable)Session["count"];
                            GridView1.DataSource = dt1;
                            GridView1.DataBind();
                            availabledesignid = false;
                        }
                        else
                        {
                            // 情況三：新商品，以目前列數 + 1 作為 sno 追加
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

                            savecartdetail(uname, sr+1, ds.Tables[0].Rows[0]["pimage"].ToString(), ds.Tables[0].Rows[0]["pname"].ToString(), Convert.ToDecimal(ds.Tables[0].Rows[0]["price"].ToString()), Convert.ToInt32(Request.QueryString["quantity"].ToString()), total, ds.Tables[0].Rows[0]["uname"].ToString());

                            dt.Rows.Add(dr);
                            GridView1.DataSource = dt;
                            GridView1.DataBind();
                            Session["count"] = dt;
                        }

                        if (GridView1.Rows.Count > 0)
                        {
                            GridView1.FooterRow.Cells[4].Text = "Grand Total";
                            GridView1.FooterRow.Cells[5].Text = grandTotal().ToString();
                        }
                    }
                }
            }
            else
            {
                DataTable dt;
                dt = (DataTable)Session["count"];
                GridView1.DataSource = dt;
                GridView1.DataBind();

                if (GridView1.Rows.Count > 0)
                {
                    GridView1.FooterRow.Cells[4].Text = "Grand Total";
                    GridView1.FooterRow.Cells[5].Text = grandTotal().ToString();
                    lblEmpty.Visible = false;
                    btnCheckout.Visible = true;
                }
                else
                    lblEmpty.Visible = true;
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
        /// 將指定 sno 的購物車品項載入數量編輯面板（僅在 PostBack 時執行）：
        /// 依目前庫存填入數量選項 1..庫存（庫存為 0 則停用），並將原數量記入 Session["oldQuantity"] 供更新時調整庫存。
        /// </summary>
        /// <param name="modifyQuantity">要編輯的品項序號（sno），取自 GridView 被選列的第一欄。</param>
        public void modify(string modifyQuantity)
        {
            DataTable dt;

            if (!IsPostBack)
            {

            }
            else
            {
                if (modifyQuantity != null)
                {
                    dt = (DataTable)Session["count"];

                    for (int i = 0; i <= dt.Rows.Count - 1; i++)
                    {
                        int sr;
                        int sr1;
                        sr = Convert.ToInt32(dt.Rows[i]["sno"].ToString());
                        Label3.Text = modifyQuantity;
                        Label4.Text = sr.ToString();
                        sr1 = Convert.ToInt32(Label3.Text);

                        if (sr == sr1)
                        {
                            //Inserting contents of DropDownList
                            SqlCommand cmd = new SqlCommand("SELECT stock FROM violet_products WHERE pname=@pname", con);
                            cmd.Parameters.AddWithValue("@pname", dt.Rows[i]["pname"].ToString());
                            con.Open();
                            SqlDataReader read = cmd.ExecuteReader();
                            int q = 0;
                            while (read.Read())
                            {
                                q = Convert.ToInt32(read["stock"].ToString());
                            }
                            con.Close();
                            int j;
                            if (q > 0)
                            {
                                for (j = 1; j <= q; j++)
                                {
                                    String n = j.ToString();
                                    DropDownList1.Items.Add(n);
                                }
                            }
                            else
                            {
                                DropDownList1.Items.Add("1");
                                DropDownList1.Enabled = false;
                            }

                            Label3.Text = dt.Rows[i]["sno"].ToString();
                            Label4.Text = dt.Rows[i]["pname"].ToString();
                            DropDownList1.Text = dt.Rows[i]["quantity"].ToString();
                            Session["oldQuantity"] = DropDownList1.Text;
                            Label5.Text = dt.Rows[i]["price"].ToString();
                            Label6.Text = dt.Rows[i]["total"].ToString();
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 檢查網址 ?id= 指定的商品名稱是否已存在於 Session 購物車，存在則將 availabledesignid 設為 true。
        /// </summary>
        private void checkdesignid()
        {
            DataTable dt;
            string designid;
            string querydesignid = Request.QueryString["id"].ToString();
            dt = (DataTable)Session["count"];
            foreach (DataRow row in dt.Rows)
            {
                designid = row["pname"].ToString();
                if (designid == querydesignid)
                {
                    availabledesignid = true;
                }
            }
        }

        /// <summary>
        /// 將網址 ?quantity= 的數量累加到 Session 購物車中同名商品的數量，並重算小計。
        /// 注意：只更新 Session，不會同步更新 violet_cart。
        /// </summary>
        private void updatequantity()
        {
            DataTable dt;
            string designid;
            String querydesignid = Request.QueryString["id"];
            dt = (DataTable)Session["count"];
            foreach (DataRow row in dt.Rows)
            {
                designid = row["pname"].ToString();
                if (designid == querydesignid)
                {
                    int newquantity = Convert.ToInt16(row["quantity"].ToString()) + Convert.ToInt16(Request.QueryString["quantity"].ToString());
                    row["quantity"] = newquantity;
                    Decimal price = Convert.ToDecimal(row["price"].ToString());
                    Decimal totalprice = price * newquantity;
                    row["total"] = totalprice;
                    break;
                }
            }
            Session["count"] = dt;
        }

        /// <summary>
        /// 將一筆購物車品項以位置式 INSERT 寫入 violet_cart
        /// （欄位順序 uname, sno, pimage, pname, price, quantity, total, sname）。
        /// 注意：SQL 以字串串接，存在 SQL Injection 風險。
        /// </summary>
        /// <param name="name">買家姓名（violet_user_login.uname）。</param>
        /// <param name="sno">購物車內序號。</param>
        /// <param name="productimage">商品圖片相對路徑。</param>
        /// <param name="Productname">商品名稱。</param>
        /// <param name="price">單價。</param>
        /// <param name="quantity">數量。</param>
        /// <param name="totalprice">小計（單價 × 數量）。</param>
        /// <param name="sname">賣家姓名（violet_products.uname）。</param>
        private void savecartdetail(String name, int sno, String productimage, String Productname, Decimal price, int quantity, Decimal totalprice, String sname)
        {
            String query = "INSERT INTO violet_cart values('" + name + "', " + sno + ", '" + productimage + "', '" + Productname + "', " + price + ", " + quantity + ", " + totalprice + ", '" + sname +"')";
            
            con.Open();
            SqlCommand cmd = new SqlCommand();
            cmd.CommandText = query;
            cmd.Connection = con;
            cmd.ExecuteNonQuery();
            con.Close();
        }
    }
}