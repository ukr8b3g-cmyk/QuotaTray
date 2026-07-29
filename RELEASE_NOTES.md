# QuantaTray v0.2.2

## 日本語

QuantaTray v0.2.2では、詳細画面のリセット履歴にある「すべて表示」が、保存期間内の履歴をすべて表示するよう修正しました。

- 「すべて表示」を押すと、保持中の月別履歴ファイルをすべて読み込む一覧画面を表示
- 履歴を新しい順で表示
- 破損した行だけを無視し、同じファイル内の正常な履歴は継続して表示
- コンパクト表示と詳細画面の「最近の履歴」件数は従来どおり維持
- 複数月、破損行、最近の履歴件数、ボタン操作の回帰テストを追加

既に保存期間の処理で削除された履歴は復元されません。ローカルの履歴JSONLに残っている記録が表示対象です。

## English

QuantaTray v0.2.2 fixes the reset-history **Show all** action so it displays every retained local history record.

- Opens a dedicated list containing all retained monthly history files
- Sorts records newest first
- Skips only damaged rows while continuing to read valid rows from the same file
- Preserves the existing recent-history limits in compact and detailed views
- Adds regression tests for multiple months, damaged rows, recent-count behavior, and the Show all action

History already removed by retention cleanup cannot be restored. The dialog displays records still present in the local history JSONL files.

- Installer: per-user installation, no administrator privileges required.
- Portable ZIP: extract to a writable folder; data stays under `data/`.
- The official Codex CLI must be installed separately.
- Existing Codex authentication is reused by App Server.
- This build is unsigned. Windows SmartScreen may show a warning; compare the file against `SHA256SUMS.txt`.
- QuantaTray is unofficial and is not affiliated with or endorsed by OpenAI.
