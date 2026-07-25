# QuantaTray v0.1.1

This maintenance release corrects the product name from QuantaTrain to
QuantaTray and fixes shutdown/relaunch behavior.

- Product display name, executable, installer, portable ZIP, and data directory
  now use QuantaTray.
- Exit no longer deadlocks after hiding the tray icon, so the application can
  be launched again normally.
- Compact and detailed panels are smaller, and the detailed view has an
  explicit control to return to compact view.
- Recent reset history grows as entries are added and scrolls after three rows.
- Existing `QUANTATRAIN_CODEX_PATH` and legacy local data remain supported for
  upgrade compatibility.

- Installer: per-user installation, no administrator privileges required.
- Portable ZIP: extract to a writable folder; data stays under `data/`.
- The official Codex CLI must be installed separately.
- Existing Codex authentication is reused by App Server. If needed,
  QuantaTray opens the official ChatGPT browser login.
- This build is unsigned. Windows SmartScreen may show a warning; compare the
  file against `SHA256SUMS.txt`.
- QuantaTray is unofficial and is not affiliated with or endorsed by OpenAI.
- Weekly limits can only be displayed when Codex App Server returns a weekly
  window. Reset reasons are local inferences and are labeled as candidates.
- Setup closes a running QuantaTray instance during an in-place update and
  removes the legacy QuantaTrain installation files.
