using System.IO.Pipes;
using System.Text;

namespace QuantaTrain.App;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly string _pipeName;
    private readonly Task? _serverTask;
    private int _activationRequested;
    private int _shutdownRequested;

    private SingleInstanceCoordinator(Mutex mutex, bool isPrimary, string pipeName)
    {
        _mutex = mutex;
        IsPrimary = isPrimary;
        _pipeName = pipeName;
        if (isPrimary)
        {
            _serverTask = RunServerAsync(_lifetime.Token);
        }
    }

    public bool IsPrimary { get; }

    public static SingleInstanceCoordinator Create()
    {
        var user = new string(Environment.UserName
            .Where(character => char.IsLetterOrDigit(character))
            .ToArray());
        var name = $"Local\\QuantaTray-{user}";
        var mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        return new SingleInstanceCoordinator(mutex, createdNew, $"QuantaTray-{user}");
    }

    public bool ConsumeActivationRequest() =>
        Interlocked.Exchange(ref _activationRequested, 0) == 1;

    public bool ConsumeShutdownRequest() =>
        Interlocked.Exchange(ref _shutdownRequested, 0) == 1;

    public void NotifyPrimary() => SendCommand("activate");

    public void RequestPrimaryShutdown() => SendCommand("shutdown");

    public bool WaitForPrimaryExit(TimeSpan timeout)
    {
        if (IsPrimary)
        {
            return true;
        }

        try
        {
            if (!_mutex.WaitOne(timeout))
            {
                return false;
            }
        }
        catch (AbandonedMutexException)
        {
            // The prior process ended without a clean mutex release.
        }

        _mutex.ReleaseMutex();
        return true;
    }

    private void SendCommand(string command)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);
            client.Connect(1000);
            var bytes = Encoding.UTF8.GetBytes(command);
            client.Write(bytes);
            client.Flush();
        }
        catch (TimeoutException)
        {
        }
        catch (IOException)
        {
        }
    }

    private async Task RunServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                var buffer = new byte[32];
                var count = await server.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                var command = Encoding.UTF8.GetString(buffer, 0, count);
                if (command == "activate")
                {
                    Interlocked.Exchange(ref _activationRequested, 1);
                }
                else if (command == "shutdown")
                {
                    Interlocked.Exchange(ref _shutdownRequested, 1);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        try
        {
            _serverTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        if (IsPrimary)
        {
            _mutex.ReleaseMutex();
        }
        _mutex.Dispose();
        _lifetime.Dispose();
    }
}
