using System.Globalization;
using System.Net.Mail;

namespace Shopping.Data;

/// <summary>註冊、商品與聯絡表單的格式規則。</summary>
public static class InputRules
{
    /// <summary>顯示名稱去掉前後空白後是否只含字母與空格，且長度 1 到 50。</summary>
    public static bool TryDisplayName(string? value, out string normalized)
    {
        normalized = "";
        if (value is null)
        {
            return false;
        }

        var trimmed = value.Trim();
        if (!IsDisplayName(trimmed))
        {
            return false;
        }

        normalized = trimmed;
        return true;
    }

    /// <summary>使用者名稱是否為 1 到 20 碼英數。</summary>
    public static bool IsUserName(string? value)
    {
        if (value is null)
        {
            return false;
        }

        return IsUserNameCore(value);
    }

    /// <summary>Email 去除前後空白後是否可被接受。</summary>
    public static bool TryEmail(string? value, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 50)
        {
            return false;
        }

        var at = 0;
        foreach (var character in trimmed)
        {
            if (character == '@')
            {
                at++;
            }
        }

        if (at != 1)
        {
            return false;
        }

        try
        {
            var parsed = new MailAddress(trimmed);
            if (!string.Equals(parsed.Address, trimmed, StringComparison.Ordinal))
            {
                return false;
            }
        }
        catch (FormatException)
        {
            return false;
        }

        normalized = trimmed;
        return true;
    }

    /// <summary>密碼是否符合長度、字元集與四類字元。</summary>
    public static bool IsPassword(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 8)
        {
            return false;
        }

        var upper = false;
        var lower = false;
        var digit = false;
        var special = false;
        foreach (var character in value)
        {
            if (character is >= 'A' and <= 'Z')
            {
                upper = true;
            }
            else if (character is >= 'a' and <= 'z')
            {
                lower = true;
            }
            else if (character is >= '0' and <= '9')
            {
                digit = true;
            }
            else if ("@$!%*?&".Contains(character))
            {
                special = true;
            }
            else
            {
                return false;
            }
        }

        return upper && lower && digit && special;
    }

    /// <summary>電話是否恰好 10 位數字。</summary>
    public static bool IsPhone(string? value)
    {
        if (value is null)
        {
            return false;
        }

        return IsPhoneCore(value);
    }

    /// <summary>性別是否為 Male、Female 或 Others。</summary>
    public static bool IsGender(string? value)
    {
        if (value is null)
        {
            return false;
        }

        return value == "Male" || value == "Female" || value == "Others";
    }

    /// <summary>生日是否可解析且不晚於今天（UTC）。</summary>
    public static bool TryBirthDate(string? value, DateOnly today, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return false;
        }

        if (date > today)
        {
            return false;
        }

        return true;
    }

    /// <summary>必填文字是否在去掉空白後介於 1 與 max。</summary>
    public static bool TryRequired(string? value, int max, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > max)
        {
            return false;
        }

        normalized = trimmed;
        return true;
    }

    /// <summary>把價格文字轉成 decimal。</summary>
    public static bool TryPrice(string? text, out decimal price)
    {
        price = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out price))
        {
            return false;
        }

        return IsPrice(price);
    }

    /// <summary>庫存文字必須是大於等於 0 的整數。</summary>
    public static bool TryStock(string? text, out int stock)
    {
        stock = 0;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out stock))
        {
            return false;
        }

        return stock >= 0;
    }

    /// <summary>價格是否大於等於 0 且最多兩位小數。</summary>
    public static bool IsPrice(decimal price)
    {
        if (price < 0)
        {
            return false;
        }

        return decimal.Round(price, 2, MidpointRounding.AwayFromZero) == price;
    }

    /// <summary>關鍵字可空，最長 500。</summary>
    public static bool IsKeywords(string? value)
    {
        if (value is null)
        {
            return true;
        }

        return value.Length <= 500;
    }

    /// <summary>商品名稱長度 1 到 50。</summary>
    public static bool TryProductName(string? value, out string normalized)
    {
        return TryRequired(value, 50, out normalized);
    }

    private static bool IsDisplayName(string value)
    {
        if (value.Length < 1)
        {
            return false;
        }

        if (value.Length > 50)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character == ' ')
            {
                continue;
            }

            if (!char.IsLetter(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsUserNameCore(string value)
    {
        if (value.Length < 1)
        {
            return false;
        }

        if (value.Length > 20)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is >= 'a' and <= 'z')
            {
                continue;
            }

            if (character is >= 'A' and <= 'Z')
            {
                continue;
            }

            if (character is >= '0' and <= '9')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsPhoneCore(string value)
    {
        if (value.Length != 10)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character < '0')
            {
                return false;
            }

            if (character > '9')
            {
                return false;
            }
        }

        return true;
    }
}
