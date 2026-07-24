using QuantaTrain.Core;

namespace QuantaTrain.Core.Tests;

public sealed class ResetClassifierTests
{
    [Fact]
    public void DetectsScheduledReset()
    {
        var before = State(80, 2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1));
        var after = State(0, 2, before.ObservedAtUtc.AddMinutes(2), before.ResetsAtUtc);

        var reset = ResetClassifier.Classify(before, after);

        Assert.Equal(ResetClassification.ScheduledReset, reset?.Classification);
    }

    [Fact]
    public void DetectsLikelyResetCreditUse()
    {
        var before = State(80, 2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(2));
        var after = State(0, 1, before.ObservedAtUtc.AddMinutes(1), before.ResetsAtUtc);

        var reset = ResetClassifier.Classify(before, after);

        Assert.Equal(ResetClassification.ResetCreditLikely, reset?.Classification);
    }

    [Fact]
    public void MarksUnexpectedRecoveryAsCandidate()
    {
        var before = State(80, 2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(2));
        var after = State(0, 2, before.ObservedAtUtc.AddMinutes(1), before.ResetsAtUtc);

        var reset = ResetClassifier.Classify(before, after);

        Assert.Equal(ResetClassification.UnexpectedResetCandidate, reset?.Classification);
    }

    [Fact]
    public void TreatsPlanChangeAsPolicyChange()
    {
        var before = State(80, 2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(2));
        var after = State(0, 2, before.ObservedAtUtc.AddMinutes(1), before.ResetsAtUtc)
            with { PlanType = "pro" };

        var reset = ResetClassifier.Classify(before, after);

        Assert.Equal(ResetClassification.LimitPolicyChange, reset?.Classification);
    }

    [Fact]
    public void IgnoresSmallCorrections()
    {
        var before = State(50, 2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(2));
        var after = State(40, 2, before.ObservedAtUtc.AddMinutes(1), before.ResetsAtUtc);

        Assert.Null(ResetClassifier.Classify(before, after));
    }

    private static WeeklyQuotaState State(
        double used,
        long credits,
        DateTimeOffset observed,
        DateTimeOffset? reset) =>
        new(
            "codex",
            BucketRole.Primary,
            used,
            100 - used,
            10080,
            reset,
            credits,
            [],
            "plus",
            observed,
            "0.144.4");
}
