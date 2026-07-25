# QuantaTray v0.1.2

This release makes display settings take effect immediately.

- Opacity now changes while the slider is moving and is persisted immediately.
- Dark, light, and Windows-system themes are applied without restarting the app.
- Accent-color changes are applied to interactive controls immediately.
- Language changes rebuild visible panels, the settings window, and the tray
  menu with the selected locale.
- Existing QuantaTray settings and QuantaTrain compatibility paths remain
  supported.

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
