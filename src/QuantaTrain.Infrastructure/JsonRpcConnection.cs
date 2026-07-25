using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QuantaTrain.Infrastructure;

public sealed class JsonRpcConnection : IAsyncDisposable
{
    private const int MaximumPendingRequests = 64;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Process _process;
    private readonly Task _stdoutTask;
    private readonly Task _stderrTask;
    private long _nextId;
    private bool _disposed;

    private JsonRpcConnection(Process process)
    {
        _process = process;
        _stdoutTask = ReadStdoutAsync(_lifetime.Token);
        _stderrTask = ReadStderrAsync(_lifetime.Token);
    }

    public event EventHandler<AppServerNotificationEventArgs>? NotificationReceived;
    public event EventHandler? Exited;

    public string? LastDiagnostic { get; private set; }

    public static async Task<JsonRpcConnection> StartAsync(
        string codexPath,
        string clientVersion,
        CancellationToken cancellationToken) =>
        await StartAsync(
            codexPath,
            [],
            clientVersion,
            cancellationToken).ConfigureAwait(false);

    public static async Task<JsonRpcConnection> StartAsync(
        string executablePath,
        IReadOnlyList<string> prefixArguments,
        string clientVersion,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(prefixArguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in prefixArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        startInfo.ArgumentList.Add("app-server");
        startInfo.ArgumentList.Add("--stdio");

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("Codex App Server could not be started.");
        }

        var connection = new JsonRpcConnection(process);
        process.Exited += connection.HandleExited;
        try
        {
            await connection.SendRequestAsync(
                "initialize",
                new
                {
                    clientInfo = new
                    {
                        name = "quantatray",
                        title = "QuantaTray",
                        version = clientVersion,
                    },
                    capabilities = new { experimentalApi = false },
                },
                cancellationToken).ConfigureAwait(false);
            await connection.SendNotificationAsync(
                "initialized",
                new { },
                cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<JsonElement> SendRequestAsync(
        string method,
        object? parameters,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pending.Count >= MaximumPendingRequests)
        {
            throw new InvalidOperationException("Too many pending App Server requests.");
        }

        var id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, completion))
        {
            throw new InvalidOperationException("Could not register App Server request.");
        }

        try
        {
            var message = new JsonObject
            {
                ["method"] = method,
                ["id"] = id,
            };
            if (parameters is not null)
            {
                message["params"] = JsonSerializer.SerializeToNode(parameters);
            }

            await WriteAsync(message, cancellationToken).ConfigureAwait(false);
            return await completion.Task.WaitAsync(
                TimeSpan.FromSeconds(15),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    public async Task SendNotificationAsync(
        string method,
        object? parameters,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var message = new JsonObject { ["method"] = method };
        if (parameters is not null)
        {
            message["params"] = JsonSerializer.SerializeToNode(parameters);
        }

        await WriteAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteAsync(JsonObject message, CancellationToken cancellationToken)
    {
        var json = message.ToJsonString();
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _process.StandardInput.WriteLineAsync(json.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            await _process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadStdoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _process.StandardOutput.ReadLineAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                ProcessLine(line);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            FailPending(new IOException("App Server output failed.", exception));
        }
    }

    private void ProcessLine(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.TryGetProperty("id", out var idElement) &&
                idElement.TryGetInt64(out var id) &&
                _pending.TryGetValue(id, out var completion))
            {
                if (root.TryGetProperty("error", out var error))
                {
                    var code = error.TryGetProperty("code", out var codeElement)
                        ? codeElement.GetInt32()
                        : -1;
                    var message = error.TryGetProperty("message", out var messageElement)
                        ? messageElement.GetString() ?? "App Server request failed."
                        : "App Server request failed.";
                    completion.TrySetException(new AppServerRpcException(code, message));
                }
                else if (root.TryGetProperty("result", out var result))
                {
                    completion.TrySetResult(result.Clone());
                }
                else
                {
                    completion.TrySetException(
                        new InvalidDataException("App Server response has no result."));
                }
                return;
            }

            if (root.TryGetProperty("method", out var methodElement))
            {
                var method = methodElement.GetString();
                if (!string.IsNullOrWhiteSpace(method))
                {
                    var parameters = root.TryGetProperty("params", out var paramsElement)
                        ? paramsElement.Clone()
                        : default;
                    NotificationReceived?.Invoke(
                        this,
                        new AppServerNotificationEventArgs(method, parameters));
                }
            }
        }
        catch (JsonException)
        {
            LastDiagnostic = "Malformed JSON line received from App Server.";
        }
    }

    private async Task ReadStderrAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _process.StandardError.ReadLineAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                LastDiagnostic = Redaction.Redact(line);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void HandleExited(object? sender, EventArgs eventArgs)
    {
        FailPending(new IOException("Codex App Server exited."));
        Exited?.Invoke(this, EventArgs.Empty);
    }

    private void FailPending(Exception exception)
    {
        foreach (var completion in _pending.Values)
        {
            completion.TrySetException(exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _process.Exited -= HandleExited;
        _lifetime.Cancel();
        try
        {
            _process.StandardInput.Close();
            if (!_process.HasExited)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try
                {
                    await _process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _process.Kill(entireProcessTree: true);
                    await _process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
        }
        finally
        {
            await Task.WhenAll(
                _stdoutTask.IgnoreCancellation(),
                _stderrTask.IgnoreCancellation()).ConfigureAwait(false);
            _process.Dispose();
            _writeLock.Dispose();
            _lifetime.Dispose();
        }
    }
}

public sealed class AppServerNotificationEventArgs(
    string method,
    JsonElement parameters) : EventArgs
{
    public string Method { get; } = method;
    public JsonElement Parameters { get; } = parameters;
}

internal static class TaskExtensions
{
    public static async Task IgnoreCancellation(this Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
