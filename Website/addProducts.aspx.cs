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
    /// 「上架商品」頁面（addProducts.aspx）的後置程式碼。
    /// 賣家填寫商品名稱、價格、分類、關鍵字並上傳圖片，圖片經 <see cref="ImageStore"/> 寫入 Azure Blob Storage，
    /// 商品資料（pimage 為圖片完整網址）寫入 violet_products。
    /// </summary>
    /// <remarks>
    /// 只有登入中的賣家（uid &gt; 5000）可使用：未登入導向 login.aspx，一般會員導向 profile.aspx。
    /// 圖片須通過副檔名、大小（<see cref="ImageStore.MaxBytes"/>）與檔案開頭格式識別碼檢查才會上傳。
    /// </remarks>
    public partial class addProducts : System.Web.UI.Page
    {
        /// <summary>
        /// 頁面載入事件：未登入導向登入頁、非賣家導向個人頁；其餘處理共用頁首的登入狀態顯示，
        /// 首次載入時填入寫死的商品分類選項。PostBack 也會先經過這裡，因此送出時同樣受到權限檢查。
        /// </summary>
        /// <param name="sender">ASP.NET Web Forms 傳入的事件來源。</param>
        /// <param name="e">頁面載入事件資料。</param>
        /// <remarks>
        /// 讀取 Session["count"] 作為購物車徽章；分類只包含 Computer 與 Computer Accesories，需與 violet_categories.name 相同。
        /// </remarks>
        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["user"] == null)
            {
                Response.Redirect("~/login.aspx");
                return;
            }

            if (!UserAccounts.IsSellerLogin(Session["user"]))
            {
                Response.Redirect("~/profile.aspx");
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
                selectCategory.Items.Clear();
                // 分類選項寫死於程式中，需與 violet_categories.name 保持一致才能在分類頁篩選到
                String[] items = new string[] { "Computer", "Computer Accesories", };
                foreach (string i in items)
                {
                    selectCategory.Items.Add(i);
                }
            }
        }

        /// <summary>
        /// 登出按鈕：清除 Session["user"] 後導回首頁。
        /// </summary>
        /// <param name="sender">登出按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        /// <remarks>不清除 Session["count"] 或 Session["uname"]。</remarks>
        protected void btnLogout_Click(object sender, EventArgs e)
        {
            Session["user"] = null;
            Response.Redirect("~/index.aspx");
        }


        /// <summary>
        /// 驗證並上傳商品圖片，成功後寫入商品資料：
        /// 1. 必須選擇檔案，副檔名須通過 <see cref="ImageStore.IsSupportedExtension"/>（jpg、jpeg、png、gif、webp，不分大小寫）；
        /// 2. 檔案大小不得超過 <see cref="ImageStore.MaxBytes"/>，開頭格式識別碼須與副檔名相符（<see cref="ImageStore.HasValidSignature"/>）；
        /// 3. 商品名稱最多 50 字元、關鍵字最多 500 字元，且名稱尚未被使用（violet_products.pname 為主索引鍵）；
        /// 4. 圖片經 <see cref="ImageStore.Upload"/> 寫入 Blob「products/&lt;賣家帳號&gt;/&lt;GUID&gt;.副檔名」，取得完整網址；
        /// 5. 以參數化、指定欄位的 INSERT 寫入 violet_products（uname 為賣家姓名，stock 為 0），完成後導向 profile.aspx。
        /// </summary>
        public void uploadImg()
        {
            if (!uploadImage.HasFile)
            {
                ShowError("Please Upload your Image");
                return;
            }

            string fileExt = System.IO.Path.GetExtension(uploadImage.FileName);
            if (!ImageStore.IsSupportedExtension(fileExt))
            {
                ShowError("File Extension Is InValid - Only Upload PNG/JPEG/GIF/WEBP File");
                return;
            }

            if (uploadImage.PostedFile.ContentLength > ImageStore.MaxBytes)
            {
                ShowError("Image must be 2 MB or smaller");
                return;
            }

            if (!ImageStore.HasValidSignature(uploadImage.PostedFile.InputStream, fileExt))
            {
                ShowError("File content is not a valid PNG/JPEG/GIF/WEBP image");
                return;
            }

            string keywords = txtKeywords.Value;
            if (txtName.Text.Length > 50 || keywords.Length > 500)
            {
                ShowError("Name allows 50 characters, Keywords allow 500");
                return;
            }

            // 先檢查名稱是否重複，避免上傳後才因主索引鍵衝突留下用不到的圖片
            if (Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM violet_products WHERE pname=@pname", Db.Param("@pname", txtName.Text))) > 0)
            {
                ShowError("A product with this name already exists");
                return;
            }

            // 圖片寫入 Blob，資料庫儲存經 Front Door 提供的完整網址，頁面 <img> 可直接使用
            string image = ImageStore.Upload(uploadImage.PostedFile.InputStream, Session["user"].ToString(), fileExt);
            string uname = Db.GetUname(Session["user"]);

            try
            {
                // 新商品庫存為 0，賣家需在 profile.aspx 的商品清單編輯庫存後才可購買
                Db.Execute("INSERT INTO violet_products (pname, price, pimage, category, uname, keywords, stock) VALUES (@pname, @price, @pimage, @category, @uname, @keywords, 0)",
                    Db.Param("@pname", txtName.Text), Db.Param("@price", txtPrice.Text), Db.Param("@pimage", image),
                    Db.Param("@category", selectCategory.SelectedItem.Text), Db.Param("@uname", uname), Db.Param("@keywords", keywords));
            }
            catch (SqlException ex) when (Db.IsDuplicateKey(ex))
            {
                // 檢查後到寫入前，另一位賣家剛好使用了相同名稱
                ShowError("A product with this name already exists");
                return;
            }

            Response.Redirect("~/profile.aspx");
        }

        /// <summary>
        /// 以紅字顯示上架失敗的原因。
        /// </summary>
        /// <param name="message">要顯示的訊息。</param>
        private void ShowError(string message)
        {
            Label1.Text = message;
            Label1.ForeColor = System.Drawing.Color.Red;
            Label1.Visible = true;
        }

        /// <summary>
        /// 送出按鈕：伺服器端驗證控制項通過後，呼叫 <see cref="uploadImg"/> 上傳圖片並新增商品。
        /// </summary>
        /// <param name="sender">Submit 按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void Submit_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
            {
                return;
            }

            uploadImg();
        }
    }
}