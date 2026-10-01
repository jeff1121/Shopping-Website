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
                countItems.Text = CartSession.Count(Session).ToString();
            }
        }

        /// <summary>
        /// 商品 DataList 的項目命令事件。點擊「加入購物車」（CommandName="addtocart"）時：
        /// 1. 以單一 UPDATE 在庫存足夠時扣除所選數量（庫存在加入購物車時即預扣），避免同時下單造成負庫存；
        /// 2. 扣除成功才設定 Session["addproduct"]="true"，讓 cart.aspx 知道要新增品項；
        /// 3. 導向 cart.aspx?id=商品名稱&amp;quantity=數量（皆經 URL 編碼）。庫存不足時重新整理首頁。
        /// </summary>
        /// <param name="source">觸發命令事件的 DataList。</param>
        /// <param name="e">包含 CommandName、CommandArgument 與項目控制項的事件資料。</param>
        protected void productsDisplay_ItemCommand(object source, DataListCommandEventArgs e)
        {
            if (e.CommandName == "addtocart")
            {
                // 未登入時不預扣庫存，先導向登入頁（購物車需登入才會保存）
                if (Session["user"] == null)
                {
                    Response.Redirect("~/login.aspx");
                    return;
                }

                // 取得該列的數量下拉選單
                DropDownList number = (DropDownList)e.Item.FindControl("DropDownList1");
                string pname = e.CommandArgument.ToString();
                int quantity;
                if (!int.TryParse(number.SelectedValue, out quantity) || quantity < 1)
                {
                    return;
                }

                int updated = Db.Execute("UPDATE violet_products SET stock = stock - @quantity WHERE pname=@pname AND stock >= @quantity",
                    Db.Param("@quantity", quantity), Db.Param("@pname", pname));
                if (updated == 0)
                {
                    Response.Redirect("~/index.aspx");
                    return;
                }

                Session["addproduct"] = "true";
                Response.Redirect("~/cart.aspx?id=" + HttpUtility.UrlEncode(pname) + "&quantity=" + quantity);
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
        /// 搜尋按鈕：搜尋框有輸入時，改用 SqlDataSource2（以 searchProducts 文字對 keywords 欄位做 LIKE 模糊比對）重新繫結商品清單；
        /// 未輸入時維持目前清單。
        /// </summary>
        /// <param name="sender">觸發搜尋事件的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void btnSearch_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(searchProducts.Text))
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
        /// 商品 DataList 每一項繫結完成後觸發：依該商品庫存（取自繫結資料的 stock 欄位，不另查資料庫）填入數量下拉選單（最多 1～10）。
        /// 庫存為 0 時停用選單與加入購物車按鈕，並將按鈕圖片換成 img/sold.png（售完）。
        /// </summary>
        /// <param name="sender">正在繫結項目的 DataList。</param>
        /// <param name="e">包含目前項目與控制項的繫結事件資料。</param>
        protected void productsDisplay_ItemDataBound(object sender, DataListItemEventArgs e)
        {
            if (e.Item.ItemType != ListItemType.Item && e.Item.ItemType != ListItemType.AlternatingItem)
            {
                return;
            }

            DropDownList number = (DropDownList)e.Item.FindControl("DropDownList1");
            ImageButton imgBtn = (ImageButton)e.Item.FindControl("ImageButton1");
            int stock = Convert.ToInt32(DataBinder.Eval(e.Item.DataItem, "stock"));

            if (stock > 0)
            {
                // 選項為 1..庫存量，最多列到 10
                for (int i = 1; i <= Math.Min(stock, 10); i++)
                {
                    number.Items.Add(i.ToString());
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