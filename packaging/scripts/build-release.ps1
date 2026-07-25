param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,
    [string]$IsccPath
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'publish.ps1') -Runtime win-x64 -Version $Version
& (Join-Path $PSScriptRoot 'build-installer.ps1') -Runtime win-x64 -Version $Version -IsccPath $IsccPath
& (Join-Path $PSScriptRoot 'checksums.ps1') -Version $Version
