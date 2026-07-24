using QuantaTrain.Core;

namespace QuantaTrain.Infrastructure;

public sealed class PollingStateChangedEventArgs(
    WeeklyQuotaState? state,
    bool isUpdating,
    string? error) : EventArgs
{
    public WeeklyQuotaState? State { get; } = state;
    public bool IsUpdating { get; } = isUpdating;
    public string? Error { get; } = error;
}

public sealed class PollingCoordinator : IAsyncDisposable
{
    private readonly CodexAccountClient _client;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _singleFlightLock = new();
    private Task<WeeklyQuotaState?>? _inFlight;
    private Task? _loop;
    private bool _disposed;

    public PollingCoordinator(CodexAccountClient client, TimeSpan interval)
    {
        _client = client;
        _interval = interval;
        _client.RateLimitsUpdated += HandleRateLimitsUpdated;
    }

    public event EventHandler<PollingStateChangedEventArgs>? StateChanged;

    public WeeklyQuotaState? Current { get; private set; }
    public DateTimeOffset? LastSuccessUtc { get; private set; }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _loop ??= RunLoopAsync(_lifetime.Token);
    }

    public Task<WeeklyQuotaState?> RefreshAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_singleFlightLock)
        {
            if (_inFlight is { IsCompleted: false })
            {
                return _inFlight;
            }

            _inFlight = RefreshCoreAsync(cancellationToken);
            return _inFlight;
        }
    }

    private async Task<WeeklyQuotaState?> RefreshCoreAsync(CancellationToken cancellationToken)
    {
        StateChanged?.Invoke(this, new PollingStateChangedEventArgs(Current, true, null));
        try
        {
            var snapshot = await _client.ReadRateLimitsAsync(cancellationToken)
                .ConfigureAwait(false);
            var weekly = WeeklyBucketSelector.BuildState(snapshot);
            if (weekly is null)
            {
                StateChanged?.Invoke(
                    this,
                    new PollingStateChangedEventArgs(
                        Current,
                        false,
                        "Weekly usage limit is unavailable."));
                return Current;
            }

            Current = weekly;
            LastSuccessUtc = weekly.ObservedAtUtc;
            StateChanged?.Invoke(this, new PollingStateChangedEventArgs(Current, false, null));
            return Current;
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            StateChanged?.Invoke(
                this,
                new PollingStateChangedEventArgs(
                    Current,
                    false,
                    Redaction.Redact(exception.Message)));
            return Current;
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void HandleRateLimitsUpdated(object? sender, EventArgs eventArgs)
    {
        _ = RefreshAsync(_lifetime.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _client.RateLimitsUpdated -= HandleRateLimitsUpdated;
        _lifetime.Cancel();
        if (_loop is not null)
        {
            await _loop.IgnoreCancellation().ConfigureAwait(false);
        }
        _lifetime.Dispose();
    }
}
