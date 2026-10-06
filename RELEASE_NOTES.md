# QuantaTray v0.2.9

## 日本語

Codex 0.160.1の公開仕様とログ形式に合わせた互換性修正版です。

- 週間利用枠やリセット券の任意項目がnull・未指定・不正でも、取得できた週間枠を維持します
- 応答単位の使用量を優先し、旧形式の重複通知やコンテキスト推定値による加算を抑えます
- 新形式の設定記録からサービスタイプを読み取ります
- 任意の圧縮ログ（.jsonl.zst）に対応し、通常ログとの二重集計を避けます
- 書き込み途中のJSONL／UTF-8行を次回スキャンで再読込します
- アカウント使用量の取得失敗・タイムアウトで前回値を保持し、更新処理の例外終了を防ぎます

配布では、自己完結型のWindows x64インストーラーとポータブルZIPを用意し、ネイティブ.NETライブラリの同梱とライセンス表示も整備しました。

- 検証：修正実装はWindows CIで173件（追加の互換性回帰テスト77件を含む）成功。リリース工程でもビルド・全テスト、EXEのバージョンとx64ペイロード、ZIP内容、SHA-256を検証します
- 対応環境：Windows x64（Windows 11推奨）。公式Codex CLI／App Serverと、Codexを利用できるChatGPTアカウントが別途必要です
- Installer版：ユーザー単位、管理者権限不要。Portable版：書き込み可能なフォルダーへ全体を展開し、設定と履歴を隣接するdataフォルダーに保存します。.NETのネイティブ実行ファイルは起動時に一時フォルダーへ展開される場合があります
- 認証：Codex App Serverが既存のCodexログインを利用します。必要な場合は公式ブラウザログインを使用してください
- 制限：ローカル使用分析は参考集計であり、請求明細や現在のコンテキスト量ではありません。フォークされた履歴などで過大・過小集計する場合があります。実アカウントでの0.160.1動作、今回の実機UI／DPI確認、新規・上書きインストール／アンインストール、長時間メモリ試験は未検証です
- 配布物は未署名です。Windows SmartScreenで発行元不明の警告が出る場合があります。ダウンロードファイルは同梱のSHA256SUMS.txtと照合してください
- 自動更新はありません。App Serverから週間枠が返らない場合は推測値を表示しません

## English

Compatibility fixes for the public Codex 0.160.1 schema and rollout formats:

- Preserve valid weekly quota data when optional quota-window or reset-credit fields are null, absent, or malformed
- Prefer observed per-response usage and avoid adding duplicate legacy snapshots or context-only estimates
- Read service tier from newer thread-settings records
- Support optional .jsonl.zst logs with bounded decompression and deduplicate raw/compressed siblings
- Retry incomplete JSONL and UTF-8 rows on the next scan
- Retain the last account-usage snapshot after RPC failures/timeouts and contain refresh exceptions safely

The Windows x64 installer and self-contained portable ZIP also include complete publish dependencies, native .NET bundling, and third-party notices.

- Validation: the implementation passed 173 Windows CI tests, including 77 new compatibility regressions. The release pipeline repeats build/tests and verifies executable versions, x64 payload, ZIP contents, and SHA-256 hashes
- Requirements: Windows x64 (Windows 11 recommended), official Codex CLI/App Server, and a ChatGPT account with Codex access
- Installer: per-user, no administrator rights. Portable: extract the entire ZIP to a writable folder; settings/history stay in adjacent data/. Native .NET runtime files may be extracted to a temporary directory at launch
- Authentication: App Server reuses the existing Codex login; use the official browser login when needed
- Limits: local analytics are best-effort, not billing or current-context figures. Forked or incomplete histories can overcount or undercount. Signed-in Codex 0.160.1 behavior, current physical UI/DPI checks, install/upgrade/uninstall execution, and long-running memory tests remain unverified
- Binaries are unsigned; Windows SmartScreen may warn about an unknown publisher. Verify downloads against SHA256SUMS.txt
- No automatic updater. No weekly estimate is invented when App Server does not return a weekly window

QuantaTrayは非公式ソフトウェアで、OpenAIによる承認、提携、支援、保証を受けていません。

QuantaTray is unofficial and is not affiliated with, endorsed by, sponsored by, or warranted by OpenAI.
