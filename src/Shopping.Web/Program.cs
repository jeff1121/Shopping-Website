namespace Shopping.Web;

/// <summary>網站進入點。</summary>
public partial class Program
{
    private static CancellationTokenSource? _exit;

    /// <summary>啟動網站。SHOP_EXIT_AFTER_START=1 時在啟動後結束，供測試覆蓋進入點。</summary>
    public static void Main(string[] args)
    {
        AppBootstrap.Build(args).RunAsync(ExitToken()).GetAwaiter().GetResult();
    }

    /// <summary>正式環境不取消。測試設定 SHOP_EXIT_AFTER_START=1 時兩秒後取消。</summary>
    public static CancellationToken ExitToken()
    {
        var raw = Environment.GetEnvironmentVariable("SHOP_EXIT_AFTER_START");
        if (!string.Equals(raw, "1", StringComparison.Ordinal))
        {
            return CancellationToken.None;
        }

        _exit = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        return _exit.Token;
    }
}
