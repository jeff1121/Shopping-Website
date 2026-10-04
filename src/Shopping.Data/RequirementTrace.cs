namespace Shopping.Data;

/// <summary>比對計畫裡的 FR 編號是否出現在測試原始碼。</summary>
public static class RequirementTrace
{
    /// <summary>檢查結果。</summary>
    public sealed record Result(IReadOnlyList<string> MissingUnit, IReadOnlyList<string> MissingE2E, bool Unreadable);

    /// <summary>計畫文字無法使用時回傳 Unreadable。</summary>
    public static Result Check(string? planText, string? unitSource, string? e2eSource, IReadOnlySet<string> e2eRequired)
    {
        if (string.IsNullOrWhiteSpace(planText))
        {
            return new Result(Array.Empty<string>(), Array.Empty<string>(), true);
        }

        var unit = unitSource ?? "";
        var e2e = e2eSource ?? "";
        var numbers = FindNumbers(planText).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
        var missingUnit = numbers.Where(number => !unit.Contains(number, StringComparison.Ordinal)).ToList();
        var missingE2E = numbers.Where(number => e2eRequired.Contains(number) && !e2e.Contains(number, StringComparison.Ordinal)).ToList();
        return new Result(missingUnit, missingE2E, false);
    }

    /// <summary>讀取檔案。檔案不存在時回傳 null。</summary>
    public static string? ReadIfExists(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return File.ReadAllText(path);
    }

    private static List<string> FindNumbers(string text)
    {
        var found = new List<string>();
        for (var index = 0; index < text.Length; index++)
        {
            if (index + 4 > text.Length)
            {
                break;
            }

            if (text[index] != 'F' || text[index + 1] != 'R' || text[index + 2] != '-')
            {
                continue;
            }

            var end = index + 3;
            if (!char.IsDigit(text[end]))
            {
                continue;
            }

            while (end < text.Length && char.IsDigit(text[end]))
            {
                end++;
            }

            found.Add(text.Substring(index, end - index));
            index = end - 1;
        }

        return found;
    }
}
