# Codex作業指示書

## ゴール

QuantaTrainの初回公開版を実装し、次の成果物を生成できる状態にしてください。

- `QuantaTrain-v<version>-win-x64-setup.exe`
- `QuantaTrain-v<version>-win-x64-portable.zip`
- `SHA256SUMS.txt`
- GitHub用README、PRIVACY、CHANGELOG、ライセンス、Release Notes

## Phase 0：実装前の確認

1. このパックの全仕様を読む。
2. Windows上の最新安定版Codex CLIを確認する。
3. `codex app-server generate-json-schema --out <temp>` を実行し、次を検証する。
   - `account/read`
   - `account/login/start`
   - `account/login/completed`
   - `account/updated`
   - `account/rateLimits/read`
   - `account/rateLimits/updated`
   - `rateLimitResetCredits`
4. `codex app-server` をstdioで起動し、`initialize` → `initialized` を実行する最小プローブを作る。
5. 結果を `docs/IMPLEMENTATION_VERIFICATION.md` に記録する。
6. 仕様との差異がある場合、UIスクレイピングに切り替えず、差異と安全な代替案を報告する。

## Phase 1：ソリューション作成

推奨構成：

```text
QuantaTrain.sln
src/
  QuantaTrain.App/             WinForms、トレイ、各画面
  QuantaTrain.Core/            ドメインモデル、枠選択、履歴判定
  QuantaTrain.Infrastructure/  App Server、保存、OS連携
  QuantaTrain.Localization/    文字列リソース

tests/
  QuantaTrain.Core.Tests/
  QuantaTrain.IntegrationTests/
  QuantaTrain.App.Tests/

packaging/
  inno/
  scripts/

docs/
```

- Target Framework：`net10.0-windows`
- UI：Windows Forms
- `ApplicationHighDpiMode`：PerMonitorV2
- 単一インスタンス化を行う
- 依存性注入コンテナは使わず、Composition Rootで組み立てる

## Phase 2：Codex App Server接続

- 実行ファイル探索順：
  1. ユーザー設定の明示パス
  2. `QUANTATRAIN_CODEX_PATH`
  3. `PATH` 上の `codex.exe`
  4. 公式インストーラーで使われる既知のユーザー領域（安全に確認できる範囲のみ）
- Microsoft Storeの保護された`WindowsApps`内部を直接探索・コピーしない。
- 見つからない場合は、公式インストール案内を開くボタンを表示する。
- `codex app-server` を非表示子プロセスとして1回起動し、stdio JSONL接続を維持する。
- 起動直後に `initialize`、成功後に `initialized`。
- `clientInfo.name = "quantatrain"`、titleとversionも送る。
- 通常利用で会話スレッドを作成しない。
- App Server終了時は指数バックオフで再起動し、再起動回数を制限する。
- アプリ終了時はstdinを閉じ、短時間待機後に残存プロセスだけ終了する。

## Phase 3：認証

- 起動時に `account/read` で認証状態を確認する。
- 既存のCodex認証キャッシュが有効ならそのまま利用する。
- 未認証なら `account/login/start` のChatGPTブラウザフローを開始し、返された公式URLを既定ブラウザで開く。
- ローカルコールバックが失敗した場合のみ、公式デバイスコード方式を代替として提示する。
- 本アプリ自身はCookie、パスワード、`auth.json`、OS資格情報を直接読まない。
- メールアドレスやアカウントIDを画面表示・保存・ログ出力しない。

## Phase 4：残量取得と更新

- `account/rateLimits/read` を60秒ごとに呼ぶ。
- `account/rateLimits/updated` も購読し、変更時は即時反映する。
- パネルを開いた時は必ず即時更新を要求する。重複要求は1件にまとめる。
- キャッシュ値を先に表示し、更新中を明示する。
- 最終成功から90秒を超えた値は「古い情報」と表示する。
- 通信失敗時は前回値を維持し、1、2、5、10、15分までバックオフする。ただし手動更新は1回だけ即時試行する。
- `rateLimitsByLimitId` と `primary` / `secondary` を平坦化し、週間枠は `windowDurationMins` が7日に最も近い候補を優先する。
- 週間枠が確認できない場合は「週間枠を取得できません」と表示し、推測値を作らない。
- 残量は `clamp(100 - usedPercent, 0, 100)`。
- `availableCount` をリセット券の正とし、詳細配列がない場合は件数だけ表示する。

## Phase 5：履歴と予定外リセット判定

`docs/04_RESET_HISTORY.md` のルールを実装する。

- 定期リセット
- リセット券使用の可能性
- 予定外リセット候補
- プラン／制限ポリシー変更
- データ補正・不確定

予定外リセットを「OpenAIが実行した」と断定しない。発生時刻は観測間隔として保存する。

## Phase 6：UI

- トレイアイコン左クリック：コンパクト表示
- コンパクト：週間残量、次回リセット、カウントダウン、最終更新だけ
- 詳細：リセット券、最近の履歴、プラン、接続状態
- 3点メニュー：詳細/コンパクト、更新、パネル固定、最前面、位置固定、設定、Codex、ChatGPT、終了
- 設定：一般、表示、言語、通知、履歴、接続、情報
- 初期テーマはダーク、アクセントはグリーン
- 常時アニメーションを使わない
- 透明度、最前面、位置固定、位置記憶、端吸着はすべて初期OFF
- 設定画面自体は不透明・非最前面・位置ロック対象外

## Phase 7：多言語

最低対応：

- 日本語
- English
- 简体中文
- 繁體中文
- 한국어
- Deutsch
- Français
- Español
- Português (Brasil)
- Русский

初期値はOS表示言語の自動検出。未対応は英語。手動選択可。日付時刻はOS地域設定を尊重する。

## Phase 8：配布

- x64のみを必須成果物とする。
- 自己完結型、単一EXE公開。トリミングは無効。
- インストーラー：Inno Setup、per-user、管理者権限不要。
- ポータブルZIP：`portable.flag` と `data/` を含める。
- スタートアップ登録は初期OFF、設定からHKCUへ登録・解除する。
- インストーラー版の設定は `%LOCALAPPDATA%\QuantaTrain`。
- ポータブル版は原則 `./data`。書き込み不可なら無断で別場所へ移さず、選択を促す。
- x86版は作らない。
- ARM64はx64合格後、Codex CLI/App Server互換性を実機または公式ランナーで確認できた場合だけ追加する。

## Phase 9：品質確認

- 単体テスト、偽App Server統合テスト、クリーンVMインストールテストを実施。
- 100%/150%/200% DPI、複数モニター、タスクバー上下左右を確認。
- 24時間メモリソークテストを必須、72時間を推奨。
- Appプロセスのメモリ・ハンドルが継続増加しないことを確認。
- 認証情報や生JSONがログへ出ないことを確認。
- `account/rateLimitResetCredit/consume` を呼ぶコードが存在しないことを検索で確認。

## Phase 10：GitHub

- リポジトリ情報と公開許可をユーザーへ確認する。
- README、PRIVACY、CHANGELOG、LICENSE、Third-Party Noticesを整備する。
- GitHub Actionsでx64ビルド・テスト・Release資産作成を自動化する。
- タグ付きRelease前に、署名有無とSmartScreen注意書きを確認する。
- ユーザーの明示許可なしにpush、Release公開、リポジトリ作成をしない。
