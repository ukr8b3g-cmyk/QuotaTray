# QuantaTray v0.2.10

## 日本語

使用分析が空のままになる不具合と、有効なのに無効と案内される表示を修正しました。

- 圧縮ログの展開上限超過・破損などをファイル単位で扱い、他の読み取れるファイルの集計・保存を続けます
- 512 MiBの展開上限、64 MiBのデコーダーウィンドウ上限、4 MiBのメタデータ行上限は維持します
- 収集中・未収集・取得失敗・空の結果・不完全な結果を区別し、更新失敗時は前回の表示結果を維持します
- 設定画面を収集中に開いても、現在の状態を表示します

重要：読み取れなかったファイルの過去分を使えるのは、互換性のある走査キャッシュがある場合だけです。旧形式キャッシュの再構築時などは、そのファイルの集計が除外されます。「不完全な結果」は全履歴の復元を意味しません。元のCodexセッションファイルを変更することはありません。

- 検証：Windows CIの全テストに加え、512 MiBを超える合成圧縮データ、他ファイルの継続集計・保存、キャッシュ・キャンセル・表示状態の回帰テストを実行します。リリース工程でEXEバージョン、x64ペイロード、ZIP内容、SHA-256とアップロード後の再ダウンロードを検証します
- 対応環境：Windows x64（Windows 11推奨）。公式Codex CLI／App Serverと、Codexを利用できるChatGPTアカウントが別途必要です
- Installer版：ユーザー単位、管理者権限不要。Portable版：書き込み可能なフォルダーへ全体を展開し、設定と履歴を隣接するdataフォルダーに保存します。.NETのネイティブ実行ファイルは起動時に一時フォルダーへ展開される場合があります
- 認証：Codex App Serverが既存のCodexログインを利用します。必要な場合は公式ブラウザログインを使用してください
- 制限：ローカル使用分析は参考集計で、請求明細や現在のコンテキスト量ではありません。フォークされた履歴などで過大・過小集計する場合があります。実アカウント接続、実機UI／DPI、新規・上書きインストール／アンインストール、長時間メモリ試験は未検証です
- 配布物は未署名です。Windows SmartScreenで発行元不明の警告が出る場合があります。同梱のSHA256SUMS.txtと照合してください
- 自動更新はありません。利用者のPCへのインストール・アプリ再起動はこの公開作業では行いません

## English

Fixes local usage analysis remaining empty and the misleading disabled message when collection is enabled.

- Isolate oversized or damaged compressed files so other readable files are still aggregated and persisted
- Preserve the 512 MiB output, 64 MiB decoder-window and 4 MiB metadata-line safety limits
- Distinguish waiting, scanning, failure, empty and partial results; retain the previous display snapshot when refresh fails
- Show current scan state when Settings opens during a scan

Important: a failed file's previous contribution is retained only when a compatible scan cache exists. Rebuilding an old incompatible cache can exclude that file's contribution. Partial results do not mean all historical totals were recovered. Original Codex session files are never modified.

- Validation: all Windows CI tests plus synthetic regressions for output beyond 512 MiB, continued scanning/persistence, caches, cancellation and UI states. The release pipeline verifies executable versions, x64 payload, ZIP contents, SHA-256 and re-downloaded uploaded assets
- Requirements: Windows x64 (Windows 11 recommended), official Codex CLI/App Server, and a ChatGPT account with Codex access
- Installer: per-user, no administrator rights. Portable: extract the entire ZIP to a writable folder; settings/history stay in adjacent data/. Native .NET runtime files may be extracted to a temporary directory at launch
- Authentication: App Server reuses the existing Codex login; use the official browser login when needed
- Limits: local analytics are best-effort, not billing or current-context figures. Forked or incomplete histories can overcount or undercount. Signed-in operation, physical UI/DPI, install/upgrade/uninstall execution and long-running memory tests remain unverified
- Binaries are unsigned; Windows SmartScreen may warn about an unknown publisher. Verify downloads against SHA256SUMS.txt
- No automatic updater. This publication does not install or restart the app on the user's computer

QuantaTrayは非公式ソフトウェアで、OpenAIによる承認、提携、支援、保証を受けていません。

QuantaTray is unofficial and is not affiliated with, endorsed by, sponsored by, or warranted by OpenAI.
