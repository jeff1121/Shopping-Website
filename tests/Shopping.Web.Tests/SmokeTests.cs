namespace Shopping.Web.Tests;

/// <summary>確認測試主機可以開啟目錄。</summary>
public sealed class SmokeTests : IClassFixture<ShopApp>
{
    private readonly ShopApp _app;

    /// <summary>建立測試。</summary>
    public SmokeTests(ShopApp app)
    {
        _app = app;
    }

    /// <summary>首頁包含示範商品。</summary>
    [Fact(DisplayName = "FR-10 catalog shows Demo Laptop")]
    public async Task HomeShowsDemoLaptop()
    {
        var client = _app.CreateNoRedirectClient();
        var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Demo Laptop", body, StringComparison.Ordinal);
        Assert.DoesNotContain("sellerRegister", body, StringComparison.OrdinalIgnoreCase);
    }
}
