using System.ComponentModel;
using System.Diagnostics;

namespace QuantaTrain.Infrastructure;

public sealed record CodexInstallation(string ExecutablePath, string Version);

public static class CodexLocator
{
    public static async Task<CodexInstallation?> LocateAsync(
        string? explicitPath,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in GetCandidates(explicitPath).Distinct(
                     StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            var version = await ProbeVersionAsync(candidate, cancellationToken)
                .ConfigureAwait(false);
            if (version is not null)
            {
                return new CodexInstallation(candidate, version);
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCandidates(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            yield return Path.GetFullPath(explicitPath);
        }

        foreach (var variableName in new[]
                 {
                     "QUANTATRAY_CODEX_PATH",
                     "QUANTATRAIN_CODEX_PATH",
                 })
        {
            var environmentPath = Environment.GetEnvironmentVariable(variableName);
            if (!string.IsNullOrWhiteSpace(environmentPath))
            {
                yield return Path.GetFullPath(environmentPath);
            }
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var directory in path.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    yield return Path.Combine(directory.Trim(), "codex.exe");
                }
            }
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        foreach (var candidate in GetStandaloneCandidates(userProfile))
        {
            yield return candidate;
        }
        yield return Path.Combine(userProfile, ".codex", "bin", "codex.exe");
        yield return Path.Combine(localAppData, "Programs", "Codex", "codex.exe");
    }

    internal static IReadOnlyList<string> GetStandaloneCandidates(string userProfile)
    {
        var releases = Path.Combine(
            userProfile,
            ".codex",
            "packages",
            "standalone",
            "releases");
        string[] directories;
        try
        {
            directories = Directory.Exists(releases)
                ? Directory.GetDirectories(releases)
                : [];
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException)
        {
            return [];
        }

        return directories
            .Select(directory => new
            {
                Directory = directory,
                Version = ParseReleaseVersion(Path.GetFileName(directory)),
            })
            .OrderByDescending(item => item.Version ?? new Version())
            .ThenByDescending(item => item.Directory, StringComparer.OrdinalIgnoreCase)
            .Select(item => Path.Combine(item.Directory, "bin", "codex.exe"))
            .ToArray();
    }

    private static Version? ParseReleaseVersion(string releaseDirectory)
    {
        var separator = releaseDirectory.IndexOf('-');
        var versionText = separator >= 0
            ? releaseDirectory[..separator]
            : releaseDirectory;
        return Version.TryParse(versionText, out var version) ? version : null;
    }

    internal static async Task<string?> ProbeVersionAsync(
        string executablePath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("--version");

        Process? process = null;
        try
        {
            process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken)
                .ConfigureAwait(false);
            var output = (await outputTask.ConfigureAwait(false)).Trim();
            return process.ExitCode == 0 && output.StartsWith("codex-cli ", StringComparison.Ordinal)
                ? output["codex-cli ".Length..]
                : null;
        }
        catch (OperationCanceledException)
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }
            throw;
        }
        catch (TimeoutException)
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }
            return null;
        }
        catch (Exception exception) when (
            exception is Win32Exception or UnauthorizedAccessException or IOException)
        {
            return null;
        }
        finally
        {
            process?.Dispose();
        }
    }
}
