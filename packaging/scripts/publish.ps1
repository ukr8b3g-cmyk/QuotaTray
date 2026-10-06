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
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

# Runtime-package license files are not copied to publish automatically.
# Use the exact versions recorded by restore, rather than a mutable web source.
$restoreAssets = Get-Content -LiteralPath (Join-Path $repo 'src\QuantaTrain.App\obj\project.assets.json') -Raw | ConvertFrom-Json
$runtimeNotices = @{
    'Microsoft.NETCore.App.Runtime.win-x64' = @('LICENSE.TXT', 'THIRD-PARTY-NOTICES.TXT')
    'Microsoft.WindowsDesktop.App.Runtime.win-x64' = @('LICENSE')
}
foreach ($package in $runtimeNotices.Keys) {
    $downloads = @($restoreAssets.project.frameworks.PSObject.Properties.Value.downloadDependencies |
        Where-Object name -eq $package)
    $versions = @($downloads.version | Sort-Object -Unique)
    if ($versions.Count -ne 1 -or $versions[0] -notmatch '^\[([^,\]]+)') {
        throw "Cannot identify the restored runtime package version: $package"
    }
    $runtimeVersion = $Matches[1].Trim()
    $packageDir = $restoreAssets.packageFolders.PSObject.Properties.Name |
        ForEach-Object { Join-Path $_ ($package.ToLowerInvariant() + '/' + $runtimeVersion) } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Container } |
        Select-Object -First 1
    if (-not $packageDir) { throw "Runtime package cache missing: $package $runtimeVersion" }
    $destination = Join-Path $publish "notices\$package-$runtimeVersion"
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach ($notice in $runtimeNotices[$package]) {
        Copy-Item -LiteralPath (Join-Path $packageDir $notice) -Destination $destination
    }
}

New-Item -ItemType Directory -Path (Join-Path $portable 'data') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $portable 'data\README.txt') `
    -Value 'QuantaTray stores portable settings, history, and redacted logs in this folder.' `
    -Encoding utf8
$projectNotice = Join-Path $publish 'THIRD-PARTY-NOTICES.txt'
if (Test-Path -LiteralPath $projectNotice) {
    Move-Item -LiteralPath $projectNotice -Destination (Join-Path $publish 'DOTNET-THIRD-PARTY-NOTICES.txt')
}
Copy-Item -LiteralPath (Join-Path $repo 'THIRD-PARTY-NOTICES.md') -Destination $projectNotice
# Preserve every publish dependency and generated runtime notice in both editions.
Get-ChildItem -LiteralPath $publish | Copy-Item -Destination $portable -Recurse
Copy-Item -LiteralPath (Join-Path $repo 'packaging\README-portable.txt') -Destination (Join-Path $portable 'README.txt')
Copy-Item -LiteralPath (Join-Path $repo 'README.md') -Destination (Join-Path $portable 'README.md')
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $portable 'LICENSE')
Copy-Item -LiteralPath (Join-Path $repo 'PRIVACY.md') -Destination (Join-Path $portable 'PRIVACY.md')
New-Item -ItemType File -Path (Join-Path $portable 'portable.flag') -Force | Out-Null

$zip = Join-Path $dist "QuantaTray-v$Version-win-x64-portable.zip"
if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}
Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $zip -CompressionLevel Optimal
