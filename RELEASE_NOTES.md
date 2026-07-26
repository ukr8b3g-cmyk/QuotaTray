# QuantaTray v0.2.0

## 日本語

QuantaTray v0.2.0では、詳細表示を幅800 logical px固定の「概要」「使用分析」2タブ構成へ刷新しました。設定画面も同じ固定幅の13カテゴリへ統合しています。

- モデル別の総トークン、使用時間、ターン数、推論レベル、標準／高速内訳
- 初期OFF・読み取り専用・既知のCodexセッションフォルダー限定のローカル集計
- 本文、コマンド、差分、パス、ID、アカウント情報を保存しない差分走査
- 起動をまたぐ利用枠状態保存と20秒再確認によるリセット分類
- 1年／3年／5年／無期限の保持、履歴JSON・集計JSON/CSV・設定バックアップ
- テーマ、色、透明度、キャンセル復元、ミニ／コンパクト不変の回帰テスト

推定消費量は、検証済みレート表がないため表示・有効化しません。通貨換算も行いません。

## English

QuantaTray v0.2.0 rebuilds the detailed window as fixed-width Overview and Usage Analysis tabs and expands Settings to thirteen integrated categories.

- Per-model tokens, elapsed time, turns, reasoning level, and standard/fast breakdowns
- Opt-in, read-only, known-root-only local Codex session aggregation
- Incremental scanning that never stores message content, commands, diffs, paths, IDs, or account data
- Restart-safe quota-state persistence and 20-second reset confirmation
- 1/3/5-year or unlimited retention plus history, aggregate, and settings exports
- Regression coverage for appearance, opacity, cancel restore, and unchanged mini/compact behavior

## Previous v0.1.3 notes

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
- Expanded the Japanese README and clarified the project license.
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
