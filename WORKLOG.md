# Work log

## 2026-10-24 - QuantaTray 0.2.11 published from local packages

- Committed all nine reviewed fixes, regression tests, version metadata, README,
  changelog, Release Notes and restricted publication workflow as
  `2d7ea11c57269a481046e050eb3b7bf73aae59d3`.
  Connected GitHub Git-data tools created the commit and advanced main with an
  expected-head lease; the local checkout imported the identical commit/tree.
- Exact-commit build `40073956486`, CodeQL `40073955912` and release workflow
  `40073956435` succeeded. Release-job logs independently confirmed 42 Core,
  92 App and 122 Integration tests passed, with zero failures/skips.
- The user requested the usual local setup-build/upload procedure. Rebuilt setup
  and portable packages locally from the tag commit, using .NET SDK 10.0.201 and
  Inno Setup 6.7.3. Version, Windows x64, notices, ZIP contents and checksums passed.
  Did not run the installer or send shutdown to the installed application.
- Saved the initial CI assets before replacing only v0.2.11 assets. The connected
  GitHub tools do not expose binary Release upload, so existing GitHub CLI was used
  for the draft/upload/download/republication step. No git-remote-https was used.
- Re-downloaded setup, portable ZIP and SHA256SUMS.txt from the draft and verified
  byte-for-byte equality with local originals, plus GitHub size/digest equality,
  before republishing v0.2.11 as Latest.
- Final setup: 46,983,696 bytes, SHA-256
  `18e4d4c11241d037f5eb37bff6e799bd75efe3228af3f89c00257e6f6b44e40b`.
- Final portable: 46,613,470 bytes, SHA-256
  `efbcc9c2af77815f12958afb0b35f9b85691e62712dcb2dcdc9f5b7022a0704d`.
- SHA256SUMS.txt SHA-256:
  `61b6f744a8ee00e9f83febcbe608c9ae3fe1350cb1ca90894c9286dec24c8577`.
- Connected GitHub reads verified public/Latest state, tag pointing to the release
  commit, final asset digests, and unchanged v0.2.10 assets. Previous releases,
  installed applications, user settings and original Codex sessions were preserved.
- Public Release:
  https://github.com/ukr8b3g-cmyk/QuotaTray/releases/tag/v0.2.11
- Full-day soak, real-account operation, manual physical DPI/multi-monitor checks
  and install/upgrade/uninstall execution remain unverified. CI host smoke applied
  to CI-generated binaries; final local packages received metadata/content/hash
  checks without launching the host on the user's PC.

## 2026-10-24 - QuantaTray 0.2.11 release preparation

- User explicitly authorized committing and publishing the reviewed nine fixes as
  QuantaTray 0.2.11. Verified connected GitHub main still equals the reviewed base
  `a3a8ff9090facb63bbd446575dda2d7df1b56007`; v0.2.11 did not exist.
- Saved 162/162 checkout files plus two workspace records at
  `D:\Codex\_snapshots\QuotaTray\pre-v0211-release-20261024_161505`.
- Updated Version, installer default, README download links and explanations,
  the nine-item changelog, and bilingual Release Notes with exact validation gaps.
- Versioned local Release build passed with zero warnings/errors, and all 255
  normal tests passed. Local installer/portable metadata, x64 payload, notices,
  archive contents and checksums passed packaging/scripts/verify-release.ps1.
- Added a local SkipHostSmoke option because the existing --shutdown smoke path
  can signal an already-running user copy. Local validation did not launch or
  terminate an application; clean CI still runs the host smoke check.
- The one-shot publication trigger requires both push-before and commit parent
  to equal the reviewed base; future normal main updates cannot execute its job.
- Public packages will be rebuilt and verified by GitHub Actions from the exact
  release commit, after successful build and CodeQL, before publishing.

## 2026-10-24 - Additional three findings fixed and rereviewed (uncommitted)

Continued in the same repair checkout and feature branch, preserving the original
six fixes. Before editing, saved and hash-verified 162 checkout files plus the two
workspace records at
`D:\Codex\_snapshots\QuotaTray\pre-additional-three-fixes-20261024_160054`.

- **Mixed totals:** UsageTokenTotals addition now adds each operand's effective
  total, preserving the input/output fallback before aggregation can erase it.
  This covers turns sharing one model/bucket as well as different models, without
  counting cached-input or reasoning-output subsets twice.
- **Late polling results:** Each actual refresh receives an attempt sequence at
  request start, including failures and in-flight updates. ResetConfirmation
  rechecks that sequence after async persistence before updating the view.
  Its baseline uses the newest successful observation from the same connection;
  a later failure cannot discard a valid reset baseline or trigger duplicate
  recovery detection. Account/connection generation guards remain in place.
- **Expanded retention:** Each cache entry records its retained date boundary and
  whether coverage is known. Both unchanged-file reuse and append-only parsing
  require adequate coverage. Widening 365 days to 1095 days or unlimited retention
  reparses the source automatically, including after restart. Filtering can only
  narrow coverage, never claim that deleted rows have been restored.
- Failed reparses retain compatible previous data with an explicit partial-result
  count and insufficient coverage, so the next scan retries. Legacy cache entries
  without coverage metadata are reparsed conservatively once. No original Codex
  session is modified, and no dependency/version/settings definition changed.

Validation and rereview:

- Added 14 regression cases first; all 14 failed against the prior repair.
  All pass after the fixes. Added two further in-flight-update boundary cases
  during rereview, giving 16 new cases in total.
- Coverage includes same/different models, both GroupOther settings, UI footer/
  percentages/donut consistency; new success/failure/updating states during history
  and baseline persistence; expanded/unlimited retention with/without restart,
  locked-source retry, legacy metadata, later narrowing and unchanged source bytes.
- Re-ran the original DLL-based reproduction harness: 150 -> 150 effective total,
  67% for the 100-token model, later timeout display preserved, and two rows after
  widening retention without manual rebuilding. All three checks pass; the
  before/after logs remain in `artifacts/review-tests/rereview-probe/`.
- Final C# suite: Core 42 + App 91 + Integration 122 = 255 passing cases. The
  separately completed resource case gives **256 distinct tests**, zero failures/
  skips. Repeated runs and the standalone harness are not added to that count.
- Resource test: 250 warm-up plus 2,000 updates; GDI 75 -> 75, USER 170 -> 170,
  process handles 552 -> 548, private bytes 40,972,288 -> 42,168,320.
- Final Release build: zero warnings/errors. `git diff --check` and forbidden
  runtime source-pattern checks passed.
- Rereview checked effective-total accumulation through parser/aggregate/UI,
  async result freshness around persistence and the production callbacks, and
  cache coverage across pruning, expansion, appending, restart and failed parses.
  No unresolved actionable finding remains in this change scope.

No commit, remote write, tag, package publication, installed-app replacement or
real-account connection was performed. The HEAD remains the fixed existing
`a3a8ff9090facb63bbd446575dda2d7df1b56007`. 24/72-hour soak and installer lifecycle
remain untested. TRX evidence is in `artifacts/review-tests/additional-three-final/`
and `additional-three-full/`.

## 2026-10-24 - Six review fixes for QuantaTray 0.2.10 (uncommitted)

Worked from existing commit `a3a8ff9090facb63bbd446575dda2d7df1b56007` in
`D:\Codex\Internal\Active\quantatray-review-fixes-20261024`,
branch `fix/review-six-issues`. All 156 acquired files matched Git blob hashes.
Snapshot: `D:\Codex\_snapshots\QuotaTray\pre-six-review-fixes-20261024_152931`
(156/156 verified). Existing 0.2.1/0.2.8 checkouts were preserved. Importing the
existing source commit did not create a new commit.

Applied in the recommended order:

1. **P2-01:** HistoryExportService disposes writer/stream before destination
   replacement and cleans temporary files. The app catches failures and reports
   success/failure in Japanese/English; diagnostics contain only the exception type.
2. **P2-02:** Polling returns explicit success/error and a successful-observation
   sequence. ResetConfirmation rejects failed/cached/older observations and changed
   limit/window/plan/connection/account generation. Failure cannot write confirmed
   history, change the baseline or clear the failure display.
3. **P2-03:** Retention maintenance and scans share the scanner gate; aggregate
   pruning shares the store gate. Every scan filters results, index contributions
   and activity counts using local aggregate dates, including unchanged-cache and
   rebuild paths. Original Codex session files are never modified.
4. **P2-05:** Current-period resets must be confirmed, within the current window,
   no later than now, and match the quota limit/window/plan. Otherwise the current
   quota window or seven-day fallback is used with an estimated-start flag.
5. **P2-06:** Whole-period token/time/turn/reasoning totals use all snapshot rows.
   Model ranking/grouping only affects presentation. Shares use the full-period
   denominator. The existing total row identifies truncated model lists without
   enlarging the panel.
6. **P2-04:** All nine dashboard Controls.Clear sites now dispose removed controls
   and descendants. Dynamic labels and credit-card labels release their owned
   fonts without disposing inherited/shared fonts.

Regression results on Windows/.NET 10.0.201:

- History: empty/multi-month/damaged-row input; new and replacement save; locked
  destination, cancellation and missing destination directory.
- Polling/confirmation: timeout, missing weekly bucket, disconnection, identical
  fresh values, coalescing, reversal, sustained recovery, older/cache results,
  changed account/connection/limit/plan and shutdown. Tests cover production
  application orchestration callbacks, not only the classifier.
- Retention: maintenance -> cached scan -> restarted scanner -> cache rebuild;
  unlimited retention, local-date boundary, advancing cutoff, activity rows and
  concurrent scan/maintenance. Synthetic source-file bytes remain unchanged.
- Period: old/future/unconfirmed history, different limit/plan/window, valid
  scheduled/candidate reset and absent history.
- Before the totals fix, the UI regression reproduced 280 vs 270 with 7 models;
  20 models also failed and the 6-model control passed. Afterward totals, reasoning,
  time, turns, shares and panel bounds pass for all three conditions.
- Before the disposal fix, 67/67 old control descendants remained undisposed.
  Afterward all are disposed, including enabled/disabled transitions.
- Release build: 0 warnings, 0 errors. An inherited LIB value referenced unrelated
  absent directories; it was cleared only for build/test commands, not globally.
- Final suite: Core 40 + App 83 + Integration 116 = 239 passed. The separately
  executed 2,000-refresh test adds one distinct passing test: **240 total**, zero
  failures/skips. Focused reruns are not counted again.
- Resource test: 250 warm-up + 2,000 updates; GDI 75 -> 75, USER 170 -> 170,
  process handles 658 -> 654, private bytes 45,916,160 -> 41,652,224.
- TRX files: `artifacts/review-tests/`. Forbidden runtime patterns and
  `git diff --check` passed.

No commit, remote write, Release, package installation, running-app replacement
or real-account test. 24/72-hour soak, installer lifecycle and manual export-dialog
interaction remain unverified.

## 2026-09-27 — v0.2.8 reasoning breakdown hotfix

- Reproduced the reported `UsageDonutControl.OnPaint` `DrawArc` exception with a 0.001-degree token share and added a regression test.
- Skipped subpixel arcs while preserving the chart totals and legend. The focused test failed before the fix and passed afterward.
- Saved pre-change snapshots at `D:\Codex\_snapshots\QuotaTray\pre-reasoning-donut-20260927_114914` (2/2 files) and `D:\Codex\_snapshots\QuotaTray\pre-v028-setup-20260927_115811` (24/24 files).
- Ran the full Release test suite: 96 passed, 0 failed. Built the v0.2.8 setup and portable ZIP from source commit `006e334`.
- Verified installer and EXE version metadata, two SHA-256 entries, ten ZIP entries, and ZIP/publish EXE hash equality. The installer is unsigned; the running installed copy was not replaced.
- Saved documentation snapshot at `D:\Codex\_snapshots\QuotaTray\pre-v028-publication-20260927_120902` (4/4 files matched).
- Updated README and release notes, pushed `1afb860` to main, tagged `v0.2.8`, and published GitHub Release after workflow `36290691781` passed.
- Downloaded the published installer, portable ZIP, and `SHA256SUMS.txt`; both assets matched their checksums, the ZIP contained ten entries, and the EXE reported version `0.2.8+1afb860`. GitHub `latest` resolves to v0.2.8.
- Archived the pre-tag local assets at `D:\Codex\_snapshots\QuotaTray\pre-v028-published-assets-20260927_121755` (3/3 verified), then synchronized local `dist` with the three published assets and matched all three hashes. Saved a 2/2 state-document snapshot before updating this record.

## 2026-08-28 — v0.2.7 local implementation

- Created pre-change snapshot at `D:\Codex\_snapshots\QuotaTray\pre-v0.2.7-20260828_123857-be39c92` (142/142 files verified).
- Implemented larger tray artwork, four-row recent history, and modeless reusable full history.
- Added read-only purchased-credit and official account token usage parsing and display.
- Added opt-in privacy-limited local plugin/tool and skill counters.
- Expanded the detailed usage dashboard and settings pages with DPI-aware vertical scrolling.
- Added schema 6 migration, localization keys, v0.2.7 metadata, parser/UI/migration/privacy tests, and release documentation.
- Built the self-contained local portable package and completed 94 passing tests plus screenshot QA.
- Added a visible bottom resize grip to the detailed window; it remains usable while panel position is locked and preserves the remembered logical height.
- Reduced interactive-resize flashing by deferring rounded-window region regeneration until the resize operation finishes.
- Updated the v0.2.7 installer to preserve `%LocalAppData%\QuantaTray` and create a one-time `settings.json.pre-v0.2.7.bak` safety copy before installation.
- Fixed panel switching so Mini, Compact, and Detail remember independent positions; added schema 7 migration from the legacy shared position and verified 95 passing tests.
- Rebuilt the v0.2.7 setup and portable ZIP with the per-mode position fix; checksums are recorded in `PROJECT_STATE.md` and `dist\SHA256SUMS.txt`.
- Prepared the final README and one-shot public GitHub Release workflow for the formal v0.2.7 launch.
- Published v0.2.7 at commit/tag `2eafe79`, then replaced the README usage-analysis screenshot and expanded its feature explanation from the verified final UI.
