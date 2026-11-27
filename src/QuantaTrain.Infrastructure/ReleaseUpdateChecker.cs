using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuantaTrain.Infrastructure;

public sealed class ReleaseUpdateChecker : IDisposable
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/ukr8b3g-cmyk/QuotaTray/releases/latest";

    private readonly string _cachePath;
    private readonly Version _installed;
    private readonly HttpClient _http;
    private CheckState? _state;

    private sealed record CheckState(
        DateTimeOffset? LastAttemptUtc = null,
        Version? Latest = null,
        Version? LastNotified = null);

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string? Tag,
        [property: JsonPropertyName("draft")] bool? Draft,
        [property: JsonPropertyName("prerelease")] bool? Prerelease);

    public ReleaseUpdateChecker(string cachePath, Version installed)
        : this(cachePath, installed, new HttpClientHandler
        {
            UseCookies = false,
            AllowAutoRedirect = false,
        })
    {
    }

    internal ReleaseUpdateChecker(string cachePath, Version installed, HttpMessageHandler handler)
    {
        _cachePath = cachePath;
        _installed = installed;
        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(12),
            MaxResponseContentBufferSize = 1024 * 1024,
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("QuantaTray");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public static string ReleasePage(Version version) =>
        $"https://github.com/ukr8b3g-cmyk/QuotaTray/releases/tag/v{version}";

    // The tray serializes checks, including changes to the notification preference.
    public async Task<(Version? Available, bool Announce)> CheckAsync(
        DateTimeOffset now, CancellationToken token)
    {
        _state ??= await AtomicJsonFile.ReadAsync<CheckState>(_cachePath, token)
            ?? new CheckState();
        var latest = _state.Latest;
        if (_state.LastAttemptUtc is not { } last || now < last || now - last >= TimeSpan.FromDays(1))
        {
            // Persist attempts even when offline so restarting does not hammer GitHub.
            _state = _state with { LastAttemptUtc = now };
            await AtomicJsonFile.WriteAsync(_cachePath, _state, token);
            try
            {
                using var response = await _http.GetAsync(LatestReleaseApi, token);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    latest = null;
                }
                else
                {
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync(token);
                    var release = JsonSerializer.Deserialize<GitHubRelease>(json);
                    latest = ParseStableVersion(release);
                }
            }
            catch (Exception error) when (error is HttpRequestException or JsonException ||
                error is OperationCanceledException && !token.IsCancellationRequested)
            {
                // A temporary failure keeps the last confirmed release accessible.
            }
        }

        token.ThrowIfCancellationRequested();
        var available = latest > _installed ? latest : null;
        var announce = available is not null && available > _state.LastNotified;
        var next = _state with { Latest = latest, LastNotified = announce ? available : _state.LastNotified };
        if (next != _state)
        {
            await AtomicJsonFile.WriteAsync(_cachePath, next, token);
            _state = next;
        }
        return (available, announce);
    }

    private static Version? ParseStableVersion(GitHubRelease? release)
    {
        if (release is not { Draft: false, Prerelease: false, Tag: { } tag } ||
            !tag.StartsWith('v') || !Version.TryParse(tag.AsSpan(1), out var version) ||
            version.Build < 0 || version.Revision >= 0 || tag != $"v{version}")
        {
            return null;
        }
        return version;
    }

    public void Dispose() => _http.Dispose();
}
