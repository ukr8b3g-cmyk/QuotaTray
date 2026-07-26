# Changelog

## Unreleased

## 0.2.1 - 2026-07-26

- Finalized the overview and usage-analysis layouts, compact presentation, colors, icons, and spacing.
- Restricted the shipped UI languages to Japanese and English, with Windows-language auto selection and English fallback.
- Added localized mouse-over help to view controls, tabs, usage-analysis fields, and key settings.
- Fixed mini click-through previews becoming transparent or visually lost while switching views or opening Settings.
- Increased and migrated the settings-window height, derived its content area from the runtime client size, and prevented initial scrollbars and overlapping controls.
- Added regression coverage for language choices, localized help, layered-window opacity, settings migration, scrolling, spacing, and overlap.

## 0.2.0 - 2026-07-26

- Rebuilt the detailed view as a fixed-width overview and per-model usage-analysis dashboard.
- Added opt-in, read-only local Codex session aggregation for model, reasoning level, service tier, token breakdown, elapsed time, and turn count.
- Added persistent offline reset detection, 20-second confirmation, classification, and restart-safe deduplication.
- Expanded Settings to thirteen categories with cancelable live previews, separate height persistence, retention, exports, diagnostics, and cache controls.
- Added atomic state, settings, aggregate, and scan-index storage with incremental append scanning and bounded retention.
- Preserved mini and compact behavior and added regression coverage for sizing, click-through, theme, accent, opacity, and localization.
- Updated the portable and installer release version to 0.2.0.

## 0.1.3 - 2026-07-25

- Expanded the Japanese README with contextual settings screenshots, recovery guidance, reset-history behavior, and issue-reporting instructions.
- Added an opt-in titleless mini view with compact/detail recovery actions.
- Added an opt-in mini-view click-through mode recoverable from the tray.
- Fixed click-through mini panels becoming faint, disappearing behind other windows, or failing to return after Settings closes.
- Serialized live settings previews and safely restored the active panel after repeated theme, accent, or language changes.
- Added shared mini/compact/detail positioning, missing-monitor recovery, and position-only/full reset actions.
- Added a clickable GitHub repository link to the About dialog.
- Fixed live theme, accent, language, and display changes that could close Settings, raise WinForms handle errors, or terminate the app.
- Kept Settings modeless so compact and detailed panels remain interactive, and restricted click-through to the mini panel.
- Fixed localized quota headings overlapping the remaining percentage.
- Applied opacity, always-on-top, position lock, position memory, and edge snap to quota panels while keeping Settings opaque.
- Applied the selected accent to normal quota values and added optional one-decimal percentage display.

## 0.1.2 - 2026-07-25

- Fixed the opacity control so changes apply while the slider is moving.
- Added live dark, light, and Windows-system theme switching.
- Added live accent-color and language switching for panels, settings, and the tray menu.
- Added runtime appearance and localization regression tests.

## 0.1.1 - 2026-07-25

- Renamed the product, executable, and release assets from QuantaTrain to QuantaTray.
- Fixed shutdown deadlock that could leave an invisible process blocking relaunch.
- Added an explicit detailed-to-compact view control and reduced both panel sizes.
- Made the recent reset history card grow only as entries are added.
- Keep compact and detailed panels open until their close button is pressed.
- Continue Codex CLI discovery past inaccessible PATH entries.
- Detect the official per-user standalone Codex CLI package cache.
- Retry Codex discovery and connection when Refresh is pressed before polling starts.

## 0.1.0 - 2026-07-25

- Added a Windows tray monitor for the Codex weekly usage limit.
- Added compact and detailed panels, reset-credit metadata, and local reset history.
- Added official Codex App Server authentication and stdio JSONL integration.
- Added ten UI languages and per-user/portable data modes.
- Added x64 installer and portable packaging automation.
- Fixed unstable selection when App Server returns multiple seven-day limits.
- Fixed corrupted settings navigation labels.
- Added graceful shutdown and installer force-close fallback for upgrades.
