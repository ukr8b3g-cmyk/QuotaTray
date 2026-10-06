using System.Text.Json;

var usageScenario = args.FirstOrDefault(argument => argument.StartsWith("--usage=", StringComparison.Ordinal));

while (await Console.In.ReadLineAsync() is { } line)
{
    JsonDocument document;
    try
    {
        document = JsonDocument.Parse(line);
    }
    catch (JsonException)
    {
        continue;
    }

    using (document)
    {
        var root = document.RootElement;
        if (!root.TryGetProperty("id", out var id))
        {
            continue;
        }

        var method = root.GetProperty("method").GetString();
        if (method == "account/usage/read" && usageScenario == "--usage=unsupported")
        {
            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new
            {
                id,
                error = new { code = -32601, message = "synthetic-private-account-payload" },
            }));
            await Console.Out.FlushAsync();
            continue;
        }
        if (method == "account/usage/read" && usageScenario == "--usage=timeout")
        {
            continue;
        }
        object result = method switch
        {
            "initialize" => new { serverInfo = new { name = "fake", version = "1.0" } },
            "account/read" => new
            {
                account = new { type = "chatgpt", planType = "plus", email = (string?)null },
                requiresOpenaiAuth = true,
            },
            "account/rateLimits/read" => new
            {
                rateLimits = new
                {
                    limitId = "codex",
                    planType = (string?)null,
                    primary = new
                    {
                        usedPercent = 5.6,
                        windowDurationMins = 10080,
                        resetsAt = DateTimeOffset.UtcNow.AddDays(2).ToUnixTimeSeconds(),
                    },
                    secondary = (object?)null,
                },
                rateLimitsByLimitId = (object?)null,
                rateLimitResetCredits = new { availableCount = 2, credits = (object?)null },
            },
            "account/usage/read" => new
            {
                summary = new
                {
                    lifetimeTokens = 150,
                    peakDailyTokens = 50,
                    longestRunningTurnSec = (long?)null,
                    currentStreakDays = (int?)null,
                    longestStreakDays = (int?)null,
                },
                dailyUsageBuckets = Array.Empty<object>(),
            },
            _ => new { },
        };

        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new { id, result }));
        await Console.Out.FlushAsync();
    }
}
