using System.Globalization;
using System.Text.Json;
using QuantaTrain.Core;

namespace QuantaTrain.App;

internal sealed class LocalizationService
{
    private const string FallbackLocale = "en-US";
    private readonly string _localesDirectory;
    private Dictionary<string, string> _strings = new(StringComparer.Ordinal);

    public LocalizationService(string localesDirectory)
    {
        _localesDirectory = localesDirectory;
    }

    public string CurrentLocale { get; private set; } = FallbackLocale;

    public void Load(LanguageSettings settings)
    {
        var requested = settings.Mode == "auto"
            ? NormalizeLocale(CultureInfo.CurrentUICulture.Name)
            : NormalizeLocale(settings.Locale);
        CurrentLocale = requested;
        _strings = LoadFile(FallbackLocale);
        if (!string.Equals(requested, FallbackLocale, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var pair in LoadFile(requested))
            {
                _strings[pair.Key] = pair.Value;
            }
        }
    }

    public string Text(string key, params object?[] arguments)
    {
        var value = _strings.GetValueOrDefault(key, key);
        return arguments.Length == 0
            ? value
            : string.Format(CultureInfo.CurrentCulture, value, arguments);
    }

    private Dictionary<string, string> LoadFile(string locale)
    {
        var path = Path.Combine(_localesDirectory, $"{locale}.json");
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static string NormalizeLocale(string locale)
    {
        if (string.Equals(locale, "ja-JP", StringComparison.OrdinalIgnoreCase) ||
            locale.StartsWith("ja-", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(locale, "ja", StringComparison.OrdinalIgnoreCase))
        {
            return "ja-JP";
        }
        return FallbackLocale;
    }
}
