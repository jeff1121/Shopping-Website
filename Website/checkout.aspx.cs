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
    /// 「結帳」頁面（checkout.aspx）的後置程式碼。
    /// 顯示購物車明細、產生訂單編號與日期；按下「Place Order」後將每一項寫入 violet_order，
    /// 並清除該使用者在 violet_cart 的資料。無任何金流介接。
    /// </summary>
    public partial class checkout : System.Web.UI.Page
    {
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
                countItems.Text = CartSession.Count(Session).ToString();

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
        /// 下單按鈕：購物車為空或未登入時不處理；否則切換為「訂單完成」面板，
        /// 將購物車每一列以相同訂單編號、參數化且指定欄位的 INSERT 寫入 violet_order，
        /// 刪除該使用者的 violet_cart 資料，並清空 Session["count"]（購物車徽章歸零）。
        /// </summary>
        /// <param name="sender">觸發下單事件的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void btnCheckout_Click(object sender, EventArgs e)
        {
            DataTable dt = CartSession.Get(Session);
            string uname = Db.GetUname(Session["user"]);
            if (dt == null || dt.Rows.Count == 0 || uname.Length == 0)
            {
                Response.Redirect("~/cart.aspx");
                return;
            }

            btnCheckout.Visible = false;
            Panel1.Visible = false;
            completeOrder.Visible = true;
            orderIDCompleted.Text = orderID.Text;
            orderDateCompleted.Text = orderDate.Text;

            foreach (DataRow row in dt.Rows)
            {
                Db.Execute("INSERT INTO violet_order (uname, pname, orderID, orderDate, quantity, total) VALUES (@uname, @pname, @orderID, @orderDate, @quantity, @total)",
                    Db.Param("@uname", uname), Db.Param("@pname", row["pname"].ToString()), Db.Param("@orderID", orderID.Text),
                    Db.Param("@orderDate", orderDate.Text), Db.Param("@quantity", Convert.ToInt32(row["quantity"])), Db.Param("@total", Convert.ToDecimal(row["total"])));
            }

            // 清除該使用者已結帳的購物車資料
            Db.Execute("DELETE FROM violet_cart WHERE uname=@uname", Db.Param("@uname", uname));
            Session[CartSession.Key] = null;
            countItems.Text = "0";
        }

        /// <summary>
        /// 填入結帳明細 GridView：顯示 Session["count"] 的購物車內容，表尾顯示 Grand Total。
        /// </summary>
        public void filldata()
        {
            GridView1.DataSource = CartSession.Get(Session);
            GridView1.DataBind();
            if (GridView1.Rows.Count > 0)
            {
                GridView1.FooterRow.Cells[4].Text = "Grand Total";
                GridView1.FooterRow.Cells[5].Text = grandTotal().ToString();
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