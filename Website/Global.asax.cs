using System;
using System.Configuration;
using System.Diagnostics;
using System.Web;
using Website.Config;

namespace Website
{
    /// <summary>
    /// 應用程式層級事件處理（Global.asax）。
    /// 啟動時檢查必要設定；失敗時記錄錯誤，並讓之後每個請求都回應 500，避免網站以不完整設定運作。
    /// </summary>
    public class Global : HttpApplication
    {
        /// <summary>啟動失敗的原因；為 null 代表啟動成功。只透過 <see cref="RecordStartupError"/> 寫入。</summary>
        private static Exception startupError;

        /// <summary>
        /// 應用程式啟動事件：呼叫 <see cref="AppSettings.Validate"/> 檢查環境變數是否齊全。
        /// 錯誤只包含設定名稱，寫入 Trace（App Service 應用程式記錄／Application Insights 會收集）。
        /// </summary>
        /// <param name="sender">事件來源。</param>
        /// <param name="e">事件資料。</param>
        protected void Application_Start(object sender, EventArgs e)
        {
            try
            {
                AppSettings.Validate();
            }
            catch (ConfigurationErrorsException ex)
            {
                RecordStartupError(ex);
            }
        }

        /// <summary>
        /// 記錄啟動失敗：保存例外供之後的請求判斷，並寫入 Trace。
        /// </summary>
        /// <param name="ex">啟動時發生的例外。</param>
        private static void RecordStartupError(Exception ex)
        {
            startupError = ex;
            Trace.TraceError("網站啟動失敗：" + ex);
        }

        /// <summary>
        /// 每個請求開始時檢查啟動狀態；啟動失敗時直接回應 500 與通用訊息（不洩漏設定值）。
        /// </summary>
        /// <param name="sender">事件來源。</param>
        /// <param name="e">事件資料。</param>
        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            if (startupError == null)
            {
                return;
            }

            HttpResponse response = Context.Response;
            response.StatusCode = 500;
            response.TrySkipIisCustomErrors = true;
            response.ContentType = "text/plain; charset=utf-8";
            response.Write("網站啟動失敗：設定不完整或資料庫無法初始化，請查看應用程式記錄。");
            if (Context.Request.IsLocal)
            {
                response.Write("\n" + startupError.Message);
            }

            CompleteRequest();
        }
    }
}
