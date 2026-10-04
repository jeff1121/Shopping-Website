using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Shopping.Data;

/// <summary>訂單 PDF 的一列。</summary>
public sealed record InvoiceLine(string Name, decimal UnitPrice, int Quantity, decimal LineTotal);

/// <summary>用 QuestPDF 畫一頁訂單 PDF。</summary>
public static class InvoicePdf
{
    /// <summary>產生 PDF 位元組。</summary>
    public static byte[] Render(string number, string dateText, IReadOnlyList<InvoiceLine> lines, decimal total)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(style => style.FontSize(12));
                page.Content().Column(column =>
                {
                    column.Item().Text(number).FontSize(18);
                    column.Item().Text(dateText);
                    foreach (var line in lines)
                    {
                        column.Item().Text(line.Name + " " + line.UnitPrice.ToString("0.00") + " x " + line.Quantity + " = " + line.LineTotal.ToString("0.00"));
                    }

                    column.Item().Text(total.ToString("0.00"));
                });
            });
        }).GeneratePdf();
    }
}
