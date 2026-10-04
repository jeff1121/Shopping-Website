using Shopping.Data;

namespace Shopping.Web.Tests;

/// <summary>讀計畫與兩個測試專案，確認編號沒有漏。</summary>
public class RequirementTraceTests
{
    private static readonly string[] RequiredEndToEnd =
    [
        "FR-01", "FR-02", "FR-03", "FR-04", "FR-05", "FR-06", "FR-07", "FR-08",
        "FR-10", "FR-11", "FR-12", "FR-13", "FR-14", "FR-15", "FR-16", "FR-17", "FR-18",
        "FR-19", "FR-20", "FR-21", "FR-22", "FR-23", "FR-24",
        "FR-30", "FR-31", "FR-32", "FR-33", "FR-34", "FR-35", "FR-36", "FR-37", "FR-38",
        "FR-40", "FR-41", "FR-42",
        "FR-50", "FR-51", "FR-52", "FR-53", "FR-54", "FR-55", "FR-56", "FR-57", "FR-58",
        "FR-60", "FR-61", "FR-62", "FR-63", "FR-64", "FR-65", "FR-67", "FR-68", "FR-69",
        "FR-70", "FR-71", "FR-72",
        "FR-80", "FR-81", "FR-82", "FR-84", "FR-85", "FR-86", "FR-87", "FR-88", "FR-89", "FR-90", "FR-91",
        "FR-100", "FR-101", "FR-102", "FR-103", "FR-104", "FR-105", "FR-107", "FR-108", "FR-109",
    ];

    /// <summary>第 6 節區塊與靜態清單一致，而且計畫編號都出現在測試原始碼。</summary>
    [Fact]
    public void SourcesCoverPlanNumbers()
    {
        var root = ShopApp.FindRepoRoot();
        var tasks = File.ReadAllText(Path.Combine(root, "Refactory-Tasks.md"));
        var fence = ReadFence(tasks);
        Assert.Equal(RequiredEndToEnd, fence);
        var plan = RequirementTrace.ReadIfExists(Path.Combine(root, "Refactory-Plan.md"));
        var unit = ReadSources(Path.Combine(root, "tests", "Shopping.Web.Tests"));
        var e2e = ReadSources(Path.Combine(root, "tests", "Shopping.E2E"));
        var result = RequirementTrace.Check(plan, unit, e2e, fence.ToHashSet(StringComparer.Ordinal));
        Assert.False(result.Unreadable);
        Assert.Empty(result.MissingUnit);
        Assert.Empty(result.MissingE2E);
    }

    private static List<string> ReadFence(string markdown)
    {
        var found = new List<string>();
        var inside = false;
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            if (!inside)
            {
                if (line == "```text")
                {
                    inside = true;
                }

                continue;
            }

            if (line == "```")
            {
                break;
            }

            if (line.Length > 0)
            {
                found.Add(line);
            }
        }

        return found;
    }

    private static string ReadSources(string directory)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.EndsWith($"{Path.DirectorySeparatorChar}RequirementTraceTests.cs", StringComparison.Ordinal))
            {
                continue;
            }

            builder.AppendLine(File.ReadAllText(file));
        }

        return builder.ToString();
    }
}
