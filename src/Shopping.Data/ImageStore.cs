using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Shopping.Data;

/// <summary>把商品圖片寫入 Blob，並回傳對外網址。</summary>
public sealed class ImageStore
{
    private readonly BlobContainerClient _container;
    private readonly string _imageBaseUrl;

    /// <summary>使用已經建立的容器用戶端。</summary>
    public ImageStore(BlobContainerClient container, string imageBaseUrl)
    {
        _container = container;
        _imageBaseUrl = imageBaseUrl.TrimEnd('/');
    }

    /// <summary>依端點決定用 Azurite 或受控識別。</summary>
    public static BlobContainerClient CreateClient(string endpoint, string container, string? clientId)
    {
        if (endpoint.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase) || endpoint.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            var connection = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=" + endpoint.TrimEnd('/') + ";";
            return new BlobContainerClient(connection, container);
        }

        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = clientId });
        var uri = new Uri(endpoint.TrimEnd('/') + "/" + container);
        return new BlobContainerClient(uri, credential);
    }

    /// <summary>上傳通過檢查的圖片。失敗時不寫入。</summary>
    public async Task<string> UploadAsync(Stream content, string userName, string extension, CancellationToken cancellationToken = default)
    {
        if (!ImageSignatures.IsSupportedExtension(extension))
        {
            throw new InvalidOperationException("副檔名不允許。");
        }

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > ImageSignatures.MaxBytes)
        {
            throw new InvalidOperationException("圖片超過 2 MB。");
        }

        buffer.Position = 0;
        if (!ImageSignatures.HasValidSignature(buffer, extension))
        {
            throw new InvalidOperationException("檔案內容不是有效圖片。");
        }

        var blobName = userName + "/" + Guid.NewGuid().ToString("N") + extension.ToLowerInvariant();
        buffer.Position = 0;
        var blob = _container.GetBlobClient(blobName);
        await blob.UploadAsync(buffer, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = ContentTypeFor(extension) },
        }, cancellationToken);
        return _imageBaseUrl + "/products/" + blobName;
    }

    /// <summary>建立容器（已存在則略過）。</summary>
    public async Task EnsureContainerAsync(CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
    }

    /// <summary>副檔名對應的 Content-Type。不支援時丟出例外。</summary>
    public static string ContentTypeFor(string extension)
    {
        if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return "image/jpeg";
        }

        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return "image/png";
        }

        if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            return "image/gif";
        }

        if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return "image/webp";
        }

        throw new InvalidOperationException("副檔名不允許。");
    }
}
