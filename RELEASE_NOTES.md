# QuantaTray v0.2.1

## 日本語

QuantaTray v0.2.1では、v0.2.0で導入した「概要」「使用分析」画面と設定画面を本番向けに仕上げ、表示切り替え、言語、ヘルプ、設定レイアウトの問題を修正しました。

- 日本語／英語に整理し、自動選択ではWindows表示言語が日本語なら日本語、それ以外は英語を使用
- タブ、表示切り替え、使用分析、主要設定へローカライズ済みマウスオーバーヘルプを追加
- ミニ表示のクリック透過中に、表示切り替えや設定操作で透明化・消失する問題を修正
- 設定画面の保存済み高さを移行し、初回スクロールと下部コントロールの重なりを修正
- 概要、モデル別使用状況、トークン内訳、時間・ターン、推論レベルの表示を調整
- 言語、ヘルプ、透明度、スクロール、余白、重なりを検証する回帰テストを追加

推定消費量は、検証済みレート表がないため表示・有効化しません。通貨換算も行いません。

## English

QuantaTray v0.2.1 finalizes the Overview, Usage Analysis, and Settings experience introduced in v0.2.0 and fixes view switching, localization, help, and settings-layout issues.

- Japanese and English UI with Japanese-only Windows-language auto selection and English fallback
- Localized mouse-over help for tabs, view controls, usage analysis, and key settings
- Fixed mini click-through views becoming transparent or visually lost during view and Settings transitions
- Migrated the saved Settings height and fixed initial scrolling and overlapping bottom controls
- Polished overview, per-model usage, token, time/turn, and reasoning layouts
- Added regression coverage for language choices, help, opacity, scrolling, spacing, and overlap

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
