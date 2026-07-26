namespace QuantaTrain.Core;

public sealed record QuotaStateEnvelope(
    int SchemaVersion,
    DateTimeOffset SavedAtUtc,
    WeeklyQuotaState State)
{
    public static QuotaStateEnvelope Create(WeeklyQuotaState state) =>
        new(2, DateTimeOffset.UtcNow, state);
}
