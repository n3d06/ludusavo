#define MyAppName "sav"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "3ky4r0"
#define MyAppURL "https://github.com/3ky4r0/ludusavo"
#define MyAppExeName "sav.exe"

[Setup]
AppId={{9C52E53E-74B0-4A9B-983C-C04388BDE85A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=artifacts
OutputBaseFilename=sav-{#MyAppVersion}-setup
SetupIconFile=assets\icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Code]
const
  EnvironmentKey = 'Environment';

procedure AddPathToEnv();
var
  Paths: string;
  AppDir: string;
begin
  AppDir := ExpandConstant('{app}');
  if RegQueryStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', Paths) then
  begin
    if Pos(';' + Uppercase(AppDir) + ';', ';' + Uppercase(Paths) + ';') = 0 then
    begin
      if (Length(Paths) > 0) and (Paths[Length(Paths)] <> ';') then
        Paths := Paths + ';';
      Paths := Paths + AppDir;
      RegWriteStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', Paths);
    end;
  end
  else
  begin
    RegWriteStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', AppDir);
  end;
end;

procedure RemovePathFromEnv();
var
  Paths, NewPaths, AppDir, P: string;
  PosIdx: Integer;
begin
  AppDir := ExpandConstant('{app}');
  if RegQueryStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', Paths) then
  begin
    P := ';' + Paths + ';';
    PosIdx := Pos(';' + Uppercase(AppDir) + ';', Uppercase(P));
    if PosIdx > 0 then
    begin
      Delete(P, PosIdx, Length(AppDir) + 1);
      NewPaths := Copy(P, 2, Length(P) - 2);
      RegWriteStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', NewPaths);
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    AddPathToEnv();
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RemovePathFromEnv();
  end;
end;
