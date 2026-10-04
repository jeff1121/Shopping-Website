using Microsoft.AspNetCore.Identity;
using Shopping.Data;

namespace Shopping.Web.Services;

/// <summary>管理者找到的會員。</summary>
public sealed class MemberLookup
{
    /// <summary>建立結果。</summary>
    public MemberLookup(string id, string userName, string email, bool isSeller)
    {
        Id = id;
        UserName = userName;
        Email = email;
        IsSeller = isSeller;
    }

    /// <summary>識別碼。</summary>
    public string Id { get; }

    /// <summary>使用者名稱。</summary>
    public string UserName { get; }

    /// <summary>Email。</summary>
    public string Email { get; }

    /// <summary>是否為賣家。</summary>
    public bool IsSeller { get; }
}

/// <summary>授予與收回賣家。</summary>
public sealed class OperatorService
{
    private readonly UserManager<ApplicationUser> _users;

    /// <summary>建立服務。</summary>
    public OperatorService(UserManager<ApplicationUser> users)
    {
        _users = users;
    }

    /// <summary>用使用者名稱或 Email 尋找。找不到回傳 null。</summary>
    public async Task<MemberLookup?> FindAsync(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var user = await _users.FindByNameAsync(query.Trim());
        if (user is null && InputRules.TryEmail(query, out var email))
        {
            user = await _users.FindByEmailAsync(email);
        }

        if (user is null)
        {
            return null;
        }

        var seller = await _users.IsInRoleAsync(user, "Seller");
        return new MemberLookup(user.Id, user.UserName!, user.Email!, seller);
    }

    /// <summary>授予或收回 Seller。</summary>
    public async Task<bool> ChangeSellerAsync(string targetId, bool grant)
    {
        var user = await _users.FindByIdAsync(targetId);
        if (user is null)
        {
            return false;
        }

        if (grant)
        {
            if (!await _users.IsInRoleAsync(user, "Seller"))
            {
                await _users.AddToRoleAsync(user, "Seller");
            }

            return true;
        }

        if (await _users.IsInRoleAsync(user, "Seller"))
        {
            await _users.RemoveFromRoleAsync(user, "Seller");
        }

        return true;
    }

    /// <summary>不授予、不收回 Operator，也不能收回自己的 Operator。</summary>
    public Task<bool> ChangeOperatorAsync(string actorId, string targetId, bool grant)
    {
        _ = actorId;
        _ = targetId;
        _ = grant;
        return Task.FromResult(false);
    }
}
