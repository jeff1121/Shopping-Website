using Microsoft.AspNetCore.Identity;
using Shopping.Data;

namespace Shopping.Web.Services;

/// <summary>使用者名稱或 Email 登入。</summary>
public sealed class LoginService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;

    /// <summary>建立服務。</summary>
    public LoginService(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
    {
        _users = users;
        _signIn = signIn;
    }

    /// <summary>登入。失敗時不說明帳號是否存在。</summary>
    public async Task<SignInResult> LoginAsync(string? account, string? password)
    {
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrEmpty(password))
        {
            return SignInResult.Failed;
        }

        var user = await _users.FindByNameAsync(account.Trim());
        if (user is null && InputRules.TryEmail(account, out var email))
        {
            user = await _users.FindByEmailAsync(email);
        }

        if (user is null)
        {
            return SignInResult.Failed;
        }

        return await _signIn.PasswordSignInAsync(user.UserName!, password, false, lockoutOnFailure: true);
    }
}
