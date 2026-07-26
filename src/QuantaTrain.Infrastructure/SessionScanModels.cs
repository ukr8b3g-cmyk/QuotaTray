using QuantaTrain.Core;

namespace QuantaTrain.Infrastructure;

public sealed record SessionScanProgress(int ProcessedFiles, int TotalFiles);

public sealed record SessionScanResult(
    IReadOnlyList<UsageAggregate> Rows,
    int ScannedFileCount,
    int SkippedFileCount,
    int ErrorFileCount);

internal sealed record SessionScanIndexDocument(
    int SchemaVersion,
    int ParserVersion,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<SessionScanIndexEntry> Files);

internal sealed record SessionScanIndexEntry(
    string PathSha256,
    long SizeBytes,
    DateTimeOffset LastWriteUtc,
    string PrefixSignature,
    string BoundarySignature,
    IReadOnlyList<UsageAggregate> Contributions,
    SessionParserContinuation Continuation);

internal sealed record SessionParserContinuation(
    bool ActiveTurn,
    DateTimeOffset? StartedAtUtc,
    string Model,
    string ReasoningEffort,
    string ServiceTier,
    UsageTokenTotals CurrentTokens,
    UsageTokenTotals? PreviousCumulative)
{
    public static readonly SessionParserContinuation Empty = new(
        false,
        null,
        "unknown",
        "unknown",
        "unknown",
        UsageTokenTotals.Empty,
        null);
}
