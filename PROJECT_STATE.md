# Project state

- Authoritative checkout: `D:\Codex\QuotaTray`
- Active branch: `main` (v0.2.8 tag at `1afb860`)
- Target version: `0.2.8`
- Settings schema: `7`
- Local test executable: `dist\portable\win-x64\QuantaTray.exe`
- Local test ZIP: `dist\QuantaTray-v0.2.8-win-x64-portable.zip`
- Current local/published setup SHA-256: `ccb71a9982929770098bebbafa74116a6085e6efe0adf98cdd1579f46a8e7f89`
- Current local/published portable SHA-256: `58fcccffd94e9af3f9a8d870ba3fa9b5e3c80171f4aa977ded684bc7257dc035`
- Formal v0.2.7 release: published from commit `2eafe79`; the one-shot release workflow removed itself successfully.
- v0.2.8 GitHub Release published from tag `v0.2.8` at `1afb860`; local `dist` contains the downloaded published assets. The pre-tag local build from `006e334` is archived under `_snapshots\QuotaTray\pre-v028-published-assets-20260927_121755`.

## v0.2.7 decisions

- Keep the Windows tray slot unchanged; enlarge the 64px artwork and use digit-aware typography.
- Keep Mini and Compact unchanged. The detailed window remains 800 logical pixels wide, defaults to 700 logical pixels high, and scrolls vertically.
- Show four recent reset rows. The full-history window is an owned, reusable, modeless window.
- Read purchased-credit snapshots from `account/rateLimits/read` and account token summaries from `account/usage/read` through Codex App Server authentication.
- Local plugin/tool and skill counters are opt-in and persist only date, sanitized category/name, and count. Do not persist message text, commands, diffs, paths, identifiers, or raw session rows.
- Auto-charge state and purchase actions remain out of scope.
- Mini, Compact, and Detail persist independent monitor-relative positions. The legacy shared position is retained only for migration and downgrade compatibility.

## Validation

- `dotnet test QuantaTrain.slnx -c Release --no-restore`
- `.\packaging\scripts\build-release.ps1 -Version 0.2.8`
- Current local result: 96 passed, 0 failed (Release, 2026-09-27).
- GitHub release workflow run `36290691781`: succeeded; published assets matched `SHA256SUMS.txt`, ZIP contents and EXE version checked.
- QA screenshots: `artifacts\qa-v0.2.7`
