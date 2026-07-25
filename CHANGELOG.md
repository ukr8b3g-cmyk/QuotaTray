# Changelog

## Unreleased

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
