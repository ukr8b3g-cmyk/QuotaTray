# Work log

## 2026-09-27 — v0.2.8 reasoning breakdown hotfix

- Reproduced the reported `UsageDonutControl.OnPaint` `DrawArc` exception with a 0.001-degree token share and added a regression test.
- Skipped subpixel arcs while preserving the chart totals and legend. The focused test failed before the fix and passed afterward.
- Saved pre-change snapshots at `D:\Codex\_snapshots\QuotaTray\pre-reasoning-donut-20260927_114914` (2/2 files) and `D:\Codex\_snapshots\QuotaTray\pre-v028-setup-20260927_115811` (24/24 files).
- Ran the full Release test suite: 96 passed, 0 failed. Built the v0.2.8 setup and portable ZIP from source commit `006e334`.
- Verified installer and EXE version metadata, two SHA-256 entries, ten ZIP entries, and ZIP/publish EXE hash equality. The installer is unsigned; the running installed copy was not replaced.
- Saved documentation snapshot at `D:\Codex\_snapshots\QuotaTray\pre-v028-publication-20260927_120902` (4/4 files matched).
- Updated README and release notes, pushed `1afb860` to main, tagged `v0.2.8`, and published GitHub Release after workflow `36290691781` passed.
- Downloaded the published installer, portable ZIP, and `SHA256SUMS.txt`; both assets matched their checksums, the ZIP contained ten entries, and the EXE reported version `0.2.8+1afb860`. GitHub `latest` resolves to v0.2.8.

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
