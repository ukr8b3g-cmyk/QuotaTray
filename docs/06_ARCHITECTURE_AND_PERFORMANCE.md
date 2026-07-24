# 06 アーキテクチャ・軽量化・信頼性

## 1. 推奨構成

```text
QuantaTrain.App
  Program / SingleInstance
  TrayController
  CompactForm / DetailForm / SettingsForm / HistoryForm
  CompositionRoot

QuantaTrain.Core
  Domain models
  WeeklyBucketSelector
  RemainingCalculator
  ResetClassifier
  FreshnessPolicy
  NotificationPolicy

QuantaTrain.Infrastructure
  CodexLocator
  AppServerProcess
  JsonRpcConnection
  AccountClient
  PollingCoordinator
  JsonSettingsStore
  JsonlHistoryStore
  StartupRegistration
  WindowPlacementStore
  RedactedLogger
```

## 2. スレッド

- UIスレッド：WinFormsのみ
- App Server stdout読取：非同期1ループ
- リクエスト管理：IDと`TaskCompletionSource`の有限辞書
- Poll：`PeriodicTimer` 1個、前回完了後に次回
- ファイル書込：単一の直列キュー、上限あり
- UI更新：`SynchronizationContext.Post`

## 3. 重複防止

- `RefreshAsync` はSingleFlight化する。
- 60秒Poll、パネル開閉、通知が重なっても同時に複数の `rateLimits/read` を送らない。
- 開いているフォームはコンパクトか詳細のどちらか1つを基本とする。

## 4. リソース解放

必須：

- `NotifyIcon.Dispose()`
- フォーム、アイコン、Bitmap、Graphics、Fontの解放
- stdout/stderr readerのキャンセル
- 子プロセスの終了監視解除
- `CancellationTokenSource.Dispose()`
- イベント購読解除
- Windowsフック、Mutex、Named Pipeの解放

動的アイコンを生成する場合、以前のIconハンドルを必ず破棄する。

## 5. メモリ目標

- App本体：ウォームアップ後100MB未満を目標
- 24時間でPrivate Bytesの継続増加が10MB未満
- GDI/USER handleが時間比例で増えない
- 履歴一覧はページングまたは仮想表示。全履歴を常時メモリへ載せない
- ログ表示も末尾限定

App Serverは別プロセスとして計測し、QuantaTrain本体のリークと混同しない。

## 6. CPU・I/O

- 待機時はTimer待ちでCPUを消費しない
- 1分ごとのJSON要求以外に常時処理をしない
- UI再描画は値が変わった時だけ
- ファイル書込は変化時・チェックポイント時だけ
- 連続アニメーション、常時グラフ、秒単位カウントダウン再描画は避ける
- カウントダウンは分単位更新で十分。パネル表示中のみ最大30秒更新可

## 7. ログ上限

- 通常レベル：Warning以上
- 1ファイル最大1MB、最大5ファイル
- 7日超過を削除
- デバッグログはユーザー明示ON、一定時間後に自動OFF
- 生JSONと認証URLはログ禁止

## 8. 異常終了対策

- `settings.json` は一時ファイルへ書いて原子的置換
- `state.json` も同様
- history JSONLは1行1イベントでflush
- 破損行はスキップし、残りを読めるようにする
- 未処理例外はローカルログ後にトレイ通知し、安全に終了
- クラッシュレポートを外部送信しない
