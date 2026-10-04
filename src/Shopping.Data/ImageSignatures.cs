namespace Shopping.Data;

/// <summary>商品圖片的副檔名與檔頭檢查。</summary>
public static class ImageSignatures
{
    /// <summary>單張圖片上限：2 MB。</summary>
    public const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>副檔名是否為 jpg、jpeg、png、gif 或 webp。</summary>
    public static bool IsSupportedExtension(string? extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>檔頭是否與副檔名一致。</summary>
    public static bool HasValidSignature(Stream stream, string extension)
    {
        if (!IsSupportedExtension(extension))
        {
            return false;
        }

        var header = new byte[16];
        var read = stream.Read(header, 0, header.Length);
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var normalized = extension.ToLowerInvariant();
        if (normalized is ".jpg" or ".jpeg")
        {
            return StartsWith(header, read, 0, 0xFF, 0xD8, 0xFF);
        }

        if (normalized == ".png")
        {
            return StartsWith(header, read, 0, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
        }

        if (normalized == ".gif")
        {
            return StartsWith(header, read, 0, 0x47, 0x49, 0x46, 0x38)
                && (StartsWith(header, read, 4, 0x37, 0x61) || StartsWith(header, read, 4, 0x39, 0x61));
        }

        return StartsWith(header, read, 0, 0x52, 0x49, 0x46, 0x46) && StartsWith(header, read, 8, 0x57, 0x45, 0x42, 0x50);
    }

    private static bool StartsWith(byte[] data, int length, int offset, params byte[] expected)
    {
        if (offset + expected.Length > length)
        {
            return false;
        }

        for (var i = 0; i < expected.Length; i++)
        {
            if (data[offset + i] != expected[i])
            {
                return false;
            }
        }

        return true;
    }
}
