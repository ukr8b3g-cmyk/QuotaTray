# Project state

- Authoritative checkout: `D:\Codex\QuotaTray`
- Active branch: `main`
- Target version: `0.2.7`
- Settings schema: `7`
- Local test executable: `dist\portable\win-x64\QuantaTray.exe`
- Local test ZIP: `dist\QuantaTray-v0.2.7-win-x64-portable.zip`
- Current setup SHA-256: `031ec3bc9798ea41236cbea8090828d1d591a30e13c837b21a7085eba0201953`
- Current portable SHA-256: `7d9a6f45ba4d47b5b70c1d1c9855ba02cd5bfc3b8018d3adfd8862a4dd3c7c6b`
- Formal v0.2.7 release: published from commit `2eafe79`; the one-shot release workflow removed itself successfully.

## v0.2.7 decisions

- Keep the Windows tray slot unchanged; enlarge the 64px artwork and use digit-aware typography.
- Keep Mini and Compact unchanged. The detailed window remains 800 logical pixels wide, defaults to 700 logical pixels high, and scrolls vertically.
- Show four recent reset rows. The full-history window is an owned, reusable, modeless window.
- Read purchased-credit snapshots from `account/rateLimits/read` and account token summaries from `account/usage/read` through Codex App Server authentication.
- Local plugin/tool and skill counters are opt-in and persist only date, sanitized category/name, and count. Do not persist message text, commands, diffs, paths, identifiers, or raw session rows.
- Auto-charge state and purchase actions remain out of scope.
- Mini, Compact, and Detail persist independent monitor-relative positions. The legacy shared position is retained only for migration and downgrade compatibility.

## Validation

- `dotnet build QuantaTrain.slnx -c Debug --no-restore`
- `dotnet test QuantaTrain.slnx -c Debug --no-build --no-restore`
- Current result: 95 passed, 0 failed.
- QA screenshots: `artifacts\qa-v0.2.7`
