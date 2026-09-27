# QuantaTray v0.2.8

## 日本語

使用分析の「推論レベル内訳」で、割合が極めて小さい区分を描画する際に例外が出る問題を修正しました。描画できないほど小さい弧だけを省略し、数値と凡例は引き続き表示します。

- 対応環境：Windows x64（Windows 11推奨）。公式Codex CLI／App Serverと、Codexを利用できるChatGPTアカウントが別途必要です。
- Installer版：ユーザー単位でインストールし、管理者権限は不要です。Portable ZIP版：書き込み可能なフォルダーに全体を展開し、設定と履歴を展開先の `data` に保存します。
- 認証：QuantaTrayは `codex app-server --stdio` を使用し、Codexの既存ログインを利用します。必要な場合はCodexの公式ブラウザログインを使用してください。
- 既知の制限：Windows x64以外は未検証です。アプリ本体の自動更新はありません。App Serverから週間枠が返らない場合は推測値を表示しません。
- 配布物はコード署名されていません。SmartScreenで発行元不明と表示された場合は、Releaseの `SHA256SUMS.txt` でダウンロードファイルを検証してください。

QuantaTrayは非公式ソフトウェアで、OpenAIによる承認、提携、支援、保証を受けていません。

## English

Fixed an exception in the usage-analysis reasoning breakdown when a category's share was too small for GDI+ to draw. Only the extremely small arc is omitted; counts and legend entries remain visible.

- Requirements: Windows x64 (Windows 11 recommended), the official Codex CLI/App Server, and a ChatGPT account with Codex access.
- Installer: per-user installation without administrator rights. Portable ZIP: extract the entire archive to a writable folder; settings and history stay in the adjacent `data` folder.
- Authentication: QuantaTray uses `codex app-server --stdio` and its existing Codex login. Use the official Codex browser login if needed.
- Known limits: platforms other than Windows x64 are untested; the app has no automatic updater; no weekly estimate is shown if the App Server does not return a weekly window.
- The binaries are unsigned. If SmartScreen shows an unknown publisher, verify your download using the Release's `SHA256SUMS.txt`.

QuantaTray is unofficial and is not affiliated with, endorsed by, sponsored by, or warranted by OpenAI.
