#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif
#define MyAppName "QuantaTrain"
#define MyAppExeName "QuantaTrain.exe"

[Setup]
AppId={{71F06D6A-8F00-4DE2-B117-C925F4446E85}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=QuantaTrain contributors
AppPublisherURL=https://github.com/ukr8b3g-cmyk/QuotaTray
DefaultDirName={localappdata}\Programs\QuantaTrain
DefaultGroupName=QuantaTrain
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\..\dist
OutputBaseFilename=QuantaTrain-v{#MyAppVersion}-win-x64-setup
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
Name: "{group}\QuantaTrain"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall QuantaTrain"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch QuantaTrain"; Flags: nowait postinstall skipifsilent

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  InstalledExe: String;
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
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    DataDir := ExpandConstant('{localappdata}\QuantaTrain');
    if DirExists(DataDir) and
       (MsgBox('Delete local QuantaTrain settings, history, and logs?',
         mbConfirmation, MB_YESNO) = IDYES) then
      DelTree(DataDir, True, True, True);
  end;
end;
