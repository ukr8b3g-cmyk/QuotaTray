# QuantaTray v0.2.4

## 日本語

QuantaTray v0.2.4では、Windows 11の4K・表示倍率200%環境で、コンパクト画面と設定画面が切れる問題に対して、共通フォーム基盤のDPI処理を修正しました。

- すべてのカスタムフォームで96 DPIの設計基準を共通化
- PerMonitorV2によるWinForms標準のDPIスケーリングを維持
- DPI変更時にウィンドウ幅を物理ピクセルで固定していた独自処理を削除
- 固定幅フォームは、DPI対応した`MinimumSize`／`MaximumSize`制約で幅を維持
- Windows 10／11向けのアプリケーションマニフェスト識別子を修正
- 共通DPI設定、物理幅固定処理の不在、マニフェストを確認する回帰テストを追加

この変更は、Windowsの互換性設定にある「高DPIスケール設定の上書き」を使用せず、200%表示でもフォームと内部コントロールを同じ倍率で拡大させることを目的としています。実際の4K・200%環境での最終確認は継続します。

## English

QuantaTray v0.2.4 reworks the shared form DPI foundation to address clipping in the Compact and Settings windows on Windows 11 systems using 4K resolution and 200% display scaling.

- Establishes a shared 96-DPI design baseline for all custom forms
- Retains standard WinForms PerMonitorV2 scaling
- Removes custom physical-width locking during DPI changes
- Preserves fixed-width behavior through DPI-aware `MinimumSize` and `MaximumSize` constraints
- Corrects the application manifest identifier for Windows 10 and Windows 11
- Adds regression checks for the shared DPI baseline, absence of physical-width locking, and manifest declarations

The change is intended to keep the top-level windows and their child controls on the same DPI scale without requiring the Windows “System (Enhanced)” compatibility override. Final validation on an actual 4K display at 200% scaling remains in progress.

- Installer: per-user installation, no administrator privileges required.
- Portable ZIP: extract to a writable folder; data stays under `data/`.
- The official Codex CLI must be installed separately.
- Existing Codex authentication is reused by App Server.
- This build is unsigned. Windows SmartScreen may show a warning; compare the file against `SHA256SUMS.txt`.
- QuantaTray is unofficial and is not affiliated with or endorsed by OpenAI.
