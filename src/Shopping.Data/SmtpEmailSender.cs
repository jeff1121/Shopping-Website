using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Shopping.Data;

/// <summary>透過 SMTP 寄信。</summary>
public sealed class SmtpEmailSender
{
    /// <summary>寄出一封純文字信。</summary>
    public async Task SendAsync(string host, int port, string user, string password, string from, string to, string subject, string body, SecureSocketOptions options, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(host, port, options, cancellationToken);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("連線失敗", exception);
        }

        try
        {
            await client.AuthenticateAsync(user, password, cancellationToken);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("認證失敗", exception);
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
