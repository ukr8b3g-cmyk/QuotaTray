# QuantaTray v0.2.3

## 日本語

QuantaTray v0.2.3では、Windows 11の4K・高DPI環境でコンパクト表示の文字やカード、上部ボタンが切れる問題を修正しました。

- コンパクト表示の論理幅とカード領域を拡大
- 既存のPerMonitorV2設定に加えて、フォーム側のDPI自動スケーリングを明示
- 次回リセット日時とリセット券有効期限に十分な折り返し領域を確保
- 上部ツールバーの配置を広い論理幅に合わせて調整
- 既存の配色、カード構成、操作方法は維持

Windowsの互換性設定にある「高DPIスケール設定の上書き」を使用しなくても表示できることを目的とした修正です。

## English

QuantaTray v0.2.3 fixes clipped text, cards, and toolbar controls in the compact panel on Windows 11 systems using 4K or other high-DPI display scaling.

- Increases the compact panel's logical width and card content area
- Explicitly enables form-level DPI autoscaling while retaining PerMonitorV2 process awareness
- Gives reset dates and reset-credit expiry text enough vertical space to wrap
- Repositions the top toolbar for the wider logical layout
- Preserves the existing colors, card structure, and interaction model

The fix is intended to remove the need for the Windows compatibility override for high-DPI scaling.

- Installer: per-user installation, no administrator privileges required.
- Portable ZIP: extract to a writable folder; data stays under `data/`.
- The official Codex CLI must be installed separately.
- Existing Codex authentication is reused by App Server.
- This build is unsigned. Windows SmartScreen may show a warning; compare the file against `SHA256SUMS.txt`.
- QuantaTray is unofficial and is not affiliated with or endorsed by OpenAI.
