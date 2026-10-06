param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$dist = Join-Path $repo 'dist'
$publish = Join-Path $dist 'publish\win-x64'
$zip = Join-Path $dist "QuantaTray-v$Version-win-x64-portable.zip"
$setup = Join-Path $dist "QuantaTray-v$Version-win-x64-setup.exe"
$expected = @([IO.Path]::GetFileName($zip), [IO.Path]::GetFileName($setup))
$checksums = @{}
foreach ($line in Get-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt')) {
    if ($line -notmatch '^([a-f0-9]{64})  (.+)$') { throw 'Invalid checksum row.' }
    if ($checksums.ContainsKey($Matches[2])) { throw 'Duplicate checksum filename.' }
    $checksums[$Matches[2]] = $Matches[1]
}
if ($checksums.Count -ne 2) { throw 'Expected exactly two asset checksums.' }
foreach ($name in $expected) {
    $file = Get-Item -LiteralPath (Join-Path $dist $name)
    if ($file.Length -lt 1MB) { throw "Unexpectedly small package: $name" }
    if (-not $checksums.ContainsKey($name) -or
        (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $checksums[$name]) {
        throw "Checksum mismatch: $name"
    }
}

function Assert-AppMetadata([string]$Path) {
    $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
    if ($info.FileVersion -ne "$Version.0" -or
        $info.ProductVersion -notmatch ('^' + [regex]::Escape($Version) + '(\+|$)') -or
        $info.ProductName -ne 'QuantaTray') { throw "Incorrect app version/product: $Path" }
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ([BitConverter]::ToUInt16($bytes, 0) -ne 0x5a4d) { throw 'Missing DOS header.' }
    $pe = [BitConverter]::ToInt32($bytes, 0x3c)
    if ([BitConverter]::ToUInt32($bytes, $pe) -ne 0x4550 -or
        [BitConverter]::ToUInt16($bytes, $pe + 4) -ne 0x8664) { throw 'App payload is not Windows x64.' }
    Write-Host "Verified Windows x64 app: $($info.ProductVersion)"
}

$setupInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($setup)
if ($setupInfo.FileVersion -ne "$Version.0" -or
    $setupInfo.ProductVersion -ne $Version -or
    $setupInfo.ProductName -ne 'QuantaTray') { throw 'Incorrect installer version/product.' }
# Inno Setup's bootstrap architecture can differ from its x64-only payload.
$iss = Get-Content -LiteralPath (Join-Path $repo 'packaging\inno\QuantaTrain.iss') -Raw
foreach ($setting in @('PrivilegesRequired=lowest', 'ArchitecturesAllowed=x64compatible',
                       'ArchitecturesInstallIn64BitMode=x64compatible')) {
    if (-not $iss.Contains($setting)) { throw "Missing installer constraint: $setting" }
}
Assert-AppMetadata (Join-Path $publish 'QuantaTray.exe')
$notices = @(Get-ChildItem -LiteralPath (Join-Path $publish 'notices') -Recurse -File)
foreach ($pattern in @('Microsoft.NETCore.App.Runtime.win-x64-*\LICENSE.TXT',
                       'Microsoft.NETCore.App.Runtime.win-x64-*\THIRD-PARTY-NOTICES.TXT',
                       'Microsoft.WindowsDesktop.App.Runtime.win-x64-*\LICENSE')) {
    if (-not ($notices | Where-Object FullName -like "*$pattern")) { throw "Missing runtime notice: $pattern" }
}

$expanded = Join-Path ([IO.Path]::GetTempPath()) ('quantatray-package-' + [Guid]::NewGuid().ToString('N'))
try {
    Expand-Archive -LiteralPath $zip -DestinationPath $expanded
    foreach ($required in @('QuantaTray.exe', 'portable.flag', 'data\README.txt',
                           'locales\ja-JP.json', 'locales\en-US.json', 'README.txt',
                           'README.md', 'LICENSE', 'PRIVACY.md', 'THIRD-PARTY-NOTICES.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $expanded $required) -PathType Leaf)) {
            throw "Missing portable file: $required"
        }
    }
    $expectedPortable = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in Get-ChildItem -LiteralPath $publish -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath($publish, $file.FullName)
        [void]$expectedPortable.Add($relative)
        $copy = Join-Path $expanded $relative
        if (-not (Test-Path -LiteralPath $copy -PathType Leaf) -or
            (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) {
            throw "Portable publish-output mismatch: $relative"
        }
    }
    foreach ($extra in @('portable.flag', 'data\README.txt', 'README.txt', 'README.md',
                         'LICENSE', 'PRIVACY.md', 'THIRD-PARTY-NOTICES.txt')) {
        [void]$expectedPortable.Add($extra)
    }
    foreach ($file in Get-ChildItem -LiteralPath $expanded -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath($expanded, $file.FullName)
        if (-not $expectedPortable.Contains($relative)) { throw "Unexpected portable file: $relative" }
    }
    Assert-AppMetadata (Join-Path $expanded 'QuantaTray.exe')
    # This exits before constructing the app context: no Codex, login, or session scan.
    $process = Start-Process -FilePath (Join-Path $expanded 'QuantaTray.exe') -ArgumentList '--shutdown' -PassThru
    if (-not $process.WaitForExit(30000)) {
        $process.Kill($true)
        throw 'Portable host smoke check timed out.'
    }
    if ($process.ExitCode -ne 0) { throw "Portable host smoke check failed: $($process.ExitCode)" }
    Write-Host 'Portable host/WinForms smoke check passed; no installer execution or signed-in UI test performed.'
}
finally {
    if (Test-Path -LiteralPath $expanded) { Remove-Item -LiteralPath $expanded -Recurse -Force }
}

foreach ($file in @($setup, (Join-Path $publish 'QuantaTray.exe'))) {
    Write-Host "Authenticode status ($([IO.Path]::GetFileName($file))): $((Get-AuthenticodeSignature -LiteralPath $file).Status)"
}
Get-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') | Write-Host
Write-Host 'Release package verification passed.'
