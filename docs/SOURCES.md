# 参照資料

確認日：2026-07-25

## OpenAI公式

- Codex App Server
  - https://learn.chatgpt.com/docs/app-server
  - stdio JSONL、initialize、account/rateLimits/read、updated通知、reset credit metadataを確認
- Codex Authentication
  - https://learn.chatgpt.com/docs/auth
  - 認証キャッシュ再利用、OS資格情報ストア、ブラウザログインを確認
- AGENTS.md
  - https://learn.chatgpt.com/docs/agent-configuration/agents-md
- OpenAI Codex repository
  - https://github.com/openai/codex
  - Windows CLI配布、Apache-2.0を確認

## Microsoft公式

- .NET releases and support
  - https://learn.microsoft.com/dotnet/core/releases-and-support
  - .NET 10 LTSを確認
- Install .NET on Windows
  - https://learn.microsoft.com/dotnet/core/install/windows
- Windows Forms
  - https://learn.microsoft.com/dotnet/desktop/winforms/
- High DPI support
  - https://learn.microsoft.com/dotnet/desktop/winforms/high-dpi-support-in-windows-forms

## Packaging

- Inno Setup
  - https://jrsoftware.org/isinfo.php

## 注意

APIと配布仕様は変わり得る。Codexは実装開始時に公式資料と `codex app-server generate-json-schema` を再確認し、本ファイルへ検証日とバージョンを追記すること。
