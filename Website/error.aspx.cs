using System;
using System.Web.UI;

namespace Website
{
    /// <summary>
    /// 錯誤頁（error.aspx）：由 Web.config 的 customErrors（redirectMode="ResponseRewrite"）在發生未處理例外或找不到頁面時顯示。
    /// 只回傳一般訊息與正確的 HTTP 狀態碼（404 或 500），不顯示例外內容；例外細節由 Global.asax 的 Application_Error 寫入記錄。
    /// </summary>
    public partial class error : Page
    {
        /// <summary>
        /// 頁面標題（依狀態碼決定）。
        /// </summary>
        protected string PageTitle { get; private set; }

        /// <summary>
        /// 顯示給使用者的說明文字（依狀態碼決定）。
        /// </summary>
        protected string PageMessage { get; private set; }

        /// <summary>
        /// 頁面載入：依查詢字串 code 設定狀態碼與訊息；只接受 404，其餘一律視為 500。
        /// 設定 TrySkipIisCustomErrors，避免 IIS 以自己的錯誤頁取代本頁內容。
        /// </summary>
        /// <param name="sender">事件來源。</param>
        /// <param name="e">事件資料。</param>
        protected void Page_Load(object sender, EventArgs e)
        {
            bool notFound = Request.QueryString["code"] == "404";
            Response.StatusCode = notFound ? 404 : 500;
            Response.TrySkipIisCustomErrors = true;
            PageTitle = notFound ? "Page Not Found" : "Something Went Wrong";
            PageMessage = notFound
                ? "The page you are looking for does not exist."
                : "Sorry, an unexpected error occurred. Please try again later.";
        }
    }
}
