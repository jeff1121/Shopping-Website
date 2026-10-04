using Shopping.Data;

namespace Shopping.Web.Services;

/// <summary>聯絡我們。</summary>
public sealed class ContactService
{
    private readonly ShoppingDbContext _db;
    private readonly TimeProvider _clock;

    /// <summary>建立服務。</summary>
    public ContactService(ShoppingDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>寫入一則留言。</summary>
    public async Task<IReadOnlyDictionary<string, string>> SendAsync(string? name, string? email, string? message, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string>();
        if (!InputRules.TryRequired(name, 50, out var normalizedName))
        {
            errors["Name"] = string.IsNullOrWhiteSpace(name) ? "Required" : "TooLong";
        }

        if (!InputRules.TryEmail(email, out var normalizedEmail))
        {
            errors["Email"] = string.IsNullOrWhiteSpace(email) ? "Required" : "EmailInvalid";
        }

        if (!InputRules.TryRequired(message, 500, out var normalizedMessage))
        {
            errors["Message"] = string.IsNullOrWhiteSpace(message) ? "Required" : "TooLong";
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        _db.ContactMessages.Add(new ContactMessage
        {
            Name = normalizedName,
            Email = normalizedEmail,
            Message = normalizedMessage,
            CreatedAt = _clock.GetUtcNow(),
        });
        await _db.SaveChangesAsync(cancellationToken);
        return errors;
    }
}
