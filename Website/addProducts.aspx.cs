using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Website.Data;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Website
{
    /// <summary>
    /// 「上架商品」頁面（addProducts.aspx）的後置程式碼。
    /// 賣家填寫商品名稱、價格、分類、關鍵字並上傳圖片，圖片存到 img/products/&lt;登入帳號&gt;/，
    /// 商品資料寫入 violet_products。
    /// </summary>
    /// <remarks>
    /// 頁面只依 Session["user"] 判斷登入狀態，未檢查 uid 是否 &gt; 5000；未登入時分類下拉選單不會被填入，
    /// 但使用者仍可能直接送出造成 Session 或檔案上傳流程例外。
    /// </remarks>
    public partial class addProducts : System.Web.UI.Page
    {
        /// <summary>資料庫連線（透過 Db 從 Web.config 的 cmpConnectionString 讀取）。</summary>
        SqlConnection con = Db.CreateConnection();

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
        /// 1. 僅接受 jpg / jpeg / png（依副檔名判斷，區分大小寫）；
        /// 2. 圖片存到 ~/img/products/&lt;Session["user"]&gt;/原始檔名，目錄不存在時自動建立；
        /// 3. 查出登入者姓名 uname 作為賣家欄位，以參數化查詢寫入 violet_products；
        /// 4. 完成後導向 profile.aspx。
        /// </summary>
        /// <remarks>
        /// INSERT 未指定欄位且只提供六個值；若 violet_products 含 stock 等額外 NOT NULL 欄位，會出現欄位數不符或預設值需求。
        /// 原始檔名會直接用於儲存路徑，可能覆蓋同名檔案；未選檔時先取副檔名會先拋出例外。
        /// </remarks>
        public void uploadImg()
        {
            var supportedTypes = new[] { "jpg", "jpeg", "png" };
            // 注意：未選檔案時 GetExtension 回傳空字串，Substring(1) 會先拋出例外
            var fileExt = System.IO.Path.GetExtension(uploadImage.FileName).Substring(1);

            if (uploadImage.HasFile)
            {
                if (!supportedTypes.Contains(fileExt))
                {
                    Label1.Visible = true;
                    Label1.Text = "File Extension Is InValid - Only Upload PNG/JPEG/JPG File";
                    Label1.ForeColor = System.Drawing.Color.Red;
                }
                else
                {
                    string folderPath = Server.MapPath("~/img/products/" + Session["user"] + "/");

                    //Check whether Directory (Folder) exists.
                    if (!Directory.Exists(folderPath))
                    {
                        //If Directory (Folder) does not exists. Create it.
                        Directory.CreateDirectory(folderPath);
                    }

                    //String path = folderPath + Path.GetFileName(uploadImage.FileName);
                    //uploadImage.SaveAs(path);

                    string str = uploadImage.FileName;
                    uploadImage.PostedFile.SaveAs(folderPath + "\\" + str.ToString());
                    // 資料庫中儲存相對路徑，供頁面 <img> 直接使用
                    string Image = "img/products/" + Session["user"] +"/" + str.ToString();
                    string name = txtName.Text;

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

                    // 位置式 INSERT：依序為 pname, price, pimage, category, uname, keywords（未含 stock 欄位）
                    SqlCommand cmd1 = new SqlCommand("INSERT INTO violet_products VALUES(@pname, @price, @Image, @category, @uname, @keywords)", con);
                    cmd1.Parameters.AddWithValue("@pname", name);
                    cmd1.Parameters.AddWithValue("Image", Image);
                    cmd1.Parameters.AddWithValue("@price", txtPrice.Text);
                    cmd1.Parameters.AddWithValue("@category", selectCategory.SelectedItem.ToString());
                    cmd1.Parameters.AddWithValue("@uname", uname);
                    cmd1.Parameters.AddWithValue("@keywords", txtKeywords.Value);

                    con.Open();
                    cmd1.ExecuteNonQuery();
                    con.Close();

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