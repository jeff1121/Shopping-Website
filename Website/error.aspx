<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="error.aspx.cs" Inherits="Website.error" %>
<%-- 錯誤頁：customErrors 以 ResponseRewrite 導向此頁，只顯示一般訊息與回首頁連結，不顯示例外細節；不使用資料庫與 Session，避免錯誤來源再次失敗。 --%>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head>
    <title><%: PageTitle %></title>
    <meta name="robots" content="noindex" />

    <!--Css Link-->
        <link rel="stylesheet" type="text/css" href="css/style.css" />
</head>
<body>
    <div style="margin: 120px auto; max-width: 600px; text-align: center; font-family: Verdana, sans-serif">
        <a href="index.aspx"><img src="img/logo.png" alt="Logo" /></a>
        <h1><%: PageTitle %></h1>
        <p><%: PageMessage %></p>
        <p><a href="index.aspx">Back to Home</a></p>
    </div>
</body>
</html>
