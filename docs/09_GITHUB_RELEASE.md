# 09 GitHub・Release運用

## 1. 実行前確認

ユーザーへ確認する項目：

- GitHub owner
- repository名
- public/private
- 新規作成か既存か
- ライセンス
- 直接mainへpushかPRか
- Release公開可否
- コード署名有無

確認前にリモート書込をしない。

## 2. 推奨ブランチ

```text
feat/initial-quantatray
```

## 3. 初回コミット構成

- ソース
- テスト
- packaging
- README
- PRIVACY
- LICENSE
- CHANGELOG
- THIRD-PARTY-NOTICES
- AGENTS.md
- `.github/workflows/build.yml`
- `.github/workflows/release.yml`

生成物、ユーザーデータ、ログ、authファイルをコミットしない。

## 4. CI

Pull Request：

- restore
- build Release
- unit tests
- integration tests with fake App Server
- localization completeness
- forbidden API string check
- format/analyzer

Tag：

- x64 publish
- installer build
- portable zip
- SHA256
- artifact upload
- GitHub Release draft

ARM64は有効化前に明示テストを追加する。

## 5. Release命名

Tag：`v0.2.0`

Assets：

```text
QuantaTray-v0.2.0-win-x64-setup.exe
QuantaTray-v0.2.0-win-x64-portable.zip
SHA256SUMS.txt
```

## 6. Release Notes

最低限：

- 対応OS/arch
- Codex CLIが別途必要
- インストーラーとZIPの違い
- 認証の流れ
- 既知の制限
- 未署名の場合のSmartScreen
- SHA256
- 非公式・非提携表示

## 7. README画像

`assets/mockup_three_views.png` は実装前のモックアップ。完成後は実アプリのスクリーンショットに置換し、モックアップである旨を削除する。
