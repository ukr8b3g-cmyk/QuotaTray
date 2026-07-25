# 03 Codex App Server・認証仕様

## 1. 前提

QuantaTrayはOpenAI公式Codex App Serverのアカウント系メソッドだけを使う。Codexの会話、スレッド、コマンド実行、プロジェクトファイルは扱わない。

## 2. プロセス構成

```text
QuantaTray.exe
  └─ child process: codex.exe app-server
       stdin/stdout: JSONL
       stderr: bounded redacted diagnostics
       network: OpenAI/ChatGPT managed by Codex
```

- stdioはデフォルトの安定ローカルトランスポート。
- WebSocketは使用しない。
- ローカルHTTPサーバーを開かない。
- Shell経由で起動せず、引数配列で安全に起動する。
- コンソールウィンドウを表示しない。

## 3. ハンドシェイク

接続ごとに：

```json
{"method":"initialize","id":1,"params":{"clientInfo":{"name":"quantatray","title":"QuantaTray","version":"0.1.1"}}}
{"method":"initialized","params":{}}
```

- `initialize` 成功前に他メソッドを呼ばない。
- experimental capabilityは設定しない。
- Codexバージョンごとに `generate-json-schema` した結果を互換性確認に使う。

## 4. 使用するメソッド

- `account/read`
- `account/login/start`
- `account/login/completed`
- `account/updated`
- `account/rateLimits/read`
- `account/rateLimits/updated`

## 5. 使用禁止

- `account/rateLimitResetCredit/consume`
- 会話・Thread・Turn関連API
- 生トークンを渡す実験的認証
- UIスクレイピング
- 非公開HTTPエンドポイント直呼び

## 6. Codex実行ファイル探索

1. 設定画面の明示パス
2. `QUANTATRAY_CODEX_PATH`
3. `PATH` の `codex.exe`
4. `%USERPROFILE%\.codex\packages\standalone\releases\*\bin\codex.exe`
5. 公式インストール方法で作成されるその他の安全に確認可能なユーザー領域

探索結果ごとに `codex --version` を安全なタイムアウト付きで実行し、起動可能か確認する。
PATH上に存在してもアクセス拒否や起動不能となる候補はスキップし、次の安全な候補を探索する。

次は行わない：

- WindowsApps保護領域を権限昇格して探索
- Codex Desktopパッケージ内部からバイナリをコピー
- 非公式ミラーから自動ダウンロード
- ユーザー承認なしのPowerShellインストールスクリプト実行

## 7. 認証フロー

1. App Server起動・初期化
2. `account/read`
3. 認証済みなら継続
4. 未認証なら `account/login/start` の `chatgpt` を呼ぶ
5. `authUrl` を既定ブラウザで開く
6. `account/login/completed` と `account/updated` を待つ
7. 成功後に `account/rateLimits/read`

ブラウザ側で既にChatGPTへログイン済みなら、ブラウザ自身のセッションにより入力が減る場合がある。QuantaTrayがそのCookieを読むわけではない。

## 8. 認証キャッシュ再利用

- QuantaTrayは通常の環境変数を継承し、`CODEX_HOME` を勝手に変更しない。
- Codex CLI/App Serverが管理する既存キャッシュを利用させる。
- `auth.json` やOS資格情報ストアはApp Serverの責任範囲。
- QuantaTrayの設定・履歴へ認証情報を複製しない。

## 9. エラー処理

- App Server未検出：セットアップ画面
- バージョン不適合：必要最小バージョンと検出バージョンを表示
- JSON破損：当該行を破棄し、回数制限付き再起動
- 401：App Serverの公式再認証フローへ
- プロセス終了：1/2/5/10/15分のバックオフ
- 10分以内に5回以上異常終了：自動再起動を停止し、ユーザー操作を待つ

## 10. ログ

通常ログに記録可能：

- 時刻
- QuantaTrayバージョン
- Codexバージョン
- メソッド名
- 成否
- エラーコード・一般化メッセージ

記録禁止：

- `authUrl` の完全値
- トークン
- Cookie
- アカウントID
- メール
- 生のaccountレスポンス
- 生のstderr全文
