using QuantaTrain.Core;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.App.Tests;

public sealed class ResetConfirmationTests
{
    [Theory]
    [InlineData("history", "failure")]
    [InlineData("history", "success")]
    [InlineData("history", "updating")]
    [InlineData("baseline", "failure")]
    [InlineData("baseline", "success")]
    [InlineData("baseline", "updating")]
    public async Task NewerPollingResultDuringSaveCannotBeOverwritten(string stage, string nextResult)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? display = "confirmation-success";
        long refreshSequence = 2;
        var currentState = State(0);
        WeeklyQuotaState? baseline = null;
        var baselines = 0;
        Task Pause() { entered.TrySetResult(); return finish.Task; }
        var confirmation = ResetConfirmation.ApplyAsync(State(80), State(0), 1,
            () => Task.FromResult(new PollingRefreshResult(State(0), null, 2, 2)), () => true,
            _ => stage == "history" ? Pause() : Task.CompletedTask,
            state => { baseline = state; baselines++; return stage == "baseline" ? Pause() : Task.CompletedTask; },
            _ => display = null, CancellationToken.None, () => refreshSequence, () => currentState);
        await entered.Task;
        var latest = $"newer-{nextResult}";
        if (nextResult == "success") currentState = State(50);
        display = latest;
        refreshSequence++;
        finish.SetResult();
        await confirmation;
        Assert.Equal(latest, display);
        Assert.Equal(1, baselines);
        Assert.Equal(stage == "history" && nextResult == "success" ? 50 : 0, baseline!.UsedPercent);
        if (stage == "history")
            Assert.Null(ResetClassifier.Classify(baseline, currentState));
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("missing")]
    [InlineData("cached")]
    [InlineData("reconnected")]
    [InlineData("account")]
    [InlineData("limit")]
    [InlineData("plan")]
    [InlineData("older")]
    [InlineData("shutdown")]
    public async Task InvalidConfirmationCannotWriteHistoryBaselineOrClearFailure(string scenario)
    {
        var before = State(80);
        var candidate = State(0);
        var after = scenario switch
        {
            "limit" => candidate with { LimitId = "other" },
            "plan" => candidate with { PlanType = "other" },
            "older" => candidate with { ObservedAtUtc = candidate.ObservedAtUtc.AddMinutes(-1) },
            _ => candidate,
        };
        var connected = true;
        var records = 0; var baselines = 0; var renders = 0;
        using var cancellation = new CancellationTokenSource();
        async Task Run() => await ResetConfirmation.ApplyAsync(
            before, candidate, 1,
            () =>
            {
                if (scenario is "reconnected" or "account") connected = false;
                if (scenario == "shutdown") cancellation.Cancel();
                return Task.FromResult(new PollingRefreshResult(after,
                    scenario is "timeout" or "missing" ? scenario : null,
                    scenario == "cached" ? 1 : 2, 2));
            },
            () => connected,
            _ => { records++; return Task.CompletedTask; },
            _ => { baselines++; return Task.CompletedTask; },
            _ => renders++, cancellation.Token, () => 2, () => after);
        if (scenario == "shutdown")
            await Assert.ThrowsAnyAsync<OperationCanceledException>(Run);
        else
            await Run();
        Assert.Equal(0, records);
        Assert.Equal(0, baselines);
        Assert.Equal(0, renders);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(80, 0)]
    public async Task FreshConfirmationRecordsOnlySustainedRecovery(double used, int expectedRecords)
    {
        var records = new List<ResetEvent>();
        WeeklyQuotaState? baseline = null, rendered = null;
        var refreshed = State(used);
        await ResetConfirmation.ApplyAsync(State(80), State(0), 1,
            () => Task.FromResult(new PollingRefreshResult(refreshed, null, 2, 2)),
            () => true, item => { records.Add(item); return Task.CompletedTask; },
            state => { baseline = state; return Task.CompletedTask; },
            state => rendered = state, CancellationToken.None, () => 2, () => refreshed);
        Assert.Equal(expectedRecords, records.Count);
        Assert.All(records, item => Assert.True(item.Confirmed));
        Assert.Equal(used, baseline!.UsedPercent);
        Assert.Same(baseline, rendered);
    }

    private static WeeklyQuotaState State(double used) => new(
        "weekly", BucketRole.Secondary, used, 100 - used, 10080,
        DateTimeOffset.Parse("2026-10-28T00:00:00Z"), 1, [], "pro",
        DateTimeOffset.Parse("2026-10-24T00:00:00Z"), "test");
}
