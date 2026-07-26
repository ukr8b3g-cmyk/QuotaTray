using System.Text.Json;

namespace QuantaTrain.App.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void EveryLocaleHasTheEnglishKeySetAndMatchingPlaceholders()
    {
        var source = FindLocalesDirectory();
        var files = new[]
        {
            Path.Combine(source, "en-US.json"),
            Path.Combine(source, "ja-JP.json"),
        };
        Assert.All(
            files,
            path => Assert.True(File.Exists(path), $"Missing locale: {path}"));
        var english = Read(Path.Combine(source, "en-US.json"));

        foreach (var file in files)
        {
            var locale = Read(file);
            Assert.Equal(
                english.Keys.Order(StringComparer.Ordinal),
                locale.Keys.Order(StringComparer.Ordinal));
            foreach (var key in english.Keys)
            {
                Assert.Equal(
                    PlaceholderCount(english[key]),
                    PlaceholderCount(locale[key]));
            }
        }
    }

    private static string FindLocalesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "QuantaTrain.App",
                "locales");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Source locales directory was not found.");
    }

    private static Dictionary<string, string> Read(string path) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
        ?? throw new InvalidDataException(path);

    private static int PlaceholderCount(string value) =>
        System.Text.RegularExpressions.Regex.Matches(value, @"\{\d+\}").Count;
}
