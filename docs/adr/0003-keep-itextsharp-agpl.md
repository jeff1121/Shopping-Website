# 沿用 iTextSharp 5（AGPL）產生訂單 PDF

訂單 PDF 以 iTextSharp 的 `HTMLWorker` 把訂單表格的 HTML 轉成 PDF。我們改從 NuGet 取得 iTextSharp 5.5.13.x，而不是換成 MIT 授權的 PdfSharp：PdfSharp 沒有 HTML 轉 PDF 功能，需要重寫發票繪製程式；而本 Repo 本來就公開原始碼，符合 AGPL 的要求。Dependency Review 的授權阻擋規則會把 iTextSharp 列為例外。

## Consequences

- 若日後改為閉源或商業授權，必須先改用 PdfSharp 或購買 iText 商業授權。
