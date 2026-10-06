using System.Text.Json;
using QuantaTrain.Core;

namespace QuantaTrain.Infrastructure;

public static class AccountUsageRefresher
{
    public static async Task<AccountUsageSnapshot?> TryReadAsync(
        Func<CancellationToken, Task<AccountUsageSnapshot>> readUsage,
        RedactedLogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await readUsage(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception) when (
            exception is AppServerRpcException or TimeoutException or
            IOException or InvalidOperationException or JsonException or FormatException)
        {
            // RPC messages and parser exceptions may contain raw account data.
            var diagnostic = exception is AppServerRpcException rpc
                ? $"Account usage refresh failed (RPC code {rpc.Code}). Previous snapshot retained."
                : $"Account usage refresh failed ({exception.GetType().Name}). Previous snapshot retained.";
            await logger.WarningSafelyAsync(diagnostic).ConfigureAwait(false);
            return null;
        }
    }
}
