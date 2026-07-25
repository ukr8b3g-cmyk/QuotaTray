# Building QuantaTray

## Requirements

- Windows 11 x64
- .NET 10 SDK
- Inno Setup 6 (`ISCC.exe`) for the installer
- PowerShell 5.1 or later

## Test

```powershell
dotnet restore QuantaTrain.slnx
dotnet build QuantaTrain.slnx -c Release --no-restore
dotnet test QuantaTrain.slnx -c Release --no-build
```

## Build release assets

```powershell
.\packaging\scripts\build-release.ps1 -Version 0.1.3
```

Outputs:

```text
dist/QuantaTray-v0.1.3-win-x64-setup.exe
dist/QuantaTray-v0.1.3-win-x64-portable.zip
dist/SHA256SUMS.txt
```

The publish is self-contained, single-file, x64, untrimmed, and unsigned unless
signing is added outside the repository. The portable archive includes
`portable.flag`, an empty `data/` directory, the quick and full README files,
localized strings, license, and
third-party notices.

For UI automation only, launch a test copy with `--qa-window` to expose panel
windows in the taskbar. Normal launches remain tray-only.
