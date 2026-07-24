using System.Text.Json;
using QuantaTrain.Core;

namespace QuantaTrain.Infrastructure;

public sealed record AccountStatus(bool IsSignedIn, string? PlanType);
public sealed record LoginStart(Uri AuthorizationUri, string LoginId);

public sealed class CodexAccountClient
{
    private readonly JsonRpcConnection _connection;
    private readonly string _codexVersion;
    private string? _accountPlanType;

    public CodexAccountClient(JsonRpcConnection connection, string codexVersion)
    {
        _connection = connection;
        _codexVersion = codexVersion;
        _connection.NotificationReceived += HandleNotification;
    }

    public event EventHandler? RateLimitsUpdated;
    public event EventHandler? AccountUpdated;
    public event EventHandler<bool>? LoginCompleted;

    public async Task<AccountStatus> ReadAccountAsync(CancellationToken cancellationToken)
    {
        var result = await _connection.SendRequestAsync(
            "account/read",
            new { refreshToken = false },
            cancellationToken).ConfigureAwait(false);
        var signedIn = result.TryGetProperty("account", out var account) &&
            account.ValueKind == JsonValueKind.Object;
        var planType = signedIn &&
            account.TryGetProperty("planType", out var plan) &&
            plan.ValueKind == JsonValueKind.String
                ? plan.GetString()
                : null;
        _accountPlanType = planType;
        return new AccountStatus(signedIn, planType);
    }

    public async Task<LoginStart> StartChatGptLoginAsync(CancellationToken cancellationToken)
    {
        var result = await _connection.SendRequestAsync(
            "account/login/start",
            new { type = "chatgpt" },
            cancellationToken).ConfigureAwait(false);
        var authUrl = result.GetProperty("authUrl").GetString();
        var loginId = result.GetProperty("loginId").GetString();
        if (!Uri.TryCreate(authUrl, UriKind.Absolute, out var authorizationUri) ||
            string.IsNullOrWhiteSpace(loginId))
        {
            throw new InvalidDataException("App Server returned an invalid login response.");
        }

        return new LoginStart(authorizationUri, loginId);
    }

    public async Task<RateLimitSnapshot> ReadRateLimitsAsync(
        CancellationToken cancellationToken)
    {
        var result = await _connection.SendRequestAsync(
            "account/rateLimits/read",
            parameters: null,
            cancellationToken).ConfigureAwait(false);
        var snapshot = ParseRateLimits(result, DateTimeOffset.UtcNow, _codexVersion);
        return snapshot.PlanType is null && _accountPlanType is not null
            ? snapshot with { PlanType = _accountPlanType }
            : snapshot;
    }

    public static RateLimitSnapshot ParseRateLimits(
        JsonElement result,
        DateTimeOffset observedAtUtc,
        string? codexVersion)
    {
        var buckets = new List<RateLimitBucket>();
        string? planType = null;

        if (result.TryGetProperty("rateLimitsByLimitId", out var byId) &&
            byId.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in byId.EnumerateObject())
            {
                ParseSnapshot(property.Value, property.Name, buckets, ref planType);
            }
        }
        else if (result.TryGetProperty("rateLimits", out var single) &&
                 single.ValueKind == JsonValueKind.Object)
        {
            ParseSnapshot(single, null, buckets, ref planType);
        }

        long? availableCount = null;
        IReadOnlyList<ResetCredit>? credits = null;
        if (result.TryGetProperty("rateLimitResetCredits", out var summary) &&
            summary.ValueKind == JsonValueKind.Object)
        {
            if (summary.TryGetProperty("availableCount", out var count) &&
                count.TryGetInt64(out var countValue))
            {
                availableCount = countValue;
            }

            if (summary.TryGetProperty("credits", out var creditArray) &&
                creditArray.ValueKind == JsonValueKind.Array)
            {
                credits = creditArray.EnumerateArray()
                    .Select(credit => new ResetCredit(
                        TryGetUnixTimestamp(credit, "expiresAt")))
                    .ToArray();
            }
        }

        return new RateLimitSnapshot(
            observedAtUtc,
            buckets,
            availableCount,
            credits,
            planType,
            codexVersion);
    }

    private static void ParseSnapshot(
        JsonElement snapshot,
        string? dictionaryLimitId,
        ICollection<RateLimitBucket> buckets,
        ref string? planType)
    {
        var limitId = TryGetString(snapshot, "limitId") ?? dictionaryLimitId;
        planType ??= TryGetString(snapshot, "planType");
        ParseWindow(snapshot, "primary", limitId, BucketRole.Primary, buckets);
        ParseWindow(snapshot, "secondary", limitId, BucketRole.Secondary, buckets);
    }

    private static void ParseWindow(
        JsonElement snapshot,
        string propertyName,
        string? limitId,
        BucketRole role,
        ICollection<RateLimitBucket> buckets)
    {
        if (!snapshot.TryGetProperty(propertyName, out var window) ||
            window.ValueKind != JsonValueKind.Object ||
            !window.TryGetProperty("usedPercent", out var used) ||
            !used.TryGetDouble(out var usedPercent))
        {
            return;
        }

        long? duration = window.TryGetProperty("windowDurationMins", out var durationElement) &&
            durationElement.TryGetInt64(out var durationValue)
                ? durationValue
                : null;
        buckets.Add(new RateLimitBucket(
            limitId,
            role,
            usedPercent,
            duration,
            TryGetUnixTimestamp(window, "resetsAt")));
    }

    private static string? TryGetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static DateTimeOffset? TryGetUnixTimestamp(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.TryGetInt64(out var timestamp)
            ? DateTimeOffset.FromUnixTimeSeconds(timestamp)
            : null;

    private void HandleNotification(object? sender, AppServerNotificationEventArgs eventArgs)
    {
        switch (eventArgs.Method)
        {
            case "account/rateLimits/updated":
                RateLimitsUpdated?.Invoke(this, EventArgs.Empty);
                break;
            case "account/updated":
                AccountUpdated?.Invoke(this, EventArgs.Empty);
                break;
            case "account/login/completed":
                var success = eventArgs.Parameters.ValueKind == JsonValueKind.Object &&
                    eventArgs.Parameters.TryGetProperty("success", out var successElement) &&
                    successElement.ValueKind == JsonValueKind.True;
                LoginCompleted?.Invoke(this, success);
                break;
        }
    }
}
