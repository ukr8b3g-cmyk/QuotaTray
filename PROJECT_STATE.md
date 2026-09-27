# Project state

- Authoritative checkout: `D:\Codex\QuotaTray`
- Active branch: `main` (v0.2.8 fix prepared on `fix/reasoning-donut-drawarc`)
- Target version: `0.2.8`
- Settings schema: `7`
- Local test executable: `dist\portable\win-x64\QuantaTray.exe`
- Local test ZIP: `dist\QuantaTray-v0.2.8-win-x64-portable.zip`
- Current setup SHA-256: `d71411a2d98a9c953120965c1d2dd811ff1f6d6ce5730b525d00fbfa5f54ac05`
- Current portable SHA-256: `1f4a5b7a2aaad0c45d4fdc0ee4e92ee6de88e7662d49e6448f5f832ff4460672`
- Formal v0.2.7 release: published from commit `2eafe79`; the one-shot release workflow removed itself successfully.
- v0.2.8 installer and portable ZIP: built locally from source commit `006e334`; no v0.2.8 GitHub Release yet.

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
- Current result: 96 passed, 0 failed (Release, 2026-09-27).
- QA screenshots: `artifacts\qa-v0.2.7`
