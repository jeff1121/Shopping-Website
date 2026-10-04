# 新表與 violet 表並存在同一個資料庫

新站使用同一個 Azure SQL，表名加上 shop_ 前綴，放在既有的 violet_ 表旁邊。啟動時由網站套用內嵌的 SQL。GitHub runner 不連線到 SQL。不把舊資料複製到新表，也不對 violet_ 建立外鍵。

## Consequences

- 舊站與新站可以同時讀寫，互不改對方的表。
- 部署流程不需要資料庫密碼。Migration 在 Web App 啟動時執行。
- 清除示範資料必須使用只處理 shop_ 的腳本，不能執行舊的 violet_ 清除腳本。
