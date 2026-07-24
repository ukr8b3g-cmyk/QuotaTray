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

        var environmentPath = Environment.GetEnvironmentVariable("QUANTATRAIN_CODEX_PATH");
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            yield return Path.GetFullPath(environmentPath);
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
        yield return Path.Combine(userProfile, ".codex", "bin", "codex.exe");
        yield return Path.Combine(localAppData, "Programs", "Codex", "codex.exe");
    }

    private static async Task<string?> ProbeVersionAsync(
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

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        try
        {
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
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            throw;
        }
        catch (TimeoutException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            return null;
        }
    }
}
