# QuantaTray v0.2.11

## 日本語

コードレビューと再レビューで確認した9件を修正した安定性・集計修正版です。

- 履歴JSONはファイルを閉じてから保存先へ移動し、失敗・キャンセル時の一時ファイルを整理します。保存結果を案内します
- 再確認の通信失敗や週間枠なしの結果を、リセット確認済みとして保存しません
- 履歴保存中に新しい通信結果が届いても、古い成功結果で表示を戻しません。基準値には最新の成功観測を使います
- 保持期限を集計と走査キャッシュの寄与・活動回数に適用し、通常走査やキャッシュ再構築で期限切れデータが復活しないようにします
- 保持期間を延ばした場合や無期限へ変更した場合は必要なファイルを自動再解析します。旧キャッシュも必要に応じて初回に再解析します
- 古い・未来の・別プランや枠の履歴を現在の集計期間から除外し、現在の週間枠を使います
- モデル一覧の上位件数や「その他」の設定にかかわらず、全期間のトークン・時間・ターン・推論別合計と割合を維持します
- 総トークン数が欠けたログが混在しても、入力・出力等から得られる有効総量を合算します
- 詳細画面の更新で不要になった部品と、その部品が所有するフォントを解放します

元のCodexセッションは変更しません。読み取り失敗時は互換キャッシュの範囲で前回分を維持し、不完全な結果を明示します。設定の互換性と既定値は維持します。

公開配布物はタグv0.2.11（2d7ea11）のコードからローカルで作成したInstaller／Portableです。SHA256SUMS.txtとともにアップロードし、再ダウンロードした3ファイルがローカルの元ファイルと完全一致することを確認しています。

- 検証：Windows/.NET 10で256件のC#テスト成功。元の3再現条件も解消を確認。250回の準備更新後に2,000回更新し、GDI/USERハンドル数の増加がないことを確認しました
- 配布検証：同じコミットのWindows build・CodeQLと256テストが成功。ローカル生成版のEXEバージョン・x64ペイロード・ZIP内容・SHA-256を検証し、アップロード後の再ダウンロード照合を通して公開しています
- 未検証：24/72時間の常駐試験、実アカウント接続、実機での手動DPI/複数モニター操作、インストール・上書き・アンインストールの実行
- 対応環境：Windows x64（Windows 11推奨）。公式Codex CLI/App Serverと、Codexを利用できるChatGPTアカウントが別途必要です
- Installerはユーザー単位で管理者権限不要。Portableは全体を書き込み可能なフォルダーへ展開し、隣接するdataフォルダーへ設定・履歴を保存します
- 認証はCodex App Serverが既存ログインを利用し、必要時は公式ブラウザログインを使用します。ローカル分析は請求明細や現在のコンテキスト量ではありません
- 配布物は未署名です。SmartScreen警告が出る場合があります。SHA256SUMS.txtと照合してください
- 自動更新はありません。この公開作業では利用者PCへのインストールや稼働アプリの再起動を行いません

## English

This stability and usage-accounting update fixes nine findings from code review and follow-up review.

- Close history-export files before replacing the destination on Windows; clean temporary files and report success/failure safely
- Do not confirm a reset from a failed refresh or a response without a weekly bucket
- Preserve newer polling results during history persistence and use the latest successful observation as the baseline
- Enforce retention for aggregates, cached contributions and activity counts, including unchanged scans and cache rebuilds
- Automatically reparse when retention is extended or made unlimited; older caches are reparsed on first use when necessary
- Resolve the current period from matching history within the current quota window, rejecting stale/future/unrelated events
- Keep whole-period token/time/turn/reasoning totals and model shares independent of the top-model/Other display setting
- Preserve effective totals when reported token totals are missing from part of the input
- Dispose replaced dashboard controls and their owned fonts

Original Codex sessions are unchanged. Failed files retain previous contributions only from compatible caches and remain explicitly partial. Settings compatibility and defaults are preserved.

Published assets are the installer and portable ZIP built locally from tag v0.2.11 (2d7ea11). Both packages and SHA256SUMS.txt were re-downloaded after upload and matched the local originals byte for byte.

- Validation: 256 C# tests passed on Windows/.NET 10, plus all three original reproduction checks. After 250 warm-up updates, 2,000 updates showed stable GDI/USER counts
- Release validation: build, CodeQL and all 256 Windows tests passed on the exact source commit. Locally generated EXE versions, x64 payload, ZIP contents and SHA-256 were verified, then uploaded files were re-downloaded and compared before publication
- Not tested: 24/72-hour soak, real-account operation, manual physical DPI/multi-monitor operation, or installer/upgrade/uninstaller execution
- Requirements: Windows x64 (Windows 11 recommended), official Codex CLI/App Server, and a ChatGPT account with Codex access
- Installer: per-user, no administrator rights. Portable: extract the whole ZIP to a writable folder; settings/history stay in adjacent data/
- Authentication: App Server reuses the existing Codex login, with official browser login when needed. Local analytics are best-effort usage estimates, not billing or context-occupancy figures
- Binaries are unsigned; SmartScreen may warn. Verify downloads against SHA256SUMS.txt
- No automatic updater. This publication does not install or restart the application on the user's PC

QuantaTrayは非公式ソフトウェアで、OpenAIによる承認、提携、支援、保証を受けていません。

QuantaTray is unofficial and is not affiliated with, endorsed by, sponsored by, or warranted by OpenAI.
