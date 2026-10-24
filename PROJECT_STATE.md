# Project state

## QuantaTray 0.2.11 published - 2026-10-24

- Release version: `0.2.11`. All nine reviewed fixes and their regression tests are included.
- README, changelog, bilingual Release Notes and installer metadata are updated.
- Local version build: zero warnings/errors; 255 normal tests passed. Exact-commit Windows CI passed Core 42 + App 92 + Integration 122 = 256, zero failures/skips.
- Local installer and portable ZIP passed version, x64, content and checksum checks. Local host execution was skipped to avoid signaling an already-running application.
- Release/tag source: `2d7ea11c57269a481046e050eb3b7bf73aae59d3`; [GitHub Release](https://github.com/ukr8b3g-cmyk/QuotaTray/releases/tag/v0.2.11) is public and Latest.
- Final published packages were built locally from that commit, following the user's requested procedure. The initially published CI assets were backed up and replaced only in v0.2.11, after returning it to draft and verifying all three re-downloaded files against local originals.
- Final SHA-256: setup `18e4d4c11241d037f5eb37bff6e799bd75efe3228af3f89c00257e6f6b44e40b`; portable `efbcc9c2af77815f12958afb0b35f9b85691e62712dcb2dcdc9f5b7022a0704d`.
- Public setup size: 46,983,696 bytes; portable: 46,613,470 bytes. EXE version is `0.2.11+2d7ea11c57269a481046e050eb3b7bf73aae59d3`. The installer is `0.2.11.0`, unsigned.
- CI: [build 40073956486](https://github.com/ukr8b3g-cmyk/QuotaTray/actions/runs/40073956486), [CodeQL 40073955912](https://github.com/ukr8b3g-cmyk/QuotaTray/actions/runs/40073955912), [release 40073956435](https://github.com/ukr8b3g-cmyk/QuotaTray/actions/runs/40073956435): all succeeded for the release commit.
- Release workflow is restricted to the direct successor of reviewed base `a3a8ff9090facb63bbd446575dda2d7df1b56007`; later ordinary pushes cannot execute its release job.
- Connected GitHub reads confirmed the final public digests, exact tag commit, Latest state, and unchanged v0.2.10 asset IDs/digests. Local dist matches the final published local-build files; neither installed applications nor user settings were changed.
- Pre-publication snapshot: `D:\Codex\_snapshots\QuotaTray\pre-v0211-release-20261024_161505` (162 checkout files and two workspace records verified).

## Pre-publication repair evidence

## Current local review fixes - 2026-10-24

- Repair checkout: `D:\Codex\Internal\Active\quantatray-review-fixes-20261024`
- Source: `ukr8b3g-cmyk/QuotaTray`, fixed existing commit `a3a8ff9090facb63bbd446575dda2d7df1b56007` (QuantaTray 0.2.10).
- Branch: `fix/review-six-issues`. HEAD remains the existing source commit; repairs are uncommitted.
- Acquired through connected GitHub read tools. All 156 files matched their Git blob hashes; the imported tree matched `cbe7a7897b94a8afda349e9313fc3cac73d2dee4`.
- Pre-change snapshot: `D:\Codex\_snapshots\QuotaTray\pre-six-review-fixes-20261024_152931` (156/156 files verified).
- Implemented the original six fixes plus the three rereview findings (mixed token totals, late polling results, expanded retention). Final scoped rereview found no unresolved actionable finding; see WORKLOG.md.
- Additional pre-change snapshot: `D:\Codex\_snapshots\QuotaTray\pre-additional-three-fixes-20261024_160054` (162 checkout files and two workspace records verified).
- Windows/.NET SDK 10.0.201 Release build: 0 warnings, 0 errors. Dependency definitions and product version remain unchanged.
- C# results: Core 42, App 92, Integration 122 = **256 distinct tests passed, 0 failed, 0 skipped**. Final suite: 255 tests; the separate 2,000-refresh test contributes one additional case, without counting reruns twice.
- Resource check: 250 warm-up + 2,000 updates; GDI 75 -> 75, USER 170 -> 170, process handles 552 -> 548, private bytes 40,972,288 -> 42,168,320.
- Local evidence: `artifacts/review-tests/additional-three-final/` and `additional-three-full/`; original reproduction harness now passes all three expected-behavior checks in `rereview-probe/results-after-fix-final.log`. Early failing runs are preserved separately.
- Coverage metadata is optional for compatibility. Old indexes without known retained coverage are conservatively reparsed once; source sessions are unchanged.
- No new commit, push, tag, Release, installer, or portable package. Existing working copies, installed application, settings and user sessions were not replaced.
- Not run: 24/72-hour soak, real account authentication/RPC, installer lifecycle, or manual export-dialog interaction. Automated Windows tests exercise actual file saves and WinForms controls.

## Historical v0.2.8 record (reference only; not current repair status)

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
