using System.Net;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.IntegrationTests;

public sealed class ReleaseUpdateCheckerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly DateTimeOffset _start = DateTimeOffset.UtcNow;
    private readonly Version _installed = new(0, 2, 9);
    private string Cache => Path.Combine(_dir, "release-check.json");

    [Theory]
    [InlineData("v0.2.10", false, false, true)]
    [InlineData("v0.2.9", false, false, false)]
    [InlineData("v0.2.8", false, false, false)]
    [InlineData("v0.3.0", true, false, false)]
    [InlineData("v0.3.0", false, true, false)]
    [InlineData("v0.3.0-rc.1", false, false, false)]
    [InlineData("https://example.com", false, false, false)]
    public async Task OnlyNewStableTagsAnnounce(string tag, bool draft, bool pre, bool announce)
    {
        var handler = new ReleaseHandler { Tag = tag, Draft = draft, Prerelease = pre };
        using var checker = new ReleaseUpdateChecker(Cache, _installed, handler);
        var result = await checker.CheckAsync(_start, default);
        Assert.Equal(announce, result.Announce);
        Assert.Equal(announce, result.Available is not null);
        Assert.Equal(1, handler.Requests);
        if (result.Available is { } version)
        {
            Assert.Equal($"https://github.com/ukr8b3g-cmyk/QuotaTray/releases/tag/{tag}",
                ReleaseUpdateChecker.ReleasePage(version));
        }
    }

    [Fact]
    public async Task DailyCheckAndNoticeSurviveRestartAndVersionUpgrade()
    {
        var old = new ReleaseHandler { Tag = "v0.2.10" };
        using (var checker = new ReleaseUpdateChecker(Cache, _installed, old))
        {
            Assert.True((await checker.CheckAsync(_start, default)).Announce);
            Assert.False((await checker.CheckAsync(_start.AddHours(12), default)).Announce);
            Assert.Equal(1, old.Requests);
        }
        var api = new ReleaseHandler { Tag = "v0.2.10" };
        using (var checker = new ReleaseUpdateChecker(Cache, _installed, api))
        {
            var cached = await checker.CheckAsync(_start.AddHours(23), default);
            Assert.Equal(new Version(0, 2, 10), cached.Available);
            Assert.False(cached.Announce);
            Assert.Equal(0, api.Requests);
            Assert.False((await checker.CheckAsync(_start.AddHours(24), default)).Announce);
            Assert.Equal(1, api.Requests);
            api.Tag = "v0.2.11";
            Assert.True((await checker.CheckAsync(_start.AddDays(2), default)).Announce);
        }
        using var upgraded = new ReleaseUpdateChecker(Cache, new Version(0, 2, 11), new ReleaseHandler());
        Assert.Equal((null, false), await upgraded.CheckAsync(_start.AddDays(2), default));
    }

    [Fact]
    public async Task FailedCheckRetainsKnownReleaseAndWaitsUntilNextDay()
    {
        var api = new ReleaseHandler { Tag = "v0.2.10" };
        using var checker = new ReleaseUpdateChecker(Cache, _installed, api);
        await checker.CheckAsync(_start, default);
        api.Status = HttpStatusCode.ServiceUnavailable;
        var failed = await checker.CheckAsync(_start.AddDays(1), default);
        Assert.Equal((new Version(0, 2, 10), false), failed);
        api.Status = HttpStatusCode.NotFound;
        Assert.Equal(failed, await checker.CheckAsync(_start.AddHours(47), default));
        Assert.Equal(2, api.Requests);
        Assert.Equal((null, false), await checker.CheckAsync(_start.AddDays(2), default));
    }

    [Fact]
    public async Task IncompleteReleaseMetadataDoesNotAnnounce()
    {
        var api = new ReleaseHandler { Content = """{"tag_name":"v9.0.0"}""" };
        using var checker = new ReleaseUpdateChecker(Cache, _installed, api);
        Assert.Equal((null, false), await checker.CheckAsync(_start, default));
    }

    private sealed class ReleaseHandler : HttpMessageHandler
    {
        public string Tag { get; set; } = "v0.2.10";
        public bool Draft { get; init; }
        public bool Prerelease { get; init; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string? Content { get; init; }
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://api.github.com/repos/ukr8b3g-cmyk/QuotaTray/releases/latest",
                request.RequestUri?.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            Assert.Null(request.Content);
            var body = Content ?? System.Text.Json.JsonSerializer.Serialize(new
            {
                tag_name = Tag, draft = Draft, prerelease = Prerelease,
                html_url = "https://example.com/ignored", assets = new[] { "unused" },
            });
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(body) });
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
