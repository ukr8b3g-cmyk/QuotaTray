namespace QuantaTrain.Core;

public enum Freshness
{
    Unknown,
    Current,
    Stale,
}

public static class FreshnessPolicy
{
    public static Freshness Evaluate(DateTimeOffset? lastSuccessUtc, DateTimeOffset nowUtc)
    {
        if (lastSuccessUtc is null)
        {
            return Freshness.Unknown;
        }

        return nowUtc - lastSuccessUtc.Value > TimeSpan.FromSeconds(90)
            ? Freshness.Stale
            : Freshness.Current;
    }
}
