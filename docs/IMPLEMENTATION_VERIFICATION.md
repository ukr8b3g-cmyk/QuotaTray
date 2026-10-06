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

## Codex 0.160.1 compatibility corrections (2026-10-06)

This pass compares public source/schema contracts and synthetic fixtures against
`openai/codex` tag `rust-v0.160.1`. It does not replace the historical live-probe
results above or claim a signed-in live probe on 0.160.1.

Primary contract references:

- [Nullable account window and reset-credit fields](https://github.com/openai/codex/blob/rust-v0.160.1/codex-rs/app-server-protocol/src/protocol/v2/account.rs)
- [TokenUsageRecord and ThreadSettingsSnapshot](https://github.com/openai/codex/blob/rust-v0.160.1/codex-rs/protocol/src/protocol.rs)
- [Response recording, repeated token snapshots and context estimates](https://github.com/openai/codex/blob/rust-v0.160.1/codex-rs/core/src/session/mod.rs)
- [Observed response usage regression fixture](https://github.com/openai/codex/blob/rust-v0.160.1/codex-rs/core/tests/suite/token_usage_rollout.rs)

Implementation boundaries:

- Nullable/unusable optional quota fields become unavailable independently; a
  usable weekly window remains selectable. Missing duration never invents a
  weekly window and missing reset/expiration never invents a timestamp.
- Observed response records take precedence over legacy token-count fallback
  within the same turn. Numeric cumulative response progress prevents replay
  double counting within a file without storing response or thread identifiers.
  Per-turn cumulative counters recover missing response rows; compaction
  checkpoints seed/reset the observed counter without adding usage. Copied
  ancestor histories in separate fork files can still overcount. Exact
  per-response model/tier attribution across mid-turn settings changes is not
  supported; the local dashboard remains best-effort.
- Legacy token snapshots are best-effort. Only advancing cumulative usage with
  actual input/output components is counted; ambiguous last-only rows, repeated
  snapshots and context-only estimates add nothing. This may undercount older
  or incomplete files. No billing or current-context figure is inferred.
- Full thread-settings snapshots update tier/model/reasoning metadata. Missing
  service tier clears the previous tier to unknown; old turn-context fields
  remain supported.
- Plain JSONL checkpoints end at a newline. Incomplete JSON or UTF-8 bytes are
  read again after append. Row storage is capped at 4 MiB before decoding.
- Zstandard decoding uses pinned `ZstdSharp.Port` 0.8.8, streams without a
  temporary decompressed file, caps the decoder window at 64 MiB and emitted
  output at 512 MiB per file, and verifies frame completion. Corrupt/oversized
  files increment the error count and retain any previous aggregate.
- Raw/compressed siblings use one logical cache key, with the raw file preferred.
  A compression transition replaces its contribution rather than adding it.
  Changed compressed files are reread from their start; old parser caches are
  invalidated. Codex rollout compression is optional and defaults off in
  [0.160.1](https://github.com/openai/codex/blob/rust-v0.160.1/codex-rs/features/src/lib.rs#L1171-L1174).
  No Codex feature/configuration is enabled by QuantaTray.
- Account usage RPC/timeout failures preserve the prior successful snapshot,
  log only type/code diagnostics, and cannot escape the manual UI refresh
  handler. Shutdown cancellation is handled without updating disposed UI.

Regression coverage is in `CodexNullableAccountParsingTests`,
`AccountUsageRefresherTests`, and `CodexSessionCompatibilityTests`. All fixtures
are synthetic; no user session logs or credentials are accessed. Runtime test
results for this patch are recorded below after execution. Windows visual QA,
signed-in Codex 0.160.1 operation, packaging installation and memory-soak tests
are outside this compatibility test pass and remain unverified.
