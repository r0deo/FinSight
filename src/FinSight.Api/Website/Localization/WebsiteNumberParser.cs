using System.Globalization;

namespace FinSight.Api.Website.Localization;

public static class WebsiteNumberParser
{
    // Accept ASCII and Arabic-script digits and either unambiguous decimal mark.
    // Grouping separators are deliberately rejected: 1,234 can mean two values.
    public static bool TryDecimal(string? input, out decimal value)
    {
        value = default;
        return TryNormalize(input, true, out var normalized)
            && decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out value);
    }

    public static bool TryInt(string? input, out int value)
    {
        value = default;
        return TryNormalize(input, false, out var normalized)
            && int.TryParse(normalized, NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out value);
    }

    private static bool TryNormalize(string? input, bool allowDecimal, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var result = new char[input.Trim().Length];
        var source = input.Trim();
        var separators = 0;
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            result[i] = c switch
            {
                >= '٠' and <= '٩' => (char)('0' + c - '٠'),
                >= '۰' and <= '۹' => (char)('0' + c - '۰'),
                '٫' when allowDecimal => '.',
                _ => c
            };
            if (result[i] == '.') separators++;
            if (!(result[i] is >= '0' and <= '9' or '+' or '-' or '.')) return false;
        }
        if (separators > 1) return false;
        normalized = new string(result);
        return true;
    }
}
