using QuantaTrain.Core;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.App;

internal static class ResetConfirmation
{
    internal static async Task ApplyAsync(
        WeeklyQuotaState before,
        WeeklyQuotaState candidate,
        long candidateSequence,
        Func<Task<PollingRefreshResult>> refresh,
        Func<bool> isCurrentConnection,
        Func<ResetEvent, Task> record,
        Func<WeeklyQuotaState, Task> setBaseline,
        Action<WeeklyQuotaState> updateViews,
        CancellationToken cancellationToken,
        Func<long> currentRefreshSequence,
        Func<WeeklyQuotaState> currentSuccessfulState)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!isCurrentConnection()) return;
        var result = await refresh();
        bool IsCurrentResult() =>
            isCurrentConnection() && currentRefreshSequence() == result.RefreshSequence;
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCurrentResult() || !result.Succeeded ||
            result.SuccessSequence <= candidateSequence ||
            result.State!.ObservedAtUtc < candidate.ObservedAtUtc ||
            result.State.LimitId != candidate.LimitId ||
            result.State.Role != candidate.Role ||
            result.State.WindowDurationMinutes != candidate.WindowDurationMinutes ||
            result.State.PlanType != candidate.PlanType)
        {
            return;
        }
        var state = result.State;
        var reset = ResetClassifier.Classify(before, state, confirmed: true);
        if (reset is not null)
        {
            await record(reset);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!isCurrentConnection()) return;
        // A later failure must not leave the pre-reset baseline behind. A later
        // success, on the other hand, must supply the newest baseline.
        await setBaseline(currentSuccessfulState());
        if (IsCurrentResult()) updateViews(state);
    }
}
