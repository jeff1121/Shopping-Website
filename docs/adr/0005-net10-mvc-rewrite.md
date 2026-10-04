# 新站使用 net10.0 ASP.NET Core MVC，與舊站同時運行

新站是 net10.0 的 ASP.NET Core MVC，用 Razor 與 Tailwind 呈現。舊的 Web Forms 站與新站是兩個 Web App，同時對外服務。不採用 .NET 8，因為它的支援在 2026-11-10 結束。不採用 .NET Framework 4.8。

## Consequences

- macOS 可以建置新站。舊站仍然只能在 Windows 建置。
- Azure 上會有兩個網站網址，都在資源群組 rg-shopping。
- 舊站的 Windows Web App 不停止，流量也不改走 Front Door。
