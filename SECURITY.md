# Security Policy / セキュリティ方針

## Supported versions / サポート対象

Security fixes are applied to the latest published release and the current `main` branch.

セキュリティ修正は、原則として最新公開版および現在の `main` ブランチを対象にします。

## Reporting a vulnerability / 脆弱性の報告

Do not disclose suspected vulnerabilities, access tokens, authentication files, account identifiers, or private logs in a public GitHub Issue.

脆弱性の疑い、アクセストークン、認証ファイル、アカウント識別子、非公開ログを、公開GitHub Issueへ投稿しないでください。

Preferred reporting method:

1. Open the repository's **Security** tab.
2. Select **Report a vulnerability** to submit a private security advisory.
3. Include the affected version, Windows version, reproduction steps, expected and actual behavior, and the smallest redacted log or screenshot needed to understand the issue.

推奨する報告方法：

1. リポジトリの **Security** タブを開きます。
2. **Report a vulnerability** から非公開のSecurity Advisoryを作成します。
3. 対象バージョン、Windowsバージョン、再現手順、期待結果と実際の結果、確認に必要な最小限のマスキング済みログまたは画像を記載します。

If private vulnerability reporting is unavailable, open a public Issue containing only a request for a private contact channel. Do not include exploit details or secrets in that Issue.

非公開報告機能が利用できない場合は、公開Issueには「非公開連絡方法を希望する」旨だけを記載し、攻撃手順や機密情報を含めないでください。

## Sensitive information / 機密情報

Before submitting any material, remove or mask:

- OpenAI/ChatGPT/Codex access tokens and authentication files
- Browser cookies and saved passwords
- Email addresses and account identifiers
- Local user names and private filesystem paths when not essential
- Conversation content, source code, project files, and repository secrets

報告前に、次の情報を削除またはマスキングしてください。

- OpenAI/ChatGPT/Codexのアクセストークンおよび認証ファイル
- ブラウザCookieおよび保存パスワード
- メールアドレスおよびアカウント識別子
- 調査に不要なローカルユーザー名や非公開パス
- 会話内容、ソースコード、プロジェクトファイル、リポジトリの秘密情報

## Scope / 対象範囲

Examples of in-scope issues include:

- Exposure or persistence of credentials or raw account responses
- Unintended network communication or telemetry
- Arbitrary command execution, unsafe process launching, or path injection
- Local data disclosure through logs, history, installer, or portable packaging
- Security-relevant weaknesses in Codex App Server process handling

対象例：

- 認証情報や生のアカウント応答の露出・保存
- 意図しない外部通信やテレメトリー
- 任意コマンド実行、安全でないプロセス起動、パス注入
- ログ、履歴、インストーラー、ポータブル配布物からのローカル情報漏えい
- Codex App Serverのプロセス管理に関するセキュリティ上の問題

General UI defects, feature requests, and non-sensitive crashes may be reported through regular [GitHub Issues](https://github.com/ukr8b3g-cmyk/QuotaTray/issues).

一般的なUI不具合、機能要望、機密性のないクラッシュは通常の [GitHub Issues](https://github.com/ukr8b3g-cmyk/QuotaTray/issues) を利用してください。

## Response expectations / 対応方針

Reports will be reviewed on a best-effort basis. Receipt will be acknowledged when possible, the issue will be assessed, and a fix or mitigation will be prepared before public disclosure when the report is valid.

報告は可能な範囲で確認し、受領連絡、影響評価、修正または緩和策の準備を行います。有効な報告については、原則として修正準備前の公開を避けます。

## Project security posture / 本プロジェクトの方針

QuantaTray is designed as a read-only, local-first utility. It does not directly read browser cookies, saved passwords, Codex credential files, conversations, or project files. It has no developer-operated backend, telemetry, advertising, or analytics, and it does not call the reset-credit consumption API.

QuantaTrayは読み取り専用・ローカル中心の設計です。ブラウザCookie、保存パスワード、Codex認証ファイル、会話、プロジェクトファイルを直接読み取りません。開発者運営サーバー、テレメトリー、広告、利用解析はなく、リセット券消費APIも呼び出しません。
