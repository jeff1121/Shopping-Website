using iTextSharp.text;
using iTextSharp.text.html.simpleparser;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Data;
using Website.Data;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Website
{
    /// <summary>
    /// 「會員個人資料」頁面（profile.aspx）的後置程式碼。
    /// 功能：顯示與編輯個人資料（電話、地址、國家/州/城市）、顯示訂單歷史並以 iTextSharp 匯出 PDF；
    /// uid &gt; 5000 的賣家額外顯示「上架商品」按鈕與自己的商品清單（可編輯/刪除）。
    /// </summary>
    /// <remarks>
    /// 以 Session["user"] 查詢 violet_user_login；訂單清單由 SqlDataSource1 使用 Session["uname"] 查詢
    /// violet_order，賣家商品清單由 SqlDataSource2 依 txtName.Text 查詢 violet_products。未登入時導向登入頁。
    /// </remarks>
    public partial class profile : System.Web.UI.Page
    {
        /// <summary>
        /// 目前登入帳號的 uid（&gt; 5000 視為賣家）。存於 ViewState，PostBack 後仍保留。
        /// </summary>
        int uid
        {
            get { return ViewState["uid"] == null ? 0 : (int)ViewState["uid"]; }
            set { ViewState["uid"] = value; }
        }

        /// <summary>
        /// 頁面載入事件：
        /// 1. 未登入時導向登入頁；已登入時更新頁首圖示與購物車徽章；
        /// 2. 首次載入時讀取帳號資料填入表單並取得 uid；
        /// 3. 依該會員姓名計算訂單數，無訂單時隱藏訂單面板；
        /// 4. uid &gt; 5000 時顯示賣家專屬區塊；
        /// 5. 預設停用所有輸入欄位。
        /// </summary>
        /// <param name="sender">ASP.NET Web Forms 傳入的事件來源。</param>
        /// <param name="e">頁面載入事件資料。</param>
        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["user"] == null)
            {
                Response.Redirect("~/login.aspx");
                return;
            }

            btnLogout.Visible = true;
            Menu1.Visible = false;
            profileIcon.Visible = true;
            cartIcon.Visible = true;
            countItems.Visible = true;
            countItems.Text = CartSession.Count(Session).ToString();

            if (!Page.IsPostBack)
            {
                validateAge.ValueToCompare = DateTime.Today.ToShortDateString();
                DataTable account = Db.Query("SELECT * FROM violet_user_login WHERE username=@name OR email=@name", Db.Param("@name", Session["user"].ToString()));
                if (account.Rows.Count == 1)
                {
                    DataRow read = account.Rows[0];
                    txtName.Text = read["uname"].ToString();
                    txtEmail.Text = read["email"].ToString();
                    txtUsername.Text = Session["user"].ToString();
                    txtPhone.Text = read["phone"].ToString();
                    // 以伺服器區域設定的短日期格式顯示，與 validateAge 的比較值格式一致
                    txtDOB.Text = Convert.ToDateTime(read["dob"]).ToShortDateString();
                    selectCountry.Text = read["country"].ToString();
                    selectState.Text = read["state"].ToString();
                    selectCity.Text = read["city"].ToString();
                    lblGender.Text = read["gender"].ToString();
                    txtAddress.InnerText = read["address"].ToString();
                    uid = Convert.ToInt32(read["uid"]);
                }
            }

            // 計算該會員的訂單數
            int rowCount = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM violet_order WHERE uname=@uname", Db.Param("@uname", txtName.Text)));
            if (rowCount == 0)
            {
                panelOrder.Visible = false;
                Label7.Text = "No Orders Have Been Placed";
            }

            if (uid > 5000)
            {
                btnAddProduct.Visible = true;
                Label8.Visible = true;
                Panel1.Visible = true;
            }

            disableInput();
            Submit.Visible = false;
        }

        /// <summary>
        /// 送出按鈕：以參數化查詢將電話、地址、國家、州、城市更新回 violet_user_login。
        /// </summary>
        /// <param name="sender">Submit 按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        /// <remarks>
        /// WHERE 條件使用 Session["user"] 同時比對 username 與 email；此方法不更新姓名、Email、使用者名稱、生日或性別。
        /// </remarks>
        protected void Submit_Click(object sender, EventArgs e)
        {
            Update.Visible = true;
            Submit.Visible = false;
            Db.Execute("UPDATE violet_user_login SET phone=@phone, address=@address, country=@country, state=@state, city=@city WHERE username=@user OR email=@user",
                Db.Param("@phone", txtPhone.Text), Db.Param("@address", txtAddress.Value), Db.Param("@country", selectCountry.SelectedItem.Text),
                Db.Param("@state", selectState.SelectedItem.Text), Db.Param("@city", selectCity.SelectedItem.Text), Db.Param("@user", Session["user"].ToString()));
        }

        /// <summary>
        /// 更新按鈕：切換為編輯模式，僅開放電話、地址、國家、州、城市可編輯，並顯示送出按鈕。
        /// </summary>
        /// <param name="sender">Update 按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void Update_Click(object sender, EventArgs e)
        {
            Update.Visible = false;
            Submit.Visible = true;
            // can be updated - phone,address,city,state,country
            disableInput();
            txtPhone.Enabled = true;
            txtAddress.Disabled = false;
            selectCountry.Enabled = true;
            selectCity.Enabled = true;
            selectState.Enabled = true;

            //uname,email,username,password,phone,dob,country,state,city,gender,address,secq,seca

        }

        /// <summary>
        /// 登出按鈕：清除 Session["user"] 後導回首頁。
        /// </summary>
        /// <param name="sender">登出按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        /// <remarks>此處不清除 Session["count"]、Session["uname"] 或其他購物車相關 Session。</remarks>
        protected void btnLogout_Click(object sender, EventArgs e)
        {
            Session["user"] = null;
            Response.Redirect("~/index.aspx");
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
        /// 「上架商品」按鈕（僅賣家可見）：導向 addProducts.aspx。
        /// </summary>
        /// <param name="sender">Add a Product 按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void btnAddProduct_Click(object sender, EventArgs e)
        {
            Response.Redirect("~/addProducts.aspx");
        }

        /// <summary>
        /// 將訂單歷史面板 panelOrder 渲染成 HTML，再以 iTextSharp 的 HTMLWorker 轉成 A4 PDF，
        /// 以附件 OrderInvoice.pdf 輸出給瀏覽器下載。HTMLWorker 已過時，故標記 [Obsolete]。
        /// </summary>
        [Obsolete]
        private void exportpdf()
        {
            Response.ContentType = "application/pdf";
            Response.AddHeader("content-disposition", "attachment;filename=OrderInvoice.pdf");
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            using (StringWriter sw = new StringWriter())
            using (HtmlTextWriter hw = new HtmlTextWriter(sw))
            {
                panelOrder.RenderControl(hw);
                using (StringReader sr = new StringReader(sw.ToString()))
                {
                    using (Document pdfDoc = new Document(PageSize.A4, 10f, 10f, 100f, 0f))
                    using (HTMLWorker htmlparser = new HTMLWorker(pdfDoc))
                    {
                        PdfWriter.GetInstance(pdfDoc, Response.OutputStream);
                        pdfDoc.Open();
                        htmlparser.Parse(sr);
                        pdfDoc.Close();
                    }
                }
            }

            Response.End();
        }

        /// <summary>
        /// 訂單卡片上「Download PDF」按鈕的點擊事件：呼叫 <see cref="exportpdf"/> 匯出全部訂單。
        /// </summary>
        /// <param name="sender">DataList 內的 Download PDF 按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        [Obsolete]
        protected void DownloadPDF(object sender, EventArgs e)
        {
            exportpdf();
        }

        /// <summary>
        /// 覆寫此方法並留空，讓 panelOrder 能在 &lt;form runat="server"&gt; 之外被 RenderControl（匯出 PDF 所需）。
        /// </summary>
        /// <param name="control">即將被 RenderControl 輸出的控制項。</param>
        /// <remarks>這是匯出 Web Forms 控制項為 PDF 的常見繞過方式；搭配 .aspx 的 EnableEventValidation="false" 使用。</remarks>
        public override void VerifyRenderingInServerForm(Control control)
        {
            /* Verifies that the control is rendered */
        }

        /// <summary>
        /// 賣家商品 GridView 每列繫結時觸發：替第 7 欄（Edit/Delete 指令欄）的第一個 LinkButton
        /// 加上 JavaScript 確認對話框。注意：第一個按鈕實際上是「Edit」而非「Delete」。
        /// </summary>
        /// <param name="sender">GridView1。</param>
        /// <param name="e">目前繫結的資料列事件資料。</param>
        protected void GridView1_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                Control control = e.Row.Cells[6].Controls[0];
                if (control is LinkButton)
                {
                    ( (LinkButton)control ).OnClientClick = "return confirm('Are you Sure?')";
                }
            }
        }
    }
}