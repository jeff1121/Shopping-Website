using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shopping.Data;

namespace Shopping.Web.Services;

/// <summary>忘記密碼的畫面結果。</summary>
public enum ResetAsk
{
    /// <summary>不論帳號是否存在都顯示同一句。</summary>
    Generic,

    /// <summary>寄信失敗。</summary>
    Unavailable,
}

/// <summary>寄信失敗的紀錄。訊息不含 Email。</summary>
public interface IResetLog
{
    /// <summary>記下寄信失敗。</summary>
    void MailFailed();
}

/// <summary>寫入記錄器。</summary>
public sealed class ResetLog : IResetLog
{
    private readonly ILogger<ResetLog> _logger;

    /// <summary>建立紀錄器。</summary>
    public ResetLog(ILogger<ResetLog> logger)
    {
        _logger = logger;
    }

    /// <summary>記下寄信失敗。</summary>
    public void MailFailed()
    {
        _logger.LogWarning("重設密碼信寄出失敗");
    }
}

/// <summary>重設密碼信與配額。</summary>
public sealed class PasswordResetService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ShoppingDbContext _db;
    private readonly IShopMailer _mail;
    private readonly IResetLog _log;
    private readonly TimeProvider _clock;

    /// <summary>建立服務。</summary>
    public PasswordResetService(UserManager<ApplicationUser> users, ShoppingDbContext db, IShopMailer mail, IResetLog log, TimeProvider clock)
    {
        _users = users;
        _db = db;
        _mail = mail;
        _log = log;
        _clock = clock;
    }

    /// <summary>申請重設。成功與否對畫面使用同一句，寄信失敗除外。</summary>
    public async Task<ResetAsk> RequestAsync(string? account, string linkBase, CancellationToken cancellationToken)
    {
        var user = await FindAsync(account);
        if (user is null)
        {
            return ResetAsk.Generic;
        }

        var now = _clock.GetUtcNow();
        var quota = await _db.PasswordResetQuotas.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (quota is not null && now - quota.WindowStart < TimeSpan.FromHours(1) && quota.Count >= 5)
        {
            return ResetAsk.Generic;
        }

        await _users.UpdateSecurityStampAsync(user);
        user = (await _users.FindByIdAsync(user.Id))!;
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var link = linkBase + "?user=" + Uri.EscapeDataString(user.Id) + "&token=" + Uri.EscapeDataString(token);
        try
        {
            await _mail.SendAsync(user.Email!, "重設密碼", link, cancellationToken);
        }
        catch (Exception)
        {
            await _users.UpdateSecurityStampAsync(user);
            _log.MailFailed();
            return ResetAsk.Unavailable;
        }

        if (quota is null || now - quota.WindowStart >= TimeSpan.FromHours(1))
        {
            if (quota is null)
            {
                _db.PasswordResetQuotas.Add(new PasswordResetQuota { UserId = user.Id, WindowStart = now, Count = 1 });
            }
            else
            {
                quota.WindowStart = now;
                quota.Count = 1;
            }
        }
        else
        {
            quota.Count += 1;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ResetAsk.Generic;
    }

    /// <summary>用權杖設定新密碼。</summary>
    public async Task<IReadOnlyDictionary<string, string>> ResetAsync(string? userId, string? token, string? password, string? confirm, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(password))
        {
            errors["Password"] = "Required";
        }
        else if (!InputRules.IsPassword(password))
        {
            errors["Password"] = "PasswordInvalid";
        }

        if (string.IsNullOrEmpty(confirm))
        {
            errors["ConfirmPassword"] = "Required";
        }
        else if (!string.Equals(password, confirm, StringComparison.Ordinal))
        {
            errors["ConfirmPassword"] = "ConfirmMismatch";
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            errors[""] = "ResetFailed";
            return errors;
        }

        var user = await _users.FindByIdAsync(userId);
        if (user is null)
        {
            errors[""] = "ResetFailed";
            return errors;
        }

        var result = await _users.ResetPasswordAsync(user, token, password!);
        if (!result.Succeeded)
        {
            errors[""] = "ResetFailed";
            return errors;
        }

        return errors;
    }

    private async Task<ApplicationUser?> FindAsync(string? account)
    {
        if (string.IsNullOrWhiteSpace(account))
        {
            return null;
        }

        var byName = await _users.FindByNameAsync(account.Trim());
        if (byName is not null)
        {
            return byName;
        }

        if (!InputRules.TryEmail(account, out var email))
        {
            return null;
        }

        return await _users.FindByEmailAsync(email);
    }
}
