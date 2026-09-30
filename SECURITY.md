# 安全性政策

## 支援版本

本專案只維護 `main` 分支，安全修正只會套用到 `main`。

## 回報漏洞

請**不要**以公開 Issue、PR 或討論區回報安全漏洞。

請使用 GitHub 的私下安全通報：
[Security → Report a vulnerability](https://github.com/jeff1121/Shopping-Website/security/advisories/new)

回報時請盡量提供：

- 漏洞類型（例如 SQL Injection、XSS）與影響範圍
- 受影響的頁面或檔案路徑
- 重現步驟或概念驗證
- 可能的修正建議（選填）

## 處理流程

1. 收到通報後 7 天內回覆確認。
2. 評估嚴重度並排定修正時程；High 以上優先處理。
3. 修正合併到 `main` 並部署後，發布 Security Advisory 並致謝回報者（除非您不希望具名）。

## 已知問題

本專案源自教學範例，目前仍有已知的安全問題（例如字串串接 SQL、明碼密碼），
已列入 [Plan.md](Plan.md) 的 M7「應用程式安全修正」，並以 `security` 標籤的 Issue（#4～#6）追蹤。
這些已知問題不需要另外通報。
