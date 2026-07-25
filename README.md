# QuantaTray

[English](#english) · [Windows Installer (.exe)](https://github.com/ukr8b3g-cmyk/QuotaTray/releases/latest/download/QuantaTray-v0.1.3-win-x64-setup.exe) · [Portable ZIP](https://github.com/ukr8b3g-cmyk/QuotaTray/releases/latest/download/QuantaTray-v0.1.3-win-x64-portable.zip) · [SHA-256](https://github.com/ukr8b3g-cmyk/QuotaTray/releases/latest/download/SHA256SUMS.txt)

OpenAI非公式の、Codex利用枠を確認するWindowsタスクトレイ常駐モニターです。

<img width="1010" height="594" alt="QuantaTray UI" src="https://github.com/user-attachments/assets/899a2de1-946b-43cd-a1bc-7b869800a89c" />

> 画像はUI構成を示すモックアップです。表示内容はCodex App Serverから取得できる情報によって変わります。

## 重要事項

QuantaTrayはOpenAIの公式製品ではなく、OpenAIによる承認、提携、支援、保証を受けたものではありません。OpenAI、ChatGPT、Codexは各権利者の商標です。

本プロジェクトのソースコードは [MIT License](LICENSE) で提供されます。MIT Licenseは、OpenAI、ChatGPT、Codexその他の第三者の名称、ロゴ、商標に対する使用許可を与えるものではありません。

## ダウンロード

- [Windows Installer（推奨）](https://github.com/ukr8b3g-cmyk/QuotaTray/releases/latest/download/QuantaTray-v0.1.3-win-x64-setup.exe)
- [Portable ZIP](https://github.com/ukr8b3g-cmyk/QuotaTray/releases/latest/download/QuantaTray-v0.1.3-win-x64-portable.zip)
- [SHA-256チェックサム](https://github.com/ukr8b3g-cmyk/QuotaTray/releases/latest/download/SHA256SUMS.txt)

配布ファイルは現在コード署名されていません。Windows SmartScreenに「不明な発行元」と表示された場合は、GitHub ReleaseのSHA-256と照合してください。

## 概要

QuantaTrayは、公式Codex App Serverの読み取り専用APIを使用し、次の情報を表示します。

- 週間利用枠の残量、次回リセット時刻、カウントダウン
- App Serverが小数値を返す場合、小数第1位までの残量表示（例：`94.4%`）
- リセット券の件数と有効期限（取得できる場合）
- 定期リセット、リセット券使用の可能性、予定外リセット候補のローカル履歴
- Codexプランと接続状態
- 60秒ごとの自動更新と手動更新

QuantaTrayは利用枠を表示するだけで、リセット券を使用したり、利用枠を変更したりしません。

## 必要環境

- Windows 11 x64推奨
- 公式Codex CLI/App Server
- Codexを利用できるChatGPTアカウント
- インターネット接続

QuantaTrayがバックグラウンドで `codex app-server --stdio` を起動するため、Codexデスクトップ画面を開いておく必要はありません。`codex.exe` を自動検出できない場合は、設定画面でパスを指定できます。

## インストール

### Installer版

1. `QuantaTray-v0.1.3-win-x64-setup.exe` をダウンロードします。
2. セットアップを実行し、画面の案内に従います。
3. 更新インストール時は、常駐中のQuantaTrayが自動的に終了します。
4. インストール後、QuantaTrayがタスクトレイに常駐します。

インストール先：

```text
%LOCALAPPDATA%\Programs\QuantaTray\
```

### Portable ZIP版

1. `QuantaTray-v0.1.3-win-x64-portable.zip` をダウンロードします。
2. ZIP全体を書き込み可能なフォルダーへ展開します。
3. `QuantaTray.exe` を実行します。

設定、履歴、ログは展開先の `data` フォルダーへ保存されます。ZIP内から直接実行しないでください。

## 使い方

- トレイアイコンを左クリック：コンパクト表示
- トレイアイコンをダブルクリック：詳細表示
- トレイアイコンを右クリック：ミニ／コンパクト／詳細表示と主要設定
- ミニ表示をダブルクリック：コンパクト表示へ戻る
- ミニ表示を右クリック：コンパクト／詳細表示、更新、設定
- コンパクト画面の3点メニュー：ミニ／詳細表示、更新、設定
- 詳細画面の更新アイコン：最新情報を取得
- 詳細画面の歯車アイコン：設定画面

<img width="198" height="327" alt="QuantaTrayのトレイメニュー" src="docs/images/tray-menu-ja.png" />

### 表示オプション

- **常に手前に表示**：ミニ／コンパクト／詳細を、ほかのウィンドウより前に表示します。
- **位置を固定**：パネルのドラッグ移動だけを禁止します。
- **モニターと位置を記憶**：終了時のモニターと基準位置を保存し、次回起動時に復元します。
- **画面端に吸着**：移動終了時に、パネルを現在のモニターの作業領域端へ寄せます。
- **ミニ表示のクリック透過**：ミニ表示だけが背後のアプリへクリックを通します。ONの間はトレイメニューから解除してください。
- **表示位置をリセット**：保存した共通位置を消去し、コンパクト表示をメインディスプレイ中央へ戻します。

表示切替時は、位置固定や位置記憶のON/OFFに関係なく、同じモニターと基準位置を引き継ぎます。

## 認証・プライバシー・セキュリティ

QuantaTrayはCodex App Serverへローカルstdioで接続します。OpenAIへの通信はCodex App Serverが行います。

- ブラウザCookie、保存パスワード、Codex認証ファイルを直接読み取りません。
- パスワード、アクセストークン、メールアドレス、アカウントIDを保存しません。
- 会話内容、ソースコード、プロジェクトファイルを収集しません。
- テレメトリー、広告、利用解析、開発者運営サーバーはありません。
- リセット券を使用する書き込みAPIは呼び出しません。

詳細：

- [PRIVACY.md](PRIVACY.md) — 保存情報、通信、認証、データ保持
- [SECURITY.md](SECURITY.md) — 脆弱性の非公開報告方法と機密情報の扱い
- [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) — 第三者コンポーネントと商標

## ローカルデータ

Installer版：

```text
%LOCALAPPDATA%\QuantaTray\
```

Portable版：

```text
<展開フォルダー>\data\
```

保存対象は設定、リセット履歴、機密情報を除いた限定・マスキング済み診断ログです。履歴の既定保持期間は365日です。

## 対応言語

`auto`はWindowsの表示言語へ追従します。手動では、日本語、英語、簡体字中国語、繁体字中国語、韓国語、ドイツ語、フランス語、スペイン語、ポルトガル語（ブラジル）、ロシア語から選択できます。

## 制限事項

- App Serverから週間枠が返らない場合、推測値は表示しません。
- リセット券の詳細が返らない場合、件数だけ表示します。
- リセット理由は返されないため、履歴の分類は観測値に基づく推定です。
- アプリ本体の自動アップデートは未実装です。
- Windows x64以外は未検証です。

## トラブルシューティング

### 数値が表示されない

初回接続には時間がかかる場合があります。詳細画面の接続状態を確認し、更新アイコンを押してください。

### Codexが見つからない

`codex --version` が実行できることを確認してください。QuantaTrayはPATHに加え、公式のユーザー別standalone配置（`%USERPROFILE%\.codex\packages\standalone\releases\`）も自動探索します。必要に応じて、設定画面の「接続」で `codex.exe` のパスを指定します。

### ミニ表示をクリックできない

「ミニ表示のクリック透過」がONです。システムトレイの右クリックメニューから解除するか、コンパクト／詳細表示へ切り替えてください。

### パネルが画面外にある

トレイの右クリックメニュー、または設定画面の「表示」から「表示位置をリセット」を実行してください。

## 開発

- C# / .NET 10 / Windows Forms
- Codex App Server stdio JSONL
- Inno Setup 6

ビルド方法は [docs/BUILDING.md](docs/BUILDING.md) を参照してください。

## 不具合・脆弱性報告

- 一般的な不具合・機能要望：[GitHub Issues](https://github.com/ukr8b3g-cmyk/QuotaTray/issues)
- 脆弱性・認証情報に関係する問題：[SECURITY.md](SECURITY.md)

公開Issueへアクセストークン、認証ファイル、Cookie、メールアドレス、会話、ソースコード等の機密情報を投稿しないでください。

## ライセンス

[MIT License](LICENSE)

ソフトウェアは無保証で提供されます。依存コンポーネントには各ライセンスが適用されます。詳細は [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) を確認してください。

---

## English

QuantaTray is an unofficial Windows system-tray monitor for viewing Codex usage limits.

QuantaTray is not an official OpenAI product and is not affiliated with, endorsed by, sponsored by, or warranted by OpenAI. OpenAI, ChatGPT, and Codex are trademarks of their respective owners.

## Download

- [Windows Installer (recommended)](https://github.com/ukr8b3g-cmyk/QuotaTray/releases/latest/download/QuantaTray-v0.1.3-win-x64-setup.exe)
- [Portable ZIP](https://github.com/ukr8b3g-cmyk/QuotaTray/releases/latest/download/QuantaTray-v0.1.3-win-x64-portable.zip)
- [SHA-256 checksums](https://github.com/ukr8b3g-cmyk/QuotaTray/releases/latest/download/SHA256SUMS.txt)

The current binaries are not code-signed. If Windows SmartScreen shows an unknown-publisher warning, verify the file against `SHA256SUMS.txt`.

## Features

- Weekly remaining allowance, reset time, and countdown
- Optional one-decimal percentage display when supplied by App Server
- Reset-credit count and expiry dates when available
- Local history of scheduled resets, possible reset-credit use, and unexpected reset candidates
- Mini, compact, and detailed views
- Automatic polling every 60 seconds and manual refresh

## Requirements

- Windows 11 x64 recommended
- Official Codex CLI/App Server
- A ChatGPT account with access to Codex
- Internet connection

QuantaTray launches `codex app-server --stdio` in the background. The Codex desktop window does not need to remain open.

## Privacy and security

QuantaTray communicates with OpenAI only through the separately installed official Codex App Server.

- It does not directly read browser cookies, saved passwords, or Codex credential files.
- It does not store passwords, access tokens, email addresses, or account IDs.
- It does not collect conversations, source code, or project files.
- It has no telemetry, ads, analytics, or developer-operated backend.
- It never calls the reset-credit consumption API.

See [PRIVACY.md](PRIVACY.md) and [SECURITY.md](SECURITY.md).

## Development

- C# / .NET 10 / Windows Forms
- Codex App Server over stdio JSONL
- Inno Setup 6

See [docs/BUILDING.md](docs/BUILDING.md) for build instructions.

## License

[MIT License](LICENSE)

The MIT License covers the project software. It does not grant rights to OpenAI, ChatGPT, Codex, or other third-party names, logos, or trademarks.
