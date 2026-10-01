<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="resetPassword.aspx.cs" Inherits="Website.resetPassword" %>
<%-- 重設密碼：以忘記密碼信中的一次性權杖（?token=）驗證後設定新密碼；權杖無效或過期時顯示提示。 --%>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Reset Password</title>
    <%-- 避免權杖經 Referer 標頭外流 --%>
    <meta name="referrer" content="no-referrer" />

    <!--Css Link-->
        <link rel="stylesheet" type="text/css" href="css/style.css" />
</head>
<body>
    <form id="form1" runat="server">
        <div class="container">
            <!--Header-->
                <%-- 共用頁首與導覽列：Logo、主選單、登入/註冊選單、登出鈕、搜尋/個人/購物車圖示與購物車徽章。 --%>
                <div class="header">
                    <div style="z-index: 1; top: 0px; width: 100%; height: 100px; position: fixed; left: 0px; background-color: white">
                        <asp:HyperLink ID="Logo" runat="server" style="top: 30px; left: 50px; position: absolute" ImageUrl="~/img/logo.png" NavigateUrl="~/index.aspx"></asp:HyperLink>
                    
                        <asp:Menu ID="HeaderMenu" runat="server" Style="top: 30px; left: 250px; position: absolute" Font-Size="Large" Orientation="Horizontal" RenderingMode="Table" DisappearAfter="100" DynamicHorizontalOffset="2" Font-Names="Verdana" ForeColor="black" StaticSubMenuIndent="20%">
                            <DynamicHoverStyle />
                            <DynamicMenuItemStyle HorizontalPadding="10px" VerticalPadding="5px" BackColor="White" />
                            <DynamicMenuStyle BorderStyle="Ridge" BorderColor="Black" />
                            <DynamicSelectedStyle />
                            <Items>
                                <asp:MenuItem Text="Home" Value="Home" NavigateUrl="index.aspx"></asp:MenuItem>
                                <asp:MenuItem Text="Shop" Value="Shop">
                                    <asp:MenuItem Text="Categories" Value="Categories" NavigateUrl="categories.aspx"></asp:MenuItem>
                                    <asp:MenuItem Text="Cart" Value="Cart" NavigateUrl="cart.aspx"></asp:MenuItem>
                                </asp:MenuItem>
                                <asp:MenuItem Text="About" Value="About" NavigateUrl="about.aspx"></asp:MenuItem>
                                <asp:MenuItem Text="Blog" Value="Blog" NavigateUrl="blog.aspx"></asp:MenuItem>
                                <asp:MenuItem Text="Contact" Value="Contact" NavigateUrl="contact.aspx"></asp:MenuItem>
                            </Items>
                            <StaticHoverStyle />
                            <StaticMenuItemStyle HorizontalPadding="30px" VerticalPadding="5px" />
                            <StaticSelectedStyle />
                        </asp:Menu>
                
                        <asp:Menu ID="Menu1" runat="server" Style="top: 30px; left: 980px; position: absolute" Font-Size="Large" Orientation="Horizontal" RenderingMode="Table" DisappearAfter="100" DynamicHorizontalOffset="2" Font-Names="Verdana" ForeColor="black" StaticSubMenuIndent="20%">
                            <DynamicHoverStyle />
                            <DynamicMenuItemStyle HorizontalPadding="10px" VerticalPadding="5px" BackColor="White" />
                            <DynamicMenuStyle BorderStyle="Ridge" BorderColor="Black" />
                            <DynamicSelectedStyle />
                            <Items>
                                <asp:MenuItem Text="Sign In" Value="Sign In" NavigateUrl="login.aspx"></asp:MenuItem>
                                <asp:MenuItem Text="/" Value="/"></asp:MenuItem>
                                <asp:MenuItem Text="Register" Value="Register">
                                    <asp:MenuItem Text="User" Value="User" NavigateUrl="register.aspx"></asp:MenuItem>
                                    <asp:MenuItem Text="Seller" Value="Seller" NavigateUrl="sellerRegister.aspx"></asp:MenuItem>
                                </asp:MenuItem>
                            </Items>
                            <StaticHoverStyle />
                            <StaticMenuItemStyle HorizontalPadding="5px" VerticalPadding="5px" />
                            <StaticSelectedStyle />
                        </asp:Menu>
                        
                        <asp:Button ID="btnLogout" runat="server" Text="Log Out" style="background-color: white; top: 35px; left: 1000px; position: absolute" Font-Size="Large" BorderStyle="None" Font-Underline="True" Visible="False" OnClick="btnLogout_Click"/>

                        <asp:ImageButton ID="searchIcon" runat="server" ImageUrl="~/img/icons/search.png" PostBackUrl="~/index.aspx" style="top: 30px; left: 1300px; position: absolute"/>
                        <asp:ImageButton ID="profileIcon" runat="server" ImageUrl="~/img/icons/man.png" PostBackUrl="~/profile.aspx" style="top: 30px; left: 1350px; position: absolute" Visible="False" />
                        <asp:ImageButton ID="cartIcon" runat="server" ImageUrl="~/img/icons/bag.png" PostBackUrl="~/cart.aspx" style="top: 30px; left: 1400px; position: absolute" Visible="False" />

                        <asp:Label ID="countItems" runat="server" style="z-index: 1; border-radius: 50px; text-align:center; top: 40px; left: 1420px; position: absolute" Font-Size="Large" BorderColor="Black" BorderStyle="Solid" Visible="False"></asp:Label>
                    </div>

                    <!--Header Info-->
                        <%-- 共用服務資訊列：免運、優惠券、線上支援與回饋圖示；各頁重複維護。 --%>
                        <div style="z-index: -1; top: 90px; width: 100%; height: 50px; position: absolute; background-color: lightslategray; left: 0px">
                            <asp:Image ID="FreeShipping" runat="server" AlternateText="Free Shipping" ImageUrl="~/img/icons/delivery.png" style=" left: 60px; top: 15px; position: absolute;"/>
                            <asp:Label ID="FreeShippingDetail" runat="server" Text="Free Shipping on orders over Rs.150* in India" style=" left: 110px; top: 15px; position: absolute;" ForeColor="White" Font-Size="Medium"></asp:Label>

                            <asp:Image ID="Voucher" runat="server" AlternateText="Voucher" ImageUrl="~/img/icons/voucher.png" style=" left: 630px; top: 10px; position: absolute;"/>
                            <asp:Label ID="VoucherDetail" runat="server" Text="20% Student Discount" style=" left: 680px; top: 15px; position: absolute;" ForeColor="White" Font-Size="Medium"></asp:Label>

                            <asp:Image ID="Discount" runat="server" AlternateText="Discount" ImageUrl="~/img/icons/sales.png" style=" left: 1100px; top: 10px; position: absolute;"/>
                            <asp:Label ID="DiscountDetail" runat="server" Text="30% off on dresses. Use code: 30OFF" style=" left: 1150px; top: 15px; position: absolute;" ForeColor="White" Font-Size="Medium"></asp:Label>
                        </div>
                    <!--Header Info-->
                </div>
            <!--Header-->

            <!--Body-->
                <%-- 主內容：resetPanel 輸入新密碼；invalidPanel 顯示權杖無效；donePanel 顯示重設完成。 --%>
                <div class="body">
                    <asp:Label ID="lblLoginMsg" runat="server" Font-Bold="True" Font-Italic="True" Font-Size="XX-Large" style="left: 660px; top: 10px; position: absolute" Text="Reset Password"></asp:Label>
                    <br />
                    <div style="text-align: center;">
                        <%-- 新密碼表單：規則與註冊頁相同（大小寫、數字、特殊字元，至少 8 碼）。 --%>
                        <asp:Panel ID="resetPanel" runat="server" BorderStyle="Solid" Height="320px" style="top: 50px; left: 500px; position: relative; text-align: center" Width="520px">
                            <br /><br /><br />
                            <asp:TextBox ID="txtPassword" runat="server" style="width: 300px; height: 30px" placeholder="Enter New Password...." TextMode="Password"></asp:TextBox>
                                <br />
                                <asp:RequiredFieldValidator ID="validatePasswordEmpty" runat="server" ErrorMessage="Password field cannot be left blank" ControlToValidate="txtPassword" ForeColor="Red" SetFocusOnError="True" Display="Dynamic"></asp:RequiredFieldValidator>
                                <asp:RegularExpressionValidator ID="validatePasswordExp" runat="server" ErrorMessage="Password Should contain 1 Upper and 1 Lower case, 1 Number and <br> 1 Special Character and should have 8 minimum characters" ControlToValidate="txtPassword" ForeColor="Red" SetFocusOnError="True" ValidationExpression="^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$" Display="Dynamic"></asp:RegularExpressionValidator>
                            <br /><br />
                            <asp:TextBox ID="txtConfirmPassword" runat="server" style="width: 300px; height: 30px" placeholder="Re-Enter New Password...." TextMode="Password"></asp:TextBox>
                                <br />
                                <asp:RequiredFieldValidator ID="validateConfirmEmpty" runat="server" ErrorMessage="Confirm Password field cannot be left blank" ControlToValidate="txtConfirmPassword" ForeColor="Red" SetFocusOnError="True" Display="Dynamic"></asp:RequiredFieldValidator>
                                <asp:CompareValidator ID="validateConfirm" runat="server" ErrorMessage="Password Mismatch" ForeColor="Red" SetFocusOnError="True" Display="Dynamic" ControlToCompare="txtPassword" ControlToValidate="txtConfirmPassword"></asp:CompareValidator>
                            <br /><br />
                            <asp:Button ID="btnReset" runat="server" Text="Reset Password" OnClick="btnReset_Click" Font-Size="Large"/>
                        </asp:Panel>

                        <%-- 權杖無效、已使用或已過期時顯示，提供重新申請的連結。 --%>
                        <asp:Panel ID="invalidPanel" runat="server" Visible="false" BorderStyle="Solid" Height="200px" style="top: 50px; left: 500px; position: relative; text-align: center" Width="520px">
                            <br /><br /><br />
                            <asp:Label ID="lblInvalid" runat="server" Text="This password reset link is invalid or has expired." Font-Size="Large" ForeColor="Red"></asp:Label>
                            <br /><br />
                            <asp:HyperLink ID="lnkForgot" runat="server" NavigateUrl="~/forgotpass.aspx" Text="Request a new link" Font-Size="Large"></asp:HyperLink>
                        </asp:Panel>

                        <%-- 重設成功時顯示，提供登入連結。 --%>
                        <asp:Panel ID="donePanel" runat="server" Visible="false" BorderStyle="Solid" Height="200px" style="top: 50px; left: 500px; position: relative; text-align: center" Width="520px">
                            <br /><br /><br />
                            <asp:Label ID="lblDone" runat="server" Text="Your password has been reset." Font-Size="Large" ForeColor="Green"></asp:Label>
                            <br /><br />
                            <asp:HyperLink ID="lnkLogin" runat="server" NavigateUrl="~/login.aspx" Text="Sign In" Font-Size="Large"></asp:HyperLink>
                        </asp:Panel>
                    </div>
                </div>
            <!--Body-->

            <!-- Footer Section Begin -->
                <%-- 共用頁尾：Contact Us、Payment Method 與 Information 區塊；未使用 Master Page，因此各頁各自複製。 --%>
                <div class="footer" style="text-align: center; background-color: #262626; left: 0px">
                    <div style="left: 220px; position: absolute; color: #FFFFFF;">
                        <h2>About us</h2>
                            <ul style="list-style: none">
                                <li>About Us</li>
                                <li>Community</li>
                                <li>Jobs</li>
                                <li>Shipping</li>
                                <li>Contact Us</li>
                            </ul>
                    </div>
                
                    <div style="left: 470px; position: absolute; color: #FFFFFF;">
                        <h2>Customer Care</h2>
                            <ul style="list-style: none">
                                <li>Search</li>
                                <li>Privacy Policy</li>
                                <li>2019 Lookbook</li>
                                <li>Shipping & Delivery</li>
                                <li>Gallery</li>
                            </ul>
                    </div>
                
                    <div style="left: 780px; position: absolute; color: #FFFFFF;">
                        <h2>Our Services</h2>
                            <ul style="list-style: none">
                                <li>Free Shipping</li>
                                <li>Free Returnes</li>
                                <li>Our Franchising</li>
                                <li>Terms and conditions</li>
                                <li>Privacy Policy</li>
                            </ul>
                    </div>
                
                    <div style="left: 1100px; position: absolute; color: #FFFFFF;">
                        <h2>Information</h2>
                            <ul style="list-style: none">
                                <li>Payment methods</li>
                                <li>Times and shipping costs</li>
                                <li>Product Returns</li>
                                <li>Shipping methods</li>
                                <li>Conformity of the products</li>
                            </ul>
                    </div>

                    <div style="bottom: 15px; left: 600px; position: absolute; color: #FFFFFF;">
                        <h3>
                            Copyright &copy;<script>document.write(new Date().getFullYear());</script> All rights reserved
                        </h3>
                    </div>
                </div>
            <!-- Footer Section End -->
        </div>
    </form>
</body>
</html>