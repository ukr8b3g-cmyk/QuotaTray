# 07 ビルド・インストーラー・ポータブル

## 1. 必須成果物

```text
QuantaTrain-v0.1.0-win-x64-setup.exe
QuantaTrain-v0.1.0-win-x64-portable.zip
SHA256SUMS.txt
```

ARM64は追加検証後のみ。

## 2. Publish方針

- `Release`
- `win-x64`
- self-contained
- single-file
- trimming無効
- debug symbols除外
- ReadyToRunはサイズ・起動時間を測定して判断
- バージョン情報、著作権、製品名をEXEへ埋め込む
- DPI-aware manifestを含める

例：

```powershell
dotnet publish src/QuantaTrain.App/QuantaTrain.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true `
  -p:PublishTrimmed=false `
  -p:DebugType=None `
  -p:DebugSymbols=false
```

最終コマンドは実プロジェクトで検証して更新する。

## 3. インストーラー

Inno Setupを使用。

- per-user
- `%LOCALAPPDATA%\Programs\QuantaTrain`
- 管理者権限不要
- Start Menuショートカット
- アンインストーラー
- 「Windows起動時に起動」は初期OFF
- アプリ起動中の上書き更新を防止
- アンインストール時にローカルデータを残す／削除する選択
- インストール完了後の起動チェック

レジストリはHKCUのみ。スタートアップ：

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

値は引用符付き完全パスと `--background`。

## 4. ポータブルZIP

内容：

```text
QuantaTrain.exe
portable.flag
data/
README.txt
LICENSE.txt
THIRD-PARTY-NOTICES.txt
```

- `portable.flag` を検出したら、設定・履歴・ログを `data/` に保存
- 実行フォルダーが書込不可なら明示的な選択画面を出す
- ポータブル版でもスタートアップ登録可能だが、フォルダー移動で無効になる注意を表示
- ZIP内にユーザーデータを含めない

## 5. Codex依存関係

初版は `codex.exe` を同梱しない。

理由：

- 公式Codexの更新・セキュリティ対応を分離する
- 配布サイズとライセンス表示を単純化する
- 既存のCodex認証キャッシュをそのまま利用しやすい

READMEで公式Codex CLIが必要なことを明記する。将来同梱する場合はApache-2.0のNOTICE、更新方法、バージョン固定、脆弱性対応、署名検証を別途設計する。

## 6. 署名

- コード署名証明書がある場合、EXEとInstallerへ署名
- GitHub Actionsの署名鍵はSecret/外部署名サービスに置き、リポジトリへ保存しない
- 未署名の場合はREADMEとRelease NotesでSmartScreen警告を説明

## 7. チェックサム

PowerShell例：

```powershell
Get-FileHash .\dist\* -Algorithm SHA256
```

`SHA256SUMS.txt` はRelease Assetsへ含める。
