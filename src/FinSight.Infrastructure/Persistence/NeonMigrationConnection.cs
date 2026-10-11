using Npgsql;

namespace FinSight.Infrastructure.Persistence;

// Explicit migration-tool input only. Does not change application/development configuration.
public static class NeonMigrationConnection
{
    public static string FromDirectUrl(string value)
    {
        const string invalid = "Enter a complete direct Neon PostgreSQL URL, without a shell command or surrounding quotes.";
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(invalid);
        value = value.Trim();
        for (var i = 0; i < value.Length; i++)
            if (value[i] == '%' && (i + 2 >= value.Length || !Uri.IsHexDigit(value[i + 1]) || !Uri.IsHexDigit(value[i + 2])))
                throw new ArgumentException(invalid);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("postgres" or "postgresql") || uri.Fragment.Length > 0
            || !uri.Host.EndsWith(".neon.tech", StringComparison.OrdinalIgnoreCase)
            || uri.Port is not (-1 or 5432))
            throw new ArgumentException(invalid);
        if (uri.Host.Split('.')[0].Contains("-pooler", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Use DATABASE_URL, not DATABASE_URL_POOLED. Migrations require the direct endpoint.");

        var userInfo = uri.GetComponents(UriComponents.UserInfo, UriFormat.UriEscaped);
        var separator = userInfo.IndexOf(':');
        var path = uri.AbsolutePath;
        if (separator < 1 || separator == userInfo.Length - 1 || path.Length < 2 || path[1..].Contains('/'))
            throw new ArgumentException(invalid);
        var username = Uri.UnescapeDataString(userInfo[..separator]);
        var password = Uri.UnescapeDataString(userInfo[(separator + 1)..]);
        var database = Uri.UnescapeDataString(path[1..]);
        if (string.IsNullOrWhiteSpace(database) || username.Any(char.IsControl)
            || password.Any(char.IsControl) || database.Any(char.IsControl)) throw new ArgumentException(invalid);

        var timeout = 30;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = parameter.Split('=', 2);
            var name = Uri.UnescapeDataString(pieces[0]);
            var setting = pieces.Length == 2 ? Uri.UnescapeDataString(pieces[1]) : "";
            if (!seen.Add(name)) throw new ArgumentException("Duplicate URL options are not accepted.");
            switch (name.ToLowerInvariant())
            {
                case "sslmode" when setting is "require" or "verify-full":
                case "channel_binding" when setting is "require" or "prefer":
                    break; // Always upgraded to VerifyFull + required channel binding below.
                case "connect_timeout" when int.TryParse(setting, out var seconds) && seconds is >= 1 and <= 120:
                    timeout = seconds;
                    break;
                default:
                    throw new ArgumentException("Unsupported or unsafe URL option. Use Neon's standard direct URL with sslmode=require and channel_binding=require.");
            }
        }

        // The builder correctly quotes decoded semicolons, equals signs and quotes.
        // Decode credentials once; literal '+' must not become a space.
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host, Port = 5432, Database = database, Username = username, Password = password,
            SslMode = SslMode.VerifyFull, ChannelBinding = ChannelBinding.Require,
            GssEncryptionMode = GssEncryptionMode.Disable,
            Pooling = false, Timeout = timeout, CommandTimeout = 120,
            IncludeErrorDetail = false, PersistSecurityInfo = false
        }.ConnectionString;
    }
}
