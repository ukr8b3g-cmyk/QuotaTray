using QuantaTrain.Core;

namespace QuantaTrain.Infrastructure;

public sealed record PollingRefreshResult(
    WeeklyQuotaState? State, string? Error, long SuccessSequence, long RefreshSequence = 0)
{
    public bool Succeeded => State is not null && Error is null;
}

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
    private readonly CodexAccountClient? _client;
    private readonly Func<CancellationToken, Task<RateLimitSnapshot>> _readRateLimits;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _singleFlightLock = new();
    private Task<PollingRefreshResult>? _inFlight;
    private Task? _loop;
    private bool _disposed;
    private long _refreshSequence;

    public PollingCoordinator(CodexAccountClient client, TimeSpan interval)
        : this(client.ReadRateLimitsAsync, interval)
    {
        _client = client;
        _client.RateLimitsUpdated += HandleRateLimitsUpdated;
    }

    internal PollingCoordinator(
        Func<CancellationToken, Task<RateLimitSnapshot>> readRateLimits,
        TimeSpan interval)
    {
        _readRateLimits = readRateLimits;
        _interval = interval;
    }

    public event EventHandler<PollingStateChangedEventArgs>? StateChanged;

    public WeeklyQuotaState? Current { get; private set; }
    public DateTimeOffset? LastSuccessUtc { get; private set; }
    public long SuccessSequence { get; private set; }
    public long RefreshSequence => Interlocked.Read(ref _refreshSequence);
    public bool IsDisposed => _disposed;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _loop ??= RunLoopAsync(_lifetime.Token);
    }

    public Task<PollingRefreshResult> RefreshAsync(CancellationToken cancellationToken)
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

    private async Task<PollingRefreshResult> RefreshCoreAsync(CancellationToken cancellationToken)
    {
        var refreshSequence = Interlocked.Increment(ref _refreshSequence);
        StateChanged?.Invoke(this, new PollingStateChangedEventArgs(Current, true, null));
        try
        {
            var snapshot = await _readRateLimits(cancellationToken)
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
                return new(Current, "Weekly usage limit is unavailable.", SuccessSequence, refreshSequence);
            }

            Current = weekly;
            LastSuccessUtc = weekly.ObservedAtUtc;
            SuccessSequence++;
            StateChanged?.Invoke(this, new PollingStateChangedEventArgs(Current, false, null));
            return new(Current, null, SuccessSequence, refreshSequence);
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
            return new(Current, Redaction.Redact(exception.Message), SuccessSequence, refreshSequence);
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
        if (_client is not null)
        {
            _client.RateLimitsUpdated -= HandleRateLimitsUpdated;
        }
        _lifetime.Cancel();
        if (_loop is not null)
        {
            await _loop.IgnoreCancellation().ConfigureAwait(false);
        }
        _lifetime.Dispose();
    }
}
