using QuantaTrain.Infrastructure;

namespace QuantaTrain.IntegrationTests;

public sealed class CodexLocatorTests
{
    [Fact]
    public void StandaloneCandidatesPreferNewestRelease()
    {
        var root = Path.Combine(Path.GetTempPath(), $"quantatrain-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(
                Path.Combine(
                    root,
                    ".codex",
                    "packages",
                    "standalone",
                    "releases",
                    "0.99.0-x86_64-pc-windows-msvc"));
            Directory.CreateDirectory(
                Path.Combine(
                    root,
                    ".codex",
                    "packages",
                    "standalone",
                    "releases",
                    "0.145.0-x86_64-pc-windows-msvc"));

            var candidates = CodexLocator.GetStandaloneCandidates(root);

            Assert.Equal(2, candidates.Count);
            Assert.Contains("0.145.0-", candidates[0]);
            Assert.Contains("0.99.0-", candidates[1]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UnlaunchableCandidateDoesNotAbortDiscovery()
    {
        var root = Path.Combine(Path.GetTempPath(), $"quantatrain-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var candidate = Path.Combine(root, "codex.exe");
        try
        {
            await File.WriteAllTextAsync(candidate, "not an executable");

            var version = await CodexLocator.ProbeVersionAsync(
                candidate,
                CancellationToken.None);

            Assert.Null(version);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
