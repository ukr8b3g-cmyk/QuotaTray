# QuantaTray v0.2.7

## 日本語

v0.2.7では、タスクトレーの数値を読みやすくし、最近のリセット履歴を4件表示へ変更しました。「すべて表示」は親画面を操作不能にしない非モーダル画面になりました。

使用分析には、Codex App Serverの正式な読み取り専用APIによる購入クレジット残高・日別トークン・累計・ピーク・連続利用日数を追加しました。ローカルのプラグイン／ツールとSkill回数は初期値オフで、会話本文・コマンド・パスを保存しません。詳細画面は横幅を維持し、DPI対応の縦スクロールで表示します。Mini／コンパクト表示のレイアウトは変更せず、3表示の位置は個別に記憶します。

## English

v0.2.7 improves tray-number legibility, shows four recent reset rows, and makes the full-history window modeless. It adds read-only purchased-credit and account token usage from Codex App Server, plus opt-in privacy-limited local tool and skill counters. The detailed dashboard keeps its width and uses DPI-aware vertical scrolling; Mini and Compact layouts are unchanged, and all three views remember their positions independently.

---

# QuantaTray v0.2.6

## 日本語

QuantaTray v0.2.6では、Windows 11の4K・表示倍率200%環境で発生していた、文字・コントロールとトップレベル画面の倍率不一致を修正します。

- PerMonitorV2上での独自サイズ補正を廃止
- WindowsのDPI非対応GDIスケーリングをアプリ側で有効化し、「システム（拡張）」の互換性設定と同系統の一括拡大へ変更
- Mini／Compact／Detail／Settingsを、内部レイアウトごと同じ倍率で拡大
- v0.2.5で二重拡大された可能性があるDetail／Settingsの保存済み高さを一度だけ安全な既定値へ移行
- マニフェスト、起動処理、設定移行の回帰テストを追加

この方式では、画面と内部コントロールをWindowsが一体として拡大するため、個別の物理ピクセル計算や`WM_DPICHANGED`補正は行いません。

## English

QuantaTray v0.2.6 fixes the scale mismatch between top-level windows and their text and controls on Windows 11 systems using 4K resolution and 200% display scaling.

- Removes the custom PerMonitorV2 top-level window resizing introduced in v0.2.5
- Enables Windows DPI-unaware GDI scaling, using the same class of whole-window scaling as the working “System (Enhanced)” compatibility workaround
- Scales Mini, Compact, Detail, and Settings together with their complete internal layouts
- Migrates potentially doubled Detail and Settings saved heights from v0.2.5 back to safe defaults once
- Adds regression checks for the manifest, startup path, and settings migration

Windows now scales each complete window as one unit, so QuantaTray no longer performs separate physical-pixel or `WM_DPICHANGED` size calculations.

- Installer: per-user installation, no administrator privileges required.
- Portable ZIP: extract to a writable folder; data stays under `data/`.
- This build is unsigned. Verify it with `SHA256SUMS.txt`.
