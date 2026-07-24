using System.Text.RegularExpressions;

namespace QuantaTrain.Infrastructure;

public static partial class Redaction
{
    public static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var redacted = UrlRegex().Replace(value, "[URL REDACTED]");
        redacted = EmailRegex().Replace(redacted, "[EMAIL REDACTED]");
        return TokenRegex().Replace(redacted, "$1[REDACTED]");
    }

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"\b[\w.%+-]+@[\w.-]+\.[A-Za-z]{2,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?i)\b(token|cookie|authorization|authUrl)\b\s*[:=]\s*\S+")]
    private static partial Regex TokenRegex();
}
