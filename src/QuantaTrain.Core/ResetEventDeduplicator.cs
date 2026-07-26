using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace QuantaTrain.Core;

public static class ResetEventDeduplicator
{
    public static string CreateKey(ResetEvent resetEvent)
    {
        ArgumentNullException.ThrowIfNull(resetEvent);
        var material = string.Join(
            '|',
            resetEvent.After.LimitId ?? string.Empty,
            RoundToMinute(resetEvent.ObservedFromUtc).ToString("O"),
            RoundToMinute(resetEvent.ObservedToUtc).ToString("O"),
            Math.Round(resetEvent.Before.UsedPercent)
                .ToString(CultureInfo.InvariantCulture),
            Math.Round(resetEvent.After.UsedPercent)
                .ToString(CultureInfo.InvariantCulture),
            resetEvent.After.ResetsAtUtc?.ToString("O") ?? string.Empty,
            resetEvent.Classification);
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    private static DateTimeOffset RoundToMinute(DateTimeOffset value) =>
        DateTimeOffset.FromUnixTimeSeconds(value.ToUnixTimeSeconds() / 60 * 60);
}
