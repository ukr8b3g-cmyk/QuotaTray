# QuantaTray v0.2.5

## 日本語

QuantaTray v0.2.5では、4K・表示倍率200%環境でトップレベル画面の大きさが内部コントロールと一致しない問題に対して、4画面共通のDPIサイズ処理を追加しました。

- Mini／Compact／Detail／Settingsを同一のDPIウィンドウ管理へ統合
- 96 DPI基準の論理サイズを、初期表示時とDPI変更時に実DPIへ明示変換
- Detail／Settingsのユーザー変更済み高さを維持
- 200%表示時のサイズ変換と4画面の定義を検証する回帰テストを追加
- 既存のPerMonitorV2、UI構成、配色、操作方法は維持

開発側には4K・200%の実機環境がないため、Issue #2の報告環境で最終確認を依頼しています。

## English

QuantaTray v0.2.5 adds shared top-level DPI window sizing for the Mini, Compact, Detail, and Settings windows to address mismatched window and child-control scaling at 4K resolution with 200% display scaling.

- Uses one shared DPI window manager for all four primary windows
- Converts 96-DPI logical window sizes on initial display and after DPI changes
- Preserves user-resized logical heights for Detail and Settings
- Adds regression tests for all four window definitions and 200% size conversion
- Preserves the existing PerMonitorV2 configuration, UI structure, colors, and controls

The maintainers do not have a 4K / 200% test environment, so final validation is requested from the Issue #2 reporter.

- Installer: per-user installation, no administrator privileges required.
- Portable ZIP: extract to a writable folder; data stays under `data/`.
- This build is unsigned. Verify it with `SHA256SUMS.txt`.