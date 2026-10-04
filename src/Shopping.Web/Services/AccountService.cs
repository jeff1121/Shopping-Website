using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shopping.Data;
using Shopping.Web.Security;

namespace Shopping.Web.Services;

/// <summary>註冊表單。</summary>
public sealed class RegistrationForm
{
    /// <summary>顯示名稱。</summary>
    public string? DisplayName { get; set; }

    /// <summary>Email。</summary>
    public string? Email { get; set; }

    /// <summary>使用者名稱。</summary>
    public string? UserName { get; set; }

    /// <summary>密碼。</summary>
    public string? Password { get; set; }

    /// <summary>確認密碼。</summary>
    public string? ConfirmPassword { get; set; }

    /// <summary>電話。</summary>
    public string? Phone { get; set; }

    /// <summary>生日。</summary>
    public string? BirthDate { get; set; }

    /// <summary>性別。</summary>
    public string? Gender { get; set; }

    /// <summary>國家。</summary>
    public string? Country { get; set; }

    /// <summary>州或省。</summary>
    public string? Region { get; set; }

    /// <summary>城市。</summary>
    public string? City { get; set; }

    /// <summary>地址。</summary>
    public string? Address { get; set; }
}

/// <summary>可修改的個人資料。</summary>
public sealed class ProfileForm
{
    /// <summary>電話。</summary>
    public string? Phone { get; set; }

    /// <summary>地址。</summary>
    public string? Address { get; set; }

    /// <summary>國家。</summary>
    public string? Country { get; set; }

    /// <summary>州或省。</summary>
    public string? Region { get; set; }

    /// <summary>城市。</summary>
    public string? City { get; set; }
}

/// <summary>註冊與個人資料。</summary>
public sealed class AccountService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ShoppingDbContext _db;
    private readonly TimeProvider _clock;

    /// <summary>建立服務。</summary>
    public AccountService(UserManager<ApplicationUser> users, ShoppingDbContext db, TimeProvider clock)
    {
        _users = users;
        _db = db;
        _clock = clock;
    }

    /// <summary>建立 Customer。失敗時回傳欄位與資源鍵。</summary>
    public async Task<IReadOnlyDictionary<string, string>> RegisterAsync(RegistrationForm form, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string>();
        if (!InputRules.TryDisplayName(form.DisplayName, out var displayName))
        {
            errors["DisplayName"] = string.IsNullOrWhiteSpace(form.DisplayName) ? "Required" : "DisplayNameInvalid";
        }

        if (!InputRules.TryEmail(form.Email, out var email))
        {
            errors["Email"] = string.IsNullOrWhiteSpace(form.Email) ? "Required" : "EmailInvalid";
        }

        if (!InputRules.IsUserName(form.UserName))
        {
            errors["UserName"] = string.IsNullOrWhiteSpace(form.UserName) ? "Required" : "UserNameInvalid";
        }

        if (string.IsNullOrEmpty(form.Password))
        {
            errors["Password"] = "Required";
        }
        else if (!InputRules.IsPassword(form.Password))
        {
            errors["Password"] = "PasswordInvalid";
        }

        if (string.IsNullOrEmpty(form.ConfirmPassword))
        {
            errors["ConfirmPassword"] = "Required";
        }
        else if (!string.Equals(form.Password, form.ConfirmPassword, StringComparison.Ordinal))
        {
            errors["ConfirmPassword"] = "ConfirmMismatch";
        }

        if (!InputRules.IsPhone(form.Phone))
        {
            errors["Phone"] = string.IsNullOrWhiteSpace(form.Phone) ? "Required" : "PhoneInvalid";
        }

        if (!InputRules.TryBirthDate(form.BirthDate, TaipeiClock.UtcToday(_clock), out var birth))
        {
            errors["BirthDate"] = string.IsNullOrWhiteSpace(form.BirthDate) ? "Required" : BirthError(form.BirthDate);
        }

        if (!InputRules.IsGender(form.Gender))
        {
            errors["Gender"] = string.IsNullOrWhiteSpace(form.Gender) ? "Required" : "GenderInvalid";
        }

        AddRequired(errors, "Country", form.Country, out var country);
        AddRequired(errors, "Region", form.Region, out var region);
        AddRequired(errors, "City", form.City, out var city);
        AddRequired(errors, "Address", form.Address, out var address);
        if (errors.Count > 0)
        {
            return errors;
        }

        if (await _users.FindByEmailAsync(email) is not null)
        {
            errors["Email"] = "EmailTaken";
        }

        if (await _users.FindByNameAsync(form.UserName!) is not null)
        {
            errors["UserName"] = "UserNameTaken";
        }

        if (await _db.Users.AnyAsync(user => user.PhoneNumber == form.Phone, cancellationToken))
        {
            errors["Phone"] = "PhoneTaken";
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var user = new ApplicationUser
        {
            UserName = form.UserName,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            PhoneNumber = form.Phone,
            DateOfBirth = birth,
            Gender = form.Gender!,
            Country = country,
            Region = region,
            City = city,
            Address = address,
        };
        var created = await _users.CreateAsync(user, form.Password!);
        if (!created.Succeeded)
        {
            errors["Password"] = "PasswordInvalid";
            return errors;
        }

        await _users.AddToRoleAsync(user, "Customer");
        return errors;
    }

    /// <summary>更新電話與地址。多送的欄位不在這個模型上。</summary>
    public async Task<IReadOnlyDictionary<string, string>> UpdateProfileAsync(string userId, ProfileForm form, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string>();
        if (!InputRules.IsPhone(form.Phone))
        {
            errors["Phone"] = string.IsNullOrWhiteSpace(form.Phone) ? "Required" : "PhoneInvalid";
        }

        AddRequired(errors, "Address", form.Address, out var address);
        AddRequired(errors, "Country", form.Country, out var country);
        AddRequired(errors, "Region", form.Region, out var region);
        AddRequired(errors, "City", form.City, out var city);
        if (errors.Count > 0)
        {
            return errors;
        }

        var user = await _users.FindByIdAsync(userId);
        if (user is null)
        {
            errors[""] = "Required";
            return errors;
        }

        var taken = await _db.Users.AnyAsync(item => item.PhoneNumber == form.Phone && item.Id != userId, cancellationToken);
        if (taken)
        {
            errors["Phone"] = "PhoneTaken";
            return errors;
        }

        user.PhoneNumber = form.Phone;
        user.Address = address;
        user.Country = country;
        user.Region = region;
        user.City = city;
        await _users.UpdateAsync(user);
        return errors;
    }

    private static string BirthError(string? value)
    {
        if (InputRules.TryBirthDate(value, DateOnly.MaxValue, out _))
        {
            return "BirthInFuture";
        }

        return "BirthInvalid";
    }

    private static void AddRequired(Dictionary<string, string> errors, string key, string? value, out string normalized)
    {
        if (!InputRules.TryRequired(value, key == "Address" ? 500 : 50, out normalized))
        {
            errors[key] = string.IsNullOrWhiteSpace(value) ? "Required" : "TooLong";
        }
    }
}
