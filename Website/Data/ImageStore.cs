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

        /// <summary>單張商品圖片的大小上限：2 MB。</summary>
        public const int MaxBytes = 2 * 1024 * 1024;

        /// <summary>Blob 路徑片段允許的字元：英數字、底線與連字號；其餘字元改為底線。</summary>
        private const string SafeSegmentChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-";

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
        /// 檢查檔案開頭的格式識別碼（magic bytes）是否與副檔名相符，避免把其他檔案改副檔名後上傳：
        /// JPEG 為 FF D8 FF；PNG 為 89 50 4E 47 0D 0A 1A 0A；GIF 為 GIF87a 或 GIF89a；WebP 為 RIFF....WEBP。
        /// 可搜尋的串流讀取後會回到原位置，之後仍可上傳。
        /// </summary>
        /// <param name="content">圖片內容串流。</param>
        /// <param name="extension">含點的副檔名。</param>
        /// <returns>內容開頭符合副檔名的格式時為 true。</returns>
        public static bool HasValidSignature(Stream content, string extension)
        {
            if (content == null || !IsSupportedExtension(extension))
            {
                return false;
            }

            byte[] header = new byte[12];
            long start = content.CanSeek ? content.Position : 0;
            int read = 0;
            while (read < header.Length)
            {
                int n = content.Read(header, read, header.Length - read);
                if (n == 0)
                {
                    break;
                }

                read += n;
            }

            if (content.CanSeek)
            {
                content.Position = start;
            }

            switch (extension.ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg":
                    return StartsWith(header, read, 0, 0xFF, 0xD8, 0xFF);
                case ".png":
                    return StartsWith(header, read, 0, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
                case ".gif":
                    return StartsWith(header, read, 0, 0x47, 0x49, 0x46, 0x38) && (StartsWith(header, read, 4, 0x37, 0x61) || StartsWith(header, read, 4, 0x39, 0x61));
                case ".webp":
                    return StartsWith(header, read, 0, 0x52, 0x49, 0x46, 0x46) && StartsWith(header, read, 8, 0x57, 0x45, 0x42, 0x50);
                default:
                    return false;
            }
        }

        /// <summary>
        /// 檢查已讀取的位元組在指定位置是否為預期的序列。
        /// </summary>
        /// <param name="data">已讀取的檔案開頭。</param>
        /// <param name="length">實際讀到的位元組數。</param>
        /// <param name="offset">開始比對的位置。</param>
        /// <param name="expected">預期的位元組。</param>
        /// <returns>完全相符時為 true；讀到的資料不足時為 false。</returns>
        private static bool StartsWith(byte[] data, int length, int offset, params byte[] expected)
        {
            if (offset + expected.Length > length)
            {
                return false;
            }

            for (int i = 0; i < expected.Length; i++)
            {
                if (data[offset + i] != expected[i])
                {
                    return false;
                }
            }

            return true;
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
                builder.Append(SafeSegmentChars.IndexOf(c) >= 0 ? c : '_');
            }

            return builder.Length == 0 ? "unknown" : builder.ToString();
        }
    }
}
