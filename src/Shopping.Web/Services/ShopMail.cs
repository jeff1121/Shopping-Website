using MailKit.Security;
using Shopping.Data;

namespace Shopping.Web.Services;

/// <summary>一封信。</summary>
public sealed class MailItem
{
    /// <summary>建立信件。</summary>
    public MailItem(string to, string subject, string body)
    {
        To = to;
        Subject = subject;
        Body = body;
    }

    /// <summary>收件者。</summary>
    public string To { get; }

    /// <summary>主旨。</summary>
    public string Subject { get; }

    /// <summary>本文。</summary>
    public string Body { get; }
}

/// <summary>寄信。</summary>
public interface IShopMailer
{
    /// <summary>寄出純文字。</summary>
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}

/// <summary>測試與開發用的信箱。</summary>
public sealed class MemoryMailbox : IShopMailer
{
    /// <summary>已收到的信。</summary>
    public List<MailItem> Messages { get; } = [];

    /// <summary>為 true 時先收下內容再拋出。</summary>
    public bool ThrowOnSend { get; set; }

    /// <summary>收下或拋出。</summary>
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        Messages.Add(new MailItem(to, subject, body));
        if (ThrowOnSend)
        {
            throw new InvalidOperationException("寄信失敗");
        }

        return Task.CompletedTask;
    }
}

/// <summary>正式環境的 SMTP 寄信。</summary>
public sealed class SmtpShopMailer : IShopMailer
{
    private readonly AppSettings _settings;
    private readonly SmtpEmailSender _sender = new();

    /// <summary>建立寄信器。</summary>
    public SmtpShopMailer(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>以 STARTTLS 寄出。</summary>
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        return _sender.SendAsync(_settings.SmtpHost, _settings.SmtpPort, _settings.SmtpUser, _settings.SmtpPassword, _settings.SmtpFrom, to, subject, body, SecureSocketOptions.StartTls, cancellationToken);
    }
}
