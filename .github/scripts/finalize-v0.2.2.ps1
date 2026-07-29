$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Set-Location $repo

function Replace-Required([string]$Path, [string]$Old, [string]$New) {
    $content = Get-Content -LiteralPath $Path -Raw
    if (-not $content.Contains($Old)) {
        throw "Required text was not found in $Path: $Old"
    }
    Set-Content -LiteralPath $Path -Value ($content.Replace($Old, $New)) -Encoding utf8
}

Replace-Required 'Directory.Build.props' '<Version>0.2.1</Version>' '<Version>0.2.2</Version>'
Replace-Required 'README.md' '0.2.1' '0.2.2'
Replace-Required 'packaging/inno/QuantaTrain.iss' '#define MyAppVersion "0.2.1"' '#define MyAppVersion "0.2.2"'

$changelogPath = 'CHANGELOG.md'
$changelog = Get-Content -LiteralPath $changelogPath -Raw
if (-not $changelog.Contains('## 0.2.2 - 2026-07-29')) {
    $section = @'

## 0.2.2 - 2026-07-29

- Fixed the Overview "Show all" action so it opens every retained reset-history record instead of only the recent in-memory subset.
- Read all retained monthly history JSONL files in newest-first order while skipping only damaged or unsupported rows.
- Kept the compact and detailed recent-history limits unchanged.
- Added regression coverage for multi-month history, damaged rows, recent-count behavior, and the Show all click event.
'@
    $changelog = [regex]::Replace(
        $changelog,
        '(?m)^## Unreleased\r?\n',
        "## Unreleased`n$section`n",
        1)
    Set-Content -LiteralPath $changelogPath -Value $changelog -Encoding utf8
}

$releaseNotes = @'
# QuantaTray v0.2.2

## 日本語

QuantaTray v0.2.2では、詳細画面のリセット履歴にある「すべて表示」が、保存期間内の履歴をすべて表示するよう修正しました。

- 「すべて表示」を押すと、保持中の月別履歴ファイルをすべて読み込む一覧画面を表示
- 履歴を新しい順で表示
- 破損した行だけを無視し、同じファイル内の正常な履歴は継続して表示
- コンパクト表示と詳細画面の「最近の履歴」件数は従来どおり維持
- 複数月、破損行、最近の履歴件数、ボタン操作の回帰テストを追加

既に保存期間の処理で削除された履歴は復元されません。ローカルの履歴JSONLに残っている記録が表示対象です。

## English

QuantaTray v0.2.2 fixes the reset-history **Show all** action so it displays every retained local history record.

- Opens a dedicated list containing all retained monthly history files
- Sorts records newest first
- Skips only damaged rows while continuing to read valid rows from the same file
- Preserves the existing recent-history limits in compact and detailed views
- Adds regression tests for multiple months, damaged rows, recent-count behavior, and the Show all action

History already removed by retention cleanup cannot be restored. The dialog displays records still present in the local history JSONL files.

- Installer: per-user installation, no administrator privileges required.
- Portable ZIP: extract to a writable folder; data stays under `data/`.
- The official Codex CLI must be installed separately.
- Existing Codex authentication is reused by App Server.
- This build is unsigned. Windows SmartScreen may show a warning; compare the file against `SHA256SUMS.txt`.
- QuantaTray is unofficial and is not affiliated with or endorsed by OpenAI.
'@
Set-Content -LiteralPath 'RELEASE_NOTES.md' -Value $releaseNotes -Encoding utf8

$buildPath = '.github/workflows/build.yml'
$build = Get-Content -LiteralPath $buildPath -Raw
$packageMarker = "`n  package:"
$packageIndex = $build.IndexOf($packageMarker, [StringComparison]::Ordinal)
if ($packageIndex -lt 0) {
    throw 'Temporary PR package job was not found in build.yml.'
}
$build = $build.Substring(0, $packageIndex).TrimEnd() + "`n"
Set-Content -LiteralPath $buildPath -Value $build -Encoding utf8

Remove-Item -LiteralPath '.github/scripts/finalize-v0.2.2.ps1'
Remove-Item -LiteralPath '.github/workflows/finalize-v0.2.2.yml'

git config user.name 'github-actions[bot]'
git config user.email '41898282+github-actions[bot]@users.noreply.github.com'
git add -A
git commit -m 'Prepare QuantaTray v0.2.2 release'
git push origin HEAD:fix/history-show-all
