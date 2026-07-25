# Implementation verification

Verified on 2026-07-25.

## Environment

- Windows 11 x64
- .NET SDK 10.0.201 / .NET runtime 10.0.5
- Codex CLI 0.144.4 (current stable release checked on 2026-07-25)
- Inno Setup 6 is used for the per-user installer

## Codex App Server

`codex app-server generate-json-schema` completed successfully with Codex CLI
0.144.4. The generated stable protocol contains:

- `account/read`
- `account/login/start`
- `account/login/completed`
- `account/updated`
- `account/rateLimits/read`
- `account/rateLimits/updated`
- `rateLimitResetCredits.availableCount`
- nullable reset-credit detail rows and expiration timestamps
- `primary` / `secondary`, `rateLimitsByLimitId`, `windowDurationMins`,
  `usedPercent`, and Unix `resetsAt`

A local stdio probe completed `initialize` and `initialized`, then successfully
called `account/read`. The isolated test environment was not authenticated, so
the real `account/rateLimits/read` call returned the expected authentication
required error. It did not return a method-not-found or incompatibility error.

The automated integration test launches a fake App Server process over stdio,
performs initialization, reads account state and rate limits, and verifies the
weekly bucket and reset-credit count.

## Compatibility notes

- `account/rateLimits/read` accepts no params in Codex CLI 0.144.4. QuantaTray
  omits the `params` member for this request.
- Auto-discovery also checks the official per-user standalone package cache at
  `%USERPROFILE%\.codex\packages\standalone\releases\*\bin\codex.exe`.
  Inaccessible PATH candidates are skipped instead of aborting discovery.
- A signed-in live probe with Codex CLI 0.145.0 confirmed that the same
  discovery and App Server client path returns two buckets and selects the
  10,080-minute weekly window. No raw account response was logged.
- Rolling rate-limit notifications are sparse. QuantaTray coalesces them into
  a fresh `account/rateLimits/read` request rather than clearing missing fields.
- QuantaTray does not enable experimental capabilities.
- QuantaTray never calls the reset-credit consume method.
- No UI scraping, local HTTP listener, browser cookie access, credential-file
  access, telemetry, or developer backend is implemented.
