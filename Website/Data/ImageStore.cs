using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Website.Config;

namespace Website.Data
{
    /// <summary>
    /// 商品圖片儲存：把賣家上傳的圖片寫入 Azure Blob Storage，並回傳經 Front Door 提供的完整網址。
    /// Azure 上以使用者指派受控識別（應用程式設定 AZURE_CLIENT_ID 指定 id-shopping-web）驗證；
    /// 本機若端點指向 Azurite（127.0.0.1 或 localhost），改用開發儲存體連線。
    /// </summary>
    public static class ImageStore
    {
        /// <summary>允許的副檔名與對應的 Content-Type。</summary>
        private static readonly Dictionary<string, string> ContentTypes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { ".jpg", "image/jpeg" },
                { ".jpeg", "image/jpeg" },
                { ".png", "image/png" },
                { ".gif", "image/gif" },
                { ".webp", "image/webp" }
            };

        /// <summary>延遲建立、整個應用程式共用的容器用戶端（BlobContainerClient 為執行緒安全）。</summary>
        private static readonly Lazy<BlobContainerClient> Container = new Lazy<BlobContainerClient>(CreateContainerClient);

        /// <summary>
        /// 判斷副檔名是否為允許上傳的圖片格式。
        /// </summary>
        /// <param name="extension">含點的副檔名，例如 .png。</param>
        /// <returns>允許時為 true。</returns>
        public static bool IsSupportedExtension(string extension)
        {
            return !string.IsNullOrEmpty(extension) && ContentTypes.ContainsKey(extension);
        }

        /// <summary>
        /// 上傳商品圖片到「容器/賣家/GUID.副檔名」，設定 Content-Type 與一天的快取標頭。
        /// </summary>
        /// <param name="content">圖片內容串流。</param>
        /// <param name="seller">賣家登入帳號；非英數字元會替換為底線。</param>
        /// <param name="extension">含點的副檔名，必須通過 <see cref="IsSupportedExtension"/>。</param>
        /// <returns>圖片完整網址：IMAGE_BASE_URL/容器/賣家/檔名。</returns>
        /// <exception cref="ArgumentException">副檔名不在允許清單中。</exception>
        public static string Upload(Stream content, string seller, string extension)
        {
            if (!IsSupportedExtension(extension))
            {
                throw new ArgumentException("不支援的圖片格式：" + extension, "extension");
            }

            string blobName = SanitizeSegment(seller) + "/" + Guid.NewGuid().ToString("N") + extension.ToLowerInvariant();
            BlobClient blob = Container.Value.GetBlobClient(blobName);
            BlobUploadOptions options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = ContentTypes[extension],
                    CacheControl = "public, max-age=86400"
                }
            };
            blob.Upload(content, options);

            return AppSettings.ImageBaseUrl + "/" + AppSettings.StorageContainer + "/" + blobName;
        }

        /// <summary>
        /// 依設定建立 Blob 容器用戶端：Azurite 端點用開發儲存體連線，其餘使用 DefaultAzureCredential。
        /// </summary>
        /// <returns>商品圖片容器的用戶端。</returns>
        private static BlobContainerClient CreateContainerClient()
        {
            Uri endpoint = new Uri(AppSettings.StorageBlobEndpoint);
            BlobServiceClient service;
            if (endpoint.IsLoopback)
            {
                // Azurite 預設帳號 devstoreaccount1；SDK 內建其公開的開發金鑰，不需在設定中提供。
                service = new BlobServiceClient("UseDevelopmentStorage=true");
            }
            else
            {
                DefaultAzureCredentialOptions options = new DefaultAzureCredentialOptions();
                string clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
                if (!string.IsNullOrWhiteSpace(clientId))
                {
                    options.ManagedIdentityClientId = clientId;
                }

                service = new BlobServiceClient(endpoint, new DefaultAzureCredential(options));
            }

            return service.GetBlobContainerClient(AppSettings.StorageContainer);
        }

        /// <summary>
        /// 把賣家帳號轉成安全的 Blob 路徑片段：只保留英數字、底線與連字號。
        /// </summary>
        /// <param name="value">原始帳號（可能是 Email）。</param>
        /// <returns>只含安全字元的路徑片段；空白時為 unknown。</returns>
        private static string SanitizeSegment(string value)
        {
            StringBuilder builder = new StringBuilder();
            foreach (char c in value ?? string.Empty)
            {
                bool safe = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                builder.Append(safe ? c : '_');
            }

            return builder.Length == 0 ? "unknown" : builder.ToString();
        }
    }
}
