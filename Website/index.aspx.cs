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
    /// 首頁 / 商品目錄頁面（index.aspx）的後置程式碼。
    /// 商品清單 productsDisplay（DataList）會依操作切換不同的 SqlDataSource：
    /// SqlDataSource1 隨機排序、SqlDataSource2 關鍵字搜尋、SqlDataSource3 價格由低到高、
    /// SqlDataSource4 價格由高到低、SqlDataSource5 依 ?category= 篩選分類。
    /// 加入購物車時會先扣減庫存，再導向 cart.aspx。
    /// </summary>
    public partial class index : System.Web.UI.Page
    {
        /// <summary>資料庫連線（透過 Db 從 Web.config 的 cmpConnectionString 讀取）。</summary>
        SqlConnection con = Db.CreateConnection();

        /// <summary>
        /// 頁面載入事件：
        /// 1. 若網址帶有 ?category=，改用 SqlDataSource5 顯示該分類商品；
        /// 2. 若已登入，將 Session["addproduct"] 重設為 "false"，並更新頁首圖示與購物車數量徽章。
        /// </summary>
        /// <param name="sender">觸發頁面載入事件的物件。</param>
        /// <param name="e">頁面載入事件資料。</param>
        protected void Page_Load(object sender, EventArgs e)
        {
            // 由分類頁點選進入時，改用分類篩選的資料來源
            if (Request.QueryString["category"] != null)
            {
                productsDisplay.DataSourceID = null;
                productsDisplay.DataSource = SqlDataSource5;
                productsDisplay.DataBind();
            }
            if (Session["user"] != null)
            {
                Session["addproduct"] = "false";
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
        /// 商品 DataList 的項目命令事件。點擊「加入購物車」（CommandName="addtocart"）時：
        /// 1. 設定 Session["addproduct"]="true"，讓 cart.aspx 知道要新增品項；
        /// 2. 查詢目前庫存並扣除所選數量（庫存在加入購物車時即預扣）；
        /// 3. 導向 cart.aspx?id=商品名稱&amp;quantity=數量。
        /// </summary>
        /// <param name="source">觸發命令事件的 DataList。</param>
        /// <param name="e">包含 CommandName、CommandArgument 與項目控制項的事件資料。</param>
        protected void productsDisplay_ItemCommand(object source, DataListCommandEventArgs e)
        {
            if (e.CommandName == "addtocart")
            {
                Session["addproduct"] = "true";
                // 取得該列的數量下拉選單與商品名稱標籤
                DropDownList number = (DropDownList)( e.Item.FindControl("DropDownList1") );
                Label lbl = (Label)( e.Item.FindControl("Label1") );

                SqlCommand cmd = new SqlCommand("SELECT stock FROM violet_products WHERE pname=@pname", con);
                cmd.Parameters.AddWithValue("@pname", lbl.Text);
                con.Open();
                SqlDataReader read = cmd.ExecuteReader();
                int q = 0;
                while (read.Read())
                {
                    q = Convert.ToInt32(read["stock"].ToString());
                }
                con.Close();
                int updateStock = q - Convert.ToInt32(number.SelectedItem.ToString());
                // 注意：字串串接 SQL，存在 SQL Injection 風險
                String update = "UPDATE violet_products SET stock=" + updateStock + " WHERE pname='" + lbl.Text + "'";
                SqlCommand cmd1 = new SqlCommand(update, con);
                // 執行庫存扣減。
                con.Open();
                cmd1.ExecuteNonQuery();
                con.Close();

                Response.Redirect("~/cart.aspx?id=" + e.CommandArgument.ToString() + "&quantity=" + number.SelectedItem.ToString());
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
        /// 搜尋按鈕：改用 SqlDataSource2（以 searchProducts 文字對 keywords 欄位做 LIKE 模糊比對）重新繫結商品清單。
        /// </summary>
        /// <param name="sender">觸發搜尋事件的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void btnSearch_Click(object sender, EventArgs e)
        {
            // 注意：此條件對 TextBox 物件呼叫 ToString()，永遠不會是 null
            if (searchProducts.ToString() != null)
            {
                productsDisplay.DataSourceID = null;
                productsDisplay.DataSource = SqlDataSource2;
                productsDisplay.DataBind();
            }
        }

        /// <summary>
        /// 價格排序下拉選單變更事件：
        /// "Low to High" → SqlDataSource3、"High to Low" → SqlDataSource4、其餘（Random）→ SqlDataSource1。
        /// </summary>
        /// <param name="sender">觸發排序變更的下拉選單。</param>
        /// <param name="e">選取項目變更事件資料。</param>
        protected void sortPrice_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            productsDisplay.DataSourceID = null;
            if (sortPrice.SelectedItem.Text == "Low to High")
            {
                productsDisplay.DataSource = SqlDataSource3;
                productsDisplay.DataBind();
            }
            else if(sortPrice.SelectedItem.Text == "High to Low")
            {
                productsDisplay.DataSource = SqlDataSource4;
                productsDisplay.DataBind();
            }
            else
            {
                productsDisplay.DataSource = SqlDataSource1;
                productsDisplay.DataBind();
            }
        }

        /// <summary>
        /// 商品 DataList 每一項繫結完成後觸發：依該商品庫存填入數量下拉選單（最多 1～10）。
        /// 庫存為 0 時停用選單與加入購物車按鈕，並將按鈕圖片換成 img/sold.png（售完）。
        /// 注意：每個品項都會額外查詢一次資料庫（N+1 查詢）。
        /// </summary>
        /// <param name="sender">正在繫結項目的 DataList。</param>
        /// <param name="e">包含目前項目與控制項的繫結事件資料。</param>
        protected void productsDisplay_ItemDataBound(object sender, DataListItemEventArgs e)
        {
            DropDownList number = (DropDownList)( e.Item.FindControl("DropDownList1") );
            Label lbl = (Label)( e.Item.FindControl("Label1") );
            ImageButton imgBtn = (ImageButton)( e.Item.FindControl("ImageButton1") );

            SqlCommand cmd = new SqlCommand("SELECT stock FROM violet_products WHERE pname=@pname", con);
            cmd.Parameters.AddWithValue("@pname", lbl.Text);
            con.Open();
            SqlDataReader read = cmd.ExecuteReader();
            int q = 0;
            while (read.Read())
            {
                q = Convert.ToInt32(read["stock"].ToString());
            }
            con.Close();

            int i;
            if (q > 0)
            {
                // 選項為 1..庫存量，最多列到 10
                for (i = 1; i <= q; i++)
                {
                    String n = i.ToString();
                    number.Items.Add(n);
                    if (i == 10)
                        break;
                }
            }
            else
            {
                number.Enabled = false;
                number.Items.Add("1");
                imgBtn.Enabled = false;
                imgBtn.ImageUrl = "img/sold.png";
            }
        }

        /// <summary>
        /// 頁首搜尋圖示點擊事件：將輸入焦點移到左側的商品搜尋框。
        /// </summary>
        /// <param name="sender">頁首搜尋圖片按鈕。</param>
        /// <param name="e">圖片按鈕點擊事件資料。</param>
        protected void searchIcon_Click(object sender, ImageClickEventArgs e)
        {
            searchProducts.Focus();
        }
    }
}