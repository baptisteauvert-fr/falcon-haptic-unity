[Setup]
AppId=NovintFalconDiagnostics
AppName=Novint Falcon
AppVersion=1.4.2
DefaultDirName={localappdata}\Programs\Novint Falcon
DefaultGroupName=Novint Falcon
DisableProgramGroupPage=yes
OutputDir=..\Distribution
OutputBaseFilename=Falcon-Setup-1.4.2-x64
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
MinVersion=10.0.17763
WizardStyle=modern
Compression=lzma2/fast
SolidCompression=yes
CloseApplications=no
RestartApplications=no
Uninstallable=not ExtractOnly
UsePreviousAppDir=not ExtractOnly
UninstallDisplayIcon={app}\Falcon.Diagnostics.exe

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
french.Watch=Surveiller le branchement du Falcon à l’ouverture de ma session
english.Watch=Watch for the Falcon at Windows sign-in
french.RunDiagnostics=Ouvrir Falcon Diagnostics
english.RunDiagnostics=Open Falcon Diagnostics
french.CloseFalcon=Quittez Falcon Diagnostics depuis son icône près de l’horloge, puis relancez cette opération.
english.CloseFalcon=Exit Falcon Diagnostics from its notification-area icon, then retry.

[Tasks]
Name: "watch"; Description: "{cm:Watch}"; Flags: unchecked; Check: not ExtractOnly

[Files]
Source: "..\Distribution\Falcon-Diagnostics-1.4.2-x64.exe"; DestDir: "{app}"; DestName: "Falcon.Diagnostics.exe"; Flags: ignoreversion
Source: "..\Distribution\com.falcon.haptics-1.1.0.tgz"; DestDir: "{app}\Unity"; Flags: ignoreversion
Source: "..\Unity\com.falcon.haptics\README.md"; DestDir: "{app}\Unity"; DestName: "Guide Unity.txt"; Flags: ignoreversion
Source: "{src}\sdk-3.17.7-win-x64.exe"; DestDir: "{app}"; Flags: external skipifsourcedoesntexist ignoreversion; Check: not ExtractOnly

[Icons]
Name: "{group}\Falcon Diagnostics"; Filename: "{app}\Falcon.Diagnostics.exe"; Check: not ExtractOnly
Name: "{group}\SDK Unity"; Filename: "{app}\Unity"; Check: not ExtractOnly

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Falcon Diagnostics"; ValueData: """{app}\Falcon.Diagnostics.exe"" --watch"; Tasks: watch; Check: not ExtractOnly

[Run]
Filename: "{app}\Falcon.Diagnostics.exe"; Description: "{cm:RunDiagnostics}"; Flags: postinstall nowait skipifsilent; Check: not ExtractOnly

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  RunValue = 'Falcon Diagnostics';

function ExtractOnly: Boolean;
begin
  Result := ExpandConstant('{param:extract-only|0}') = '1';
end;

function FalconClosed: Boolean;
begin
  Result := not CheckForMutexes('Local\Falcon.Diagnostics');
  if not Result then
    MsgBox(CustomMessage('CloseFalcon'), mbInformation, MB_OK);
end;

function InitializeSetup: Boolean;
begin
  if ExtractOnly then
    Result := True
  else
    Result := FalconClosed;
end;

function InitializeUninstall: Boolean;
begin
  Result := FalconClosed;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, RunKey, RunValue, Command) then
      if CompareText(Command, '"' + ExpandConstant('{app}\Falcon.Diagnostics.exe') + '" --watch') = 0 then
        RegDeleteValue(HKCU, RunKey, RunValue);
end;
