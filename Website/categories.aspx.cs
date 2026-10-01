using System;
using Website.Data;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Website
{
    /// <summary>
    /// 「商品分類」頁面（categories.aspx）的後置程式碼。
    /// 分類清單由標記中的 SqlDataSource1 從 violet_categories（欄位 name、cimage）讀出，
    /// 點選分類後導向 index.aspx?category=分類名稱 以篩選商品。
    /// </summary>
    public partial class categories : System.Web.UI.Page
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
                countItems.Text = CartSession.Count(Session).ToString();
            }
        }

        /// <summary>
        /// 登出按鈕：清除 Session["user"]。注意：本頁登出後導向 login.aspx（其他頁面皆導向 index.aspx）。
        /// </summary>
        /// <param name="sender">觸發登出事件的按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void btnLogout_Click(object sender, EventArgs e)
        {
            Session["user"] = null;
            Response.Redirect("~/login.aspx");
        }

        /// <summary>
        /// 分類 DataList 的項目命令事件：點擊分類名稱（CommandName="category"）時，
        /// 取出該列 LinkButton1 的文字作為分類名稱，導向首頁並帶入 category 查詢字串。
        /// </summary>
        /// <param name="source">觸發命令事件的分類 DataList。</param>
        /// <param name="e">包含分類列與命令名稱的事件資料。</param>
        protected void DataList1_ItemCommand(object source, DataListCommandEventArgs e)
        {
            if (e.CommandName == "category")
            {
                LinkButton linkBtn = (LinkButton)( e.Item.FindControl("LinkButton1") );

                Response.Redirect("~/index.aspx?category=" + linkBtn.Text);
            }
        }
    }
}