param(
    [string]$DistPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')) 'dist')
)

$ErrorActionPreference = 'Stop'
$assets = Get-ChildItem -LiteralPath $DistPath -File |
    Where-Object { $_.Name -match '^QuantaTrain-v.+-win-x64-(setup\.exe|portable\.zip)$' } |
    Sort-Object Name
if ($assets.Count -ne 2) {
    throw "Expected setup and portable assets; found $($assets.Count)."
}

$lines = foreach ($asset in $assets) {
    $hash = Get-FileHash -LiteralPath $asset.FullName -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $($asset.Name)"
}
Set-Content -LiteralPath (Join-Path $DistPath 'SHA256SUMS.txt') -Value $lines -Encoding utf8
