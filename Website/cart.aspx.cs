using System;
using System.Collections.Generic;
using System.Data;
using Website.Data;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Website
{
    /// <summary>
    /// 「購物車」頁面（cart.aspx）的後置程式碼。
    /// 購物車同時存在兩處：Session["count"]（DataTable，欄位見 <see cref="CartSession"/>）
    /// 與資料表 violet_cart（以 uname + pname 識別）；本頁負責新增、修改數量、移除品項，並同步調整 violet_products 的庫存。
    /// </summary>
    public partial class cart : System.Web.UI.Page
    {
        /// <summary>目前登入者的姓名（violet_user_login.uname），為 violet_cart 的買家欄位。</summary>
        string uname = "";

        /// <summary>
        /// 頁面載入事件：
        /// 已登入時更新頁首圖示、查出登入者姓名 uname、更新購物車徽章，首次載入時呼叫 <see cref="filldata"/>；
        /// 未登入時顯示「請先登入」提示。
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

                uname = Db.GetUname(Session["user"]);

                if (!IsPostBack)
                {
                    filldata();
                }

                countItems.Text = CartSession.Count(Session).ToString();
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
        /// <param name="sender">觸發登出事件的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
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
        /// <param name="sender">觸發刪除事件的 GridView。</param>
        /// <param name="e">包含被刪除列索引的事件資料。</param>
        protected void GridView1_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            DataTable dt = CartSession.Get(Session);
            DataRow row = dt == null ? null : FindRowBySno(dt, GridView1.Rows[e.RowIndex].Cells[0].Text);
            if (row != null)
            {
                string productName = row["pname"].ToString();
                int quantity = Convert.ToInt32(row["quantity"]);

                // 移除品項時，將購物車內原數量加回商品庫存。
                Db.Execute("UPDATE violet_products SET stock = stock + @quantity WHERE pname=@pname",
                    Db.Param("@quantity", quantity), Db.Param("@pname", productName));
                Db.Execute("DELETE FROM violet_cart WHERE uname=@uname AND pname=@pname",
                    Db.Param("@uname", uname), Db.Param("@pname", productName));

                dt.Rows.Remove(row);

                // 重新編號剩餘品項的 sno，並同步到 violet_cart
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    dt.Rows[i]["sno"] = i + 1;
                    Db.Execute("UPDATE violet_cart SET sno=@sno WHERE uname=@uname AND pname=@pname",
                        Db.Param("@sno", i + 1), Db.Param("@uname", uname), Db.Param("@pname", dt.Rows[i]["pname"].ToString()));
                }

                Session[CartSession.Key] = dt;
            }

            Response.Redirect("~/cart.aspx");
        }

        /// <summary>
        /// 購物車 GridView 「Modify」選取事件：隱藏清單與結帳按鈕，顯示數量編輯面板，
        /// 並以被選列的 sno 呼叫 <see cref="modify"/> 載入該品項資料。
        /// </summary>
        /// <param name="sender">觸發選取事件的 GridView。</param>
        /// <param name="e">選取列變更事件資料。</param>
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
        /// <param name="sender">觸發數量變更的下拉選單。</param>
        /// <param name="e">選取項目變更事件資料。</param>
        protected void DropDownList1_SelectedIndexChanged(object sender, EventArgs e)
        {
            Label6.Text = (Convert.ToDecimal(Label5.Text) * Convert.ToInt32(DropDownList1.SelectedValue)).ToString();
        }

        /// <summary>
        /// 編輯面板「Update」按鈕：
        /// 1. 以購物車中原數量與新數量的差額調整 violet_products 庫存（stock + 舊數量 - 新數量，結果不可小於 0）；
        /// 2. 調整成功後更新 Session 購物車對應列與 violet_cart 的數量與小計（小計由伺服器重新計算）；
        /// 3. 重新導向本頁。
        /// </summary>
        /// <param name="sender">觸發更新事件的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void btnUpdate_Click(object sender, EventArgs e)
        {
            DataTable dt = CartSession.Get(Session);
            DataRow row = dt == null ? null : FindRowBySno(dt, Label3.Text);
            int newQuantity;
            if (row != null && int.TryParse(DropDownList1.SelectedValue, out newQuantity) && newQuantity > 0)
            {
                string productName = row["pname"].ToString();
                int oldQuantity = Convert.ToInt32(row["quantity"]);

                // 依「舊數量 - 新數量」調整預扣庫存，正值代表補回庫存、負值代表追加扣庫存。
                int updated = Db.Execute("UPDATE violet_products SET stock = stock + @old - @new WHERE pname=@pname AND stock + @old - @new >= 0",
                    Db.Param("@old", oldQuantity), Db.Param("@new", newQuantity), Db.Param("@pname", productName));
                if (updated == 1)
                {
                    decimal total = Convert.ToDecimal(row["price"]) * newQuantity;
                    row["quantity"] = newQuantity;
                    row["total"] = total;
                    Db.Execute("UPDATE violet_cart SET quantity=@quantity, total=@total WHERE uname=@uname AND pname=@pname",
                        Db.Param("@quantity", newQuantity), Db.Param("@total", total), Db.Param("@uname", uname), Db.Param("@pname", productName));
                    Session[CartSession.Key] = dt;
                }
            }

            Response.Redirect("~/cart.aspx");
        }

        /// <summary>
        /// 填入購物車 GridView。
        /// 若網址的 ?quantity=數量&amp;id=商品 與 Session["addproduct"] 記錄的「數量:商品名稱」（首頁預扣庫存時寫入）完全相同：
        /// - 商品已在購物車：累加數量並同步 violet_cart；
        /// - 新商品：查出商品資料，以目前列數 + 1 作為 sno 追加，並寫入 violet_cart。
        /// 處理後清除 Session["addproduct"]，避免重新整理時重複加入；參數不符時不加入任何品項。
        /// 最後顯示購物車內容與 Grand Total；無資料時顯示「購物車是空的」。
        /// </summary>
        public void filldata()
        {
            DataTable dt = CartSession.Get(Session) ?? CartSession.CreateTable();
            string productName = Request.QueryString["id"];
            int quantity;

            string reserved = Session["addproduct"] as string;
            if (reserved != null && productName != null
                && int.TryParse(Request.QueryString["quantity"], out quantity) && quantity > 0
                && reserved == quantity + ":" + productName)
            {
                Session["addproduct"] = null;
                DataRow existing = FindRowByName(dt, productName);
                if (existing != null)
                {
                    updatequantity(existing, quantity);
                }
                else
                {
                    DataTable product = Db.Query("SELECT pimage, pname, price, uname FROM violet_products WHERE pname=@pname", Db.Param("@pname", productName));
                    if (product.Rows.Count == 1)
                    {
                        DataRow source = product.Rows[0];
                        decimal price = Convert.ToDecimal(source["price"]);
                        decimal total = price * quantity;
                        int sno = dt.Rows.Count + 1;

                        DataRow dr = dt.NewRow();
                        dr["sno"] = sno;
                        dr["pimage"] = source["pimage"].ToString();
                        dr["pname"] = source["pname"].ToString();
                        dr["price"] = source["price"].ToString();
                        dr["quantity"] = quantity;
                        dr["total"] = total;
                        dr["uname"] = source["uname"].ToString();
                        dt.Rows.Add(dr);

                        savecartdetail(uname, sno, source["pimage"].ToString(), source["pname"].ToString(), price, quantity, total, source["uname"].ToString());
                    }
                }

                Session[CartSession.Key] = dt;
            }

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
            {
                lblEmpty.Visible = true;
            }
        }

        /// <summary>
        /// 計算購物車（Session["count"]）所有列 total 欄位的加總金額。
        /// </summary>
        /// <returns>購物車總金額。</returns>
        public decimal grandTotal()
        {
            return CartSession.GrandTotal(CartSession.Get(Session));
        }

        /// <summary>
        /// 將指定 sno 的購物車品項載入數量編輯面板：
        /// 數量選項為 1..（購物車內原數量 + 目前剩餘庫存），確保原數量一定在選項中；
        /// Label3～Label6 分別顯示序號、商品名稱、單價與小計。
        /// </summary>
        /// <param name="modifyQuantity">要編輯的品項序號（sno），取自 GridView 被選列的第一欄。</param>
        public void modify(string modifyQuantity)
        {
            DataTable dt = CartSession.Get(Session);
            DataRow row = dt == null ? null : FindRowBySno(dt, modifyQuantity);
            if (row == null)
            {
                return;
            }

            object stockValue = Db.Scalar("SELECT stock FROM violet_products WHERE pname=@pname", Db.Param("@pname", row["pname"].ToString()));
            int stock = stockValue == null ? 0 : Convert.ToInt32(stockValue);
            int current = Convert.ToInt32(row["quantity"]);

            DropDownList1.Items.Clear();
            for (int j = 1; j <= current + stock; j++)
            {
                DropDownList1.Items.Add(j.ToString());
            }

            DropDownList1.Enabled = current + stock > 1;
            DropDownList1.SelectedValue = current.ToString();
            Label3.Text = row["sno"].ToString();
            Label4.Text = row["pname"].ToString();
            Label5.Text = row["price"].ToString();
            Label6.Text = row["total"].ToString();
        }

        /// <summary>
        /// 在購物車表中依序號（sno）尋找品項。
        /// </summary>
        /// <param name="dt">購物車表。</param>
        /// <param name="sno">序號文字。</param>
        /// <returns>找到的列；找不到時為 null。</returns>
        private static DataRow FindRowBySno(DataTable dt, string sno)
        {
            foreach (DataRow row in dt.Rows)
            {
                if (row["sno"].ToString() == sno)
                {
                    return row;
                }
            }

            return null;
        }

        /// <summary>
        /// 在購物車表中依商品名稱（pname）尋找品項，用來判斷要加入的商品是否已在購物車中。
        /// </summary>
        /// <param name="dt">購物車表。</param>
        /// <param name="productName">商品名稱。</param>
        /// <returns>找到的列；找不到時為 null。</returns>
        private static DataRow FindRowByName(DataTable dt, string productName)
        {
            foreach (DataRow row in dt.Rows)
            {
                if (row["pname"].ToString() == productName)
                {
                    return row;
                }
            }

            return null;
        }

        /// <summary>
        /// 將新加入的數量累加到購物車中同名商品，重算小計，並同步更新 violet_cart。
        /// </summary>
        /// <param name="row">購物車中已存在的品項列。</param>
        /// <param name="addQuantity">本次加入的數量。</param>
        private void updatequantity(DataRow row, int addQuantity)
        {
            int newQuantity = Convert.ToInt32(row["quantity"]) + addQuantity;
            decimal total = Convert.ToDecimal(row["price"]) * newQuantity;
            row["quantity"] = newQuantity;
            row["total"] = total;
            Db.Execute("UPDATE violet_cart SET quantity=@quantity, total=@total WHERE uname=@uname AND pname=@pname",
                Db.Param("@quantity", newQuantity), Db.Param("@total", total), Db.Param("@uname", uname), Db.Param("@pname", row["pname"].ToString()));
        }

        /// <summary>
        /// 將一筆購物車品項以參數化、指定欄位的 INSERT 寫入 violet_cart。
        /// </summary>
        /// <param name="name">買家姓名（violet_user_login.uname）。</param>
        /// <param name="sno">購物車內序號。</param>
        /// <param name="productimage">商品圖片網址。</param>
        /// <param name="Productname">商品名稱。</param>
        /// <param name="price">單價。</param>
        /// <param name="quantity">數量。</param>
        /// <param name="totalprice">小計（單價 × 數量）。</param>
        /// <param name="sname">賣家姓名（violet_products.uname）。</param>
        private static void savecartdetail(string name, int sno, string productimage, string Productname, decimal price, int quantity, decimal totalprice, string sname)
        {
            Db.Execute("INSERT INTO violet_cart (uname, sno, pimage, pname, price, quantity, total, sname) VALUES (@uname, @sno, @pimage, @pname, @price, @quantity, @total, @sname)",
                Db.Param("@uname", name), Db.Param("@sno", sno), Db.Param("@pimage", productimage), Db.Param("@pname", Productname),
                Db.Param("@price", price), Db.Param("@quantity", quantity), Db.Param("@total", totalprice), Db.Param("@sname", sname));
        }
    }
}
