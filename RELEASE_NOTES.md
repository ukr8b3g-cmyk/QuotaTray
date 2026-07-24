# QuantaTrain v0.1.0

Initial public build for Windows 11 x64. Best-effort support is provided for
supported Windows 10 x64 editions where .NET 10 and Codex CLI run.

- Installer: per-user installation, no administrator privileges required.
- Portable ZIP: extract to a writable folder; data stays under `data/`.
- The official Codex CLI must be installed separately.
- Existing Codex authentication is reused by App Server. If needed,
  QuantaTrain opens the official ChatGPT browser login.
- This build is unsigned. Windows SmartScreen may show a warning; compare the
  file against `SHA256SUMS.txt`.
- QuantaTrain is unofficial and is not affiliated with or endorsed by OpenAI.
- Weekly limits can only be displayed when Codex App Server returns a weekly
  window. Reset reasons are local inferences and are labeled as candidates.
- Multiple exact seven-day limit buckets are selected deterministically to
  prevent the displayed value from switching between unrelated limits.
- Setup closes a running QuantaTrain instance during an in-place update.
- The settings navigation uses standard text controls to avoid corrupted labels
  on affected Windows display configurations.
