using System.Globalization;
using System.Xml.Linq;

namespace Shopping.Data;

/// <summary>判斷 Cobertura 報告是否達到兩個產品套件各 100% 行與分支。</summary>
public static class CoverageReport
{
    /// <summary>通過時回傳 null。失敗時回傳原因。</summary>
    public static string? FailureReason(string coberturaXml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(coberturaXml);
        }
        catch (System.Xml.XmlException exception)
        {
            return "不是 Cobertura：" + exception.Message;
        }

        var packages = document.Descendants("package").ToList();
        foreach (var name in new[] { "Shopping.Web", "Shopping.Data" })
        {
            var matched = packages.Where(item => Belongs(item.Attribute("name")?.Value, name)).ToList();
            if (matched.Count == 0)
            {
                return "缺少套件 " + name;
            }

            foreach (var package in matched)
            {
                if (!IsExactOne(package.Attribute("line-rate")?.Value))
                {
                    return name + " 的 line-rate 不是 1";
                }

                if (!IsExactOne(package.Attribute("branch-rate")?.Value))
                {
                    return name + " 的 branch-rate 不是 1";
                }
            }
        }

        return null;
    }

    private static bool Belongs(string? packageName, string assembly)
    {
        if (packageName is null)
        {
            return false;
        }

        return packageName == assembly || packageName.StartsWith(assembly + ".", StringComparison.Ordinal);
    }

    private static bool IsExactOne(string? value)
    {
        if (value is null)
        {
            return false;
        }

        if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        return number == 1m;
    }
}
