# QuantaTrain repository instructions

## Mission

Implement and release QuantaTrain, a lightweight Windows tray monitor that displays OpenAI Codex weekly quota remaining, reset time, reset-credit metadata, and locally inferred reset history.

## Source of truth

Read these before changing code:

1. `PROJECT_MANIFEST.yaml`
2. `CODEX_IMPLEMENTATION_TASK.md`
3. `docs/01_PRODUCT_SPEC.md` through `docs/11_RISKS_AND_FALLBACKS.md`
4. `README_TEMPLATE.md` and `PRIVACY_TEMPLATE.md`

When generated mockups disagree with written specifications, the written specifications win.

## Non-negotiable requirements

- Product name is `QuantaTrain` / `クオンタトレイン`.
- Target Windows with C# and .NET 10 LTS using Windows Forms.
- Do not use Electron, WebView, a browser-embedded UI, or a local HTTP listener.
- Use `codex app-server` over local stdio JSONL only.
- Use stable App Server methods only; do not enable experimental APIs unless the user explicitly approves it.
- Do not scrape ChatGPT or Codex UI pages.
- Do not read browser cookies, browser password stores, or Codex credential files directly.
- Let Codex App Server reuse its own cached login; use the official browser login flow only when needed.
- Never persist access tokens, cookies, passwords, account IDs, email addresses, or raw account responses.
- Never call `account/rateLimitResetCredit/consume` or expose any reset-consumption button.
- No telemetry, analytics, ads, crash upload, or developer-operated backend.
- Do not access Codex conversations, threads, project files, or source repositories for runtime operation.
- Default polling interval is 60 seconds; opening the panel triggers a coalesced immediate refresh.
- Persist history only when state changes, on reset events, or at a bounded checkpoint interval.
- Default startup state is tray-only; left-click opens compact weekly-quota view.
- Transparency, always-on-top, position lock, position memory, edge snap, and persistent panel behavior are opt-in and off by default.
- Produce a per-user x64 installer and a self-contained x64 portable ZIP. Do not produce x86.
- Do not upload, push, create a release, or modify a remote repository until the user supplies the target repository and authorizes the write.

## Implementation discipline

- Prefer the .NET base class library. Add production dependencies only when they provide clear value and document each one.
- Use manual composition rather than a heavyweight dependency-injection container.
- Use async I/O, cancellation tokens, and a single non-overlapping polling loop.
- Dispose timers, streams, processes, icons, forms, cancellation sources, and event subscriptions.
- Bound logs, queues, retry loops, history retention, and in-memory collections.
- Use tolerant JSON deserialization and graceful degradation for optional App Server fields.
- Generate App Server JSON schemas from the installed Codex version during compatibility work; do not copy assumptions blindly.
- Keep raw App Server payloads out of normal logs. Redact secrets and identifiers in diagnostics.
- Run unit, integration, packaging, DPI, localization, and memory-soak checks before release.

## Stop conditions

Stop and report rather than inventing a workaround when:

- `account/rateLimits/read` is unavailable or incompatible.
- No weekly window can be identified from returned data.
- Codex CLI/App Server cannot be found and an official installation path has not been approved.
- Authentication would require reading browser or credential files directly.
- A requested feature would require scraping private UI or calling an undocumented endpoint.

## Git and release expectations

- Work on a feature branch.
- Make focused commits with tests.
- Do not commit secrets, generated auth files, signing material, user history, or local settings.
- Release assets must include installer, portable ZIP, and SHA-256 checksums.
- README must state that the app is unofficial and not affiliated with OpenAI.
