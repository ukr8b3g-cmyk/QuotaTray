using System.Globalization;
using System.Text;
using System.Text.Json;
using QuantaTrain.Core;

namespace QuantaTrain.Infrastructure;

public sealed class UsageExportService
{
    public async Task ExportJsonAsync(
        string path,
        IEnumerable<UsageAggregate> rows,
        CancellationToken cancellationToken)
    {
        await AtomicJsonFile.WriteAsync(
            path,
            rows.ToArray(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportCsvAsync(
        string path,
        IEnumerable<UsageAggregate> rows,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder(
            "localDate,model,reasoningEffort,serviceTier,inputTokens," +
            "cachedInputTokens,cacheWriteInputTokens,outputTokens," +
            "reasoningOutputTokens,totalTokens,turnCount,elapsedMilliseconds\r\n");
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.Append(row.Key.LocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Append(',').Append(Escape(row.Key.Model))
                .Append(',').Append(Escape(row.Key.ReasoningEffort))
                .Append(',').Append(Escape(row.Key.ServiceTier))
                .Append(',').Append(row.Tokens.InputTokens)
                .Append(',').Append(row.Tokens.CachedInputTokens)
                .Append(',').Append(row.Tokens.CacheWriteInputTokens)
                .Append(',').Append(row.Tokens.OutputTokens)
                .Append(',').Append(row.Tokens.ReasoningOutputTokens)
                .Append(',').Append(row.Tokens.EffectiveTotalTokens)
                .Append(',').Append(row.TurnCount)
                .Append(',').Append(
                    row.ExactElapsedMilliseconds + row.EstimatedElapsedMilliseconds)
                .Append("\r\n");
        }
        await File.WriteAllTextAsync(
            path,
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            cancellationToken).ConfigureAwait(false);
    }

    private static string Escape(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
