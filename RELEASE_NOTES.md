# QuantaTray v0.2.12

## 日本語

- 「詳細表示」→「概要」で、リセット券が4件以上あるとカードが次の行に折り返されます。表示件数に上限はありません。期限の詳細が一部しか届かない場合は、残りを「期限情報なし」としてまとめて表示します。
- Codexから情報が届いた場合は、同じ「概要」で5時間枠の残量とリセット日時、利用上限に達した理由、支出上限への到達も確認できます。
- 「使用分析」ではGPT-6.2 Astra／Solなどの推論レベル `xhigh` と `max` を個別に集計します。モデルごとに、総トークンのうちFast、Ultrafastで使った割合も表示します。サービス種別の記録が一切ないモデルでは「—」を表示します。

ローカル使用分析を有効にしている場合は、更新後の初回走査で既存ログを再集計します。Windowsの自動テスト262件が成功し、インストール版の実画面でもリセット券4件の表示を確認しました。

Windows x64用のインストーラーとポータブルZIPを配布しています。ファイルの照合には `SHA256SUMS.txt` を使用してください。

## English

- In **Detail → Overview**, reset-credit cards wrap onto new rows so you can see four or more credits. If Codex reports a higher count than it provides entries for, the remaining credits share one card with no expiry information.
- Overview also shows the remaining five-hour allowance and its reset time, the reason a limit was reached, and spending-limit notices when Codex supplies those details.
- In **Usage analysis**, `xhigh` and `max` are counted separately for models such as GPT-6.2 Astra and Sol. Each model shows the percentage of its total tokens recorded as Fast and as Ultrafast. A dash means that no service tier was recorded for that model.

If local usage analysis is enabled, the first scan after the update rebuilds its cache from existing logs. All 262 automated Windows tests passed, and four credits were confirmed in the installed app.

Download the Windows x64 installer or extract the portable ZIP to a writable folder. Use `SHA256SUMS.txt` to verify either download.
