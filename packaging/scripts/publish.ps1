param(
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$dist = Join-Path $repo 'dist'
$publish = Join-Path $dist "publish\$Runtime"
$portable = Join-Path $dist "portable\$Runtime"

foreach ($target in @($publish, $portable)) {
    $targetFull = [IO.Path]::GetFullPath($target)
    $distFull = [IO.Path]::GetFullPath($dist)
    if (-not $targetFull.StartsWith($distFull, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean outside dist: $targetFull"
    }
    if (Test-Path -LiteralPath $targetFull) {
        Remove-Item -LiteralPath $targetFull -Recurse -Force
    }
}

dotnet publish (Join-Path $repo 'src\QuantaTrain.App\QuantaTrain.App.csproj') `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -o $publish `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

New-Item -ItemType Directory -Path (Join-Path $portable 'data') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $portable 'data\README.txt') `
    -Value 'QuantaTrain stores portable settings, history, and redacted logs in this folder.' `
    -Encoding utf8
Copy-Item -LiteralPath (Join-Path $publish 'QuantaTrain.exe') -Destination $portable
Copy-Item -LiteralPath (Join-Path $publish 'locales') -Destination $portable -Recurse
Copy-Item -LiteralPath (Join-Path $repo 'packaging\README-portable.txt') -Destination (Join-Path $portable 'README.txt')
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $portable 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $repo 'THIRD-PARTY-NOTICES.md') -Destination (Join-Path $portable 'THIRD-PARTY-NOTICES.txt')
New-Item -ItemType File -Path (Join-Path $portable 'portable.flag') -Force | Out-Null

$zip = Join-Path $dist "QuantaTrain-v$Version-win-x64-portable.zip"
if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}
Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $zip -CompressionLevel Optimal
