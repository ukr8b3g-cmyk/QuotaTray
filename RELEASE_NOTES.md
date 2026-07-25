# QuantaTray v0.1.3

This release adds the mini view and stabilizes live display changes.

- Added an opt-in titleless mini view and mini-only click-through mode.
- Fixed click-through mini panels becoming faint, disappearing behind other
  windows, or failing to return after Settings closes.
- Restores click-through mini panels without taking keyboard focus.
- Serializes live Settings previews so repeated theme, accent-color, and
  language changes cannot overlap panel reconstruction.
- Added shared mini/compact/detail positioning, missing-monitor recovery, and
  position reset actions.
- Added optional one-decimal quota display when Codex returns fractional data.
- Expanded the Japanese README and changed the project license to CC0 1.0.
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
