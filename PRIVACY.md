# Privacy Policy / プライバシー方針

最終更新：2026-07-26

## 要約

QuantaTrayはローカル中心の読み取り専用アプリです。開発者運営サーバー、テレメトリー、広告、外部送信される利用解析はありません。Codex利用枠を取得するため、OpenAI公式Codex App Serverを介してOpenAI/ChatGPTへ通信します。

本アプリはOpenAIの公式製品ではなく、OpenAIによる承認、提携、保証を受けたものではありません。

## QuantaTrayが取得・保存しない情報

QuantaTrayは次の情報を独自に取得、保存、収集、外部送信しません。

- ChatGPT/OpenAIのパスワード
- ブラウザCookie、保存パスワード、閲覧履歴
- Codexのアクセストークンや認証ファイルの内容
- メールアドレス、アカウントID
- ChatGPT/Codexの会話内容
- ソースコード、プロジェクトファイル、リポジトリ内容
- キー入力、画面内容
- 広告識別子、端末指紋

## 認証

QuantaTrayはOpenAI公式Codex App Serverに認証状態の確認を依頼します。既存のCodex認証キャッシュが有効な場合、App Serverがその認証を再利用します。未認証の場合はOpenAI公式ブラウザログインを開始します。

QuantaTray自身が `auth.json`、OS資格情報ストア、Chrome/Edge等のCookieやパスワードを直接読み取ることはありません。

## 通信

通常動作の通信経路：

```text
QuantaTray
  └─ ローカルstdio JSONL → codex app-server
       └─ HTTPS → OpenAI / ChatGPT
```

QuantaTrayには開発者運営のAPI、テレメトリー送信先、広告サーバーはありません。Codex、ChatGPT、ドキュメント、GitHub Release等の外部リンクはユーザー操作時だけ既定ブラウザで開きます。

将来、自動更新確認、クラッシュ送信、追加の外部通信を実装する場合は、実装前に本方針と設定画面を更新し、初期OFFまたは明示同意方式にします。

## ローカル保存する情報

- 表示・言語・通知等の設定
- ウィンドウ位置と選択モニター
- 利用枠の残量、次回リセット、リセット券件数・期限の観測値
- 検出したリセット履歴
- 接続障害を調査するための限定・マスキング済みログ
- 使用者が明示的に有効化した場合に限り、ローカルCodexセッションから集計したモデル、推論レベル、標準/高速区分、トークン数、使用時間、ターン数
- 使用分析の差分走査用インデックス（元ファイルパスではなくSHA-256、サイズ、更新時刻、境界シグネチャ）

認証トークン、Cookie、パスワード、アカウント識別子、生のアカウント応答は保存しません。

## 任意のローカル使用分析

使用分析は初期状態で無効です。設定画面で明示的に有効化した場合だけ、`CODEX_HOME\sessions` と、設定に応じて `CODEX_HOME\archived_sessions` を読み取ります。任意のフォルダーを対象にする機能はありません。

走査時は、モデル、推論レベル、サービスタイプ、トークン数、開始・完了時刻に関係する既知のイベント行だけを処理します。メッセージ本文、ツール出力、コマンド、差分、作業ディレクトリ、リポジトリパス、セッションID、アカウント情報は集計・保存しません。セッションファイルを変更せず、リセット券使用を含む書き込みAPIも呼び出しません。

集計結果と走査キャッシュは端末内だけに保存され、QuantaTrayまたは開発者のサーバーへ送信されません。設定から機能を無効化すると以後の走査を行いません。

## 保存場所

- インストーラー版：`%LOCALAPPDATA%\QuantaTray\`
- ポータブル版：アプリフォルダー内の `data\`

## 保持と削除

新規インストールの履歴・使用集計の既定保持期間は3年です。既存設定の365日は移行時に維持されます。保存期間は1年、3年、5年、無期限から選択でき、期限を超えた行だけを起動時かつ24時間に1回以下で整理します。診断ログは別の保存期間で管理します。設定画面から履歴・集計・設定を書き出せます。

## 予定外リセットの推定

OpenAIから理由が返されない場合、QuantaTrayは残量、予定リセット、リセット券件数等の変化から「予定外リセット候補」を推定します。この分類はローカル処理であり、OpenAIによる実行を断定するものではありません。

## セキュリティ報告

脆弱性または認証情報の露出につながる問題は、公開Issueへ詳細を書き込まず、[SECURITY.md](SECURITY.md) の手順に従ってください。

## ライセンスと商標

ソースコードは [MIT License](LICENSE) で提供されます。MIT Licenseは、OpenAI、ChatGPT、Codex、QuantaTrayその他の名称、ロゴ、商標に対する使用許可を与えるものではありません。

OpenAI、ChatGPT、Codexは各権利者の商標です。

## 問い合わせ

一般的な不具合・要望は [GitHub Issues](https://github.com/ukr8b3g-cmyk/QuotaTray/issues) へお願いします。機密性のあるセキュリティ問題は [SECURITY.md](SECURITY.md) を参照してください。

---

## English summary

QuantaTray is a local-first, read-only Windows utility. It has no developer-operated backend, telemetry, advertising, or externally transmitted analytics. It communicates with OpenAI only through the separately installed official Codex App Server.

Optional local usage analysis is off by default. When explicitly enabled, it scans only known Codex session roots and stores aggregate model, reasoning, service-tier, token, elapsed-time, and turn-count metadata plus a path-hashed incremental index. It never stores message content, commands, diffs, repository paths, session identifiers, or account data, and sends no aggregate data to the developer.

QuantaTray does not directly read or store browser cookies, saved passwords, Codex credential files, access tokens, account identifiers, conversations, source code, or project files. It stores only local settings, observed quota state, inferred reset history, optional aggregate usage metadata, and bounded redacted diagnostics.

QuantaTray is unofficial and is not affiliated with, endorsed by, or warranted by OpenAI. See [SECURITY.md](SECURITY.md) for private vulnerability reporting guidance and [LICENSE](LICENSE) for the MIT License.
