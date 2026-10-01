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
    /// 「上架商品」頁面（addProducts.aspx）的後置程式碼。
    /// 賣家填寫商品名稱、價格、分類、關鍵字並上傳圖片，圖片經 <see cref="ImageStore"/> 寫入 Azure Blob Storage，
    /// 商品資料（pimage 為圖片完整網址）寫入 violet_products。
    /// </summary>
    /// <remarks>
    /// 頁面只依 Session["user"] 判斷登入狀態，未檢查 uid 是否 &gt; 5000；未登入時分類下拉選單不會被填入，
    /// 但使用者仍可能直接送出造成 Session 或檔案上傳流程例外。
    /// </remarks>
    public partial class addProducts : System.Web.UI.Page
    {
        /// <summary>
        /// 頁面載入事件：處理共用頁首的登入狀態顯示；首次載入時填入寫死的商品分類選項。
        /// 注意：未登入時不會填入分類，也沒有阻擋未登入或非賣家存取。
        /// </summary>
        /// <param name="sender">ASP.NET Web Forms 傳入的事件來源。</param>
        /// <param name="e">頁面載入事件資料。</param>
        /// <remarks>
        /// 讀取 Session["count"] 作為購物車徽章；分類只包含 Computer 與 Computer Accesories，需與 violet_categories.name 相同。
        /// </remarks>
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
        /// 1. 僅接受 <see cref="ImageStore.IsSupportedExtension"/> 允許的副檔名（jpg、jpeg、png、gif、webp，不分大小寫）；
        /// 2. 圖片經 <see cref="ImageStore.Upload"/> 寫入 Blob「products/&lt;賣家帳號&gt;/&lt;GUID&gt;.副檔名」，取得完整網址；
        /// 3. 查出登入者姓名 uname 作為賣家欄位，以參數化、指定欄位的 INSERT 寫入 violet_products（stock 為 0）；
        /// 4. 完成後導向 profile.aspx。
        /// </summary>
        /// <remarks>目前只檢查副檔名，尚未檢查檔案內容與大小（M7 修正）。</remarks>
        public void uploadImg()
        {
            if (uploadImage.HasFile)
            {
                string fileExt = System.IO.Path.GetExtension(uploadImage.FileName);
                if (!ImageStore.IsSupportedExtension(fileExt))
                {
                    Label1.Visible = true;
                    Label1.Text = "File Extension Is InValid - Only Upload PNG/JPEG/GIF/WEBP File";
                    Label1.ForeColor = System.Drawing.Color.Red;
                }
                else
                {
                    // 圖片寫入 Blob，資料庫儲存經 Front Door 提供的完整網址，頁面 <img> 可直接使用
                    string Image = ImageStore.Upload(uploadImage.PostedFile.InputStream, Session["user"].ToString(), fileExt);
                    string uname = Db.GetUname(Session["user"]);

                    // 新商品庫存為 0，賣家需在 profile.aspx 的商品清單編輯庫存後才可購買
                    Db.Execute("INSERT INTO violet_products (pname, price, pimage, category, uname, keywords, stock) VALUES (@pname, @price, @pimage, @category, @uname, @keywords, 0)",
                        Db.Param("@pname", txtName.Text), Db.Param("@price", txtPrice.Text), Db.Param("@pimage", Image),
                        Db.Param("@category", selectCategory.SelectedItem.Text), Db.Param("@uname", uname), Db.Param("@keywords", txtKeywords.Value));

                    Label1.Text = "Image Uploaded";
                    Label1.ForeColor = System.Drawing.Color.ForestGreen;

                    Response.Redirect("~/profile.aspx");
                }
            }

            else
            {
                Label1.Text = "Please Upload your Image";
                Label1.ForeColor = System.Drawing.Color.Red;
            }
        }

        /// <summary>
        /// 送出按鈕：呼叫 <see cref="uploadImg"/> 上傳圖片並新增商品。
        /// </summary>
        /// <param name="sender">Submit 按鈕。</param>
        /// <param name="e">按鈕點擊事件資料。</param>
        protected void Submit_Click(object sender, EventArgs e)
        {
            uploadImg();
        }
    }
}