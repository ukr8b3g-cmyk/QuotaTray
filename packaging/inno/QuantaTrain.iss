#ifndef MyAppVersion
  #define MyAppVersion "0.2.0"
#endif
#define MyAppName "QuantaTray"
#define MyAppExeName "QuantaTray.exe"

[Setup]
AppId={{71F06D6A-8F00-4DE2-B117-C925F4446E85}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=QuantaTray contributors
AppPublisherURL=https://github.com/ukr8b3g-cmyk/QuotaTray
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
VersionInfoDescription={#MyAppName} Setup
DefaultDirName={localappdata}\Programs\QuantaTray
DefaultGroupName=QuantaTray
UsePreviousAppDir=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\..\dist
OutputBaseFilename=QuantaTray-v{#MyAppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=force
RestartApplications=no
LicenseFile=..\..\LICENSE

[Files]
Source: "..\..\dist\publish\win-x64\*"; DestDir: "{app}"; Excludes: "portable.flag,data\*"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\QuantaTray"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall QuantaTray"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch QuantaTray"; Flags: nowait postinstall skipifsilent

[InstallDelete]
Type: files; Name: "{localappdata}\Programs\QuantaTrain\QuantaTrain.exe"
Type: files; Name: "{localappdata}\Programs\QuantaTrain\unins000.dat"
Type: files; Name: "{localappdata}\Programs\QuantaTrain\unins000.exe"
Type: filesandordirs; Name: "{localappdata}\Programs\QuantaTrain\locales"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  InstalledExe: String;
  LegacyExe: String;
  ResultCode: Integer;
begin
  Result := '';
  InstalledExe := ExpandConstant('{app}\{#MyAppExeName}');
  if FileExists(InstalledExe) then
  begin
    Exec(
      InstalledExe,
      '--shutdown',
      ExpandConstant('{app}'),
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode);
    Sleep(1000);
  end;
  LegacyExe := ExpandConstant(
    '{localappdata}\Programs\QuantaTrain\QuantaTrain.exe');
  if FileExists(LegacyExe) then
  begin
    Exec(
      LegacyExe,
      '--shutdown',
      ExtractFileDir(LegacyExe),
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode);
    Exec(
      ExpandConstant('{sys}\taskkill.exe'),
      '/IM QuantaTrain.exe /T /F',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode);
    Sleep(500);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    DataDir := ExpandConstant('{localappdata}\QuantaTray');
    if DirExists(DataDir) and
       (MsgBox('Delete local QuantaTray settings, history, and logs?',
         mbConfirmation, MB_YESNO) = IDYES) then
      DelTree(DataDir, True, True, True);
  end;
end;
