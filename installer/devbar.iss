; Inno Setup script for Windows DevBar.
; Build the app first:
;   dotnet publish src/DevBar     -c Release -r win-x64 --self-contained -o publish/app
;   dotnet publish src/DevBar.Cli -c Release -r win-x64 --self-contained -o publish/cli
; Then compile this script with Inno Setup 6+ (iscc installer\devbar.iss).

#define AppName "DevBar"
#define AppVersion "1.0.0"
#define AppPublisher "DevBar"
#define AppExe "DevBar.exe"

[Setup]
AppId={{8B1F2C9E-4D3A-4E6B-9C21-7F5A8D2E6B41}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=DevBar-{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
; Per-user install needs no admin rights, matching the app's non-elevated design.
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startupicon"; Description: "Start {#AppName} when I sign in"; GroupDescription: "Startup"

[Files]
Source: "..\publish\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\publish\cli\devbar.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: startupicon

[Registry]
; devbar:// deep links. HKCU so no elevation is required.
Root: HKCU; Subkey: "Software\Classes\devbar"; ValueType: string; ValueName: ""; ValueData: "URL:DevBar Protocol"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\devbar"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\devbar\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""

; Put the CLI on PATH for the current user.
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; Check: NeedsPathEntry(ExpandConstant('{app}'))

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
function NeedsPathEntry(Dir: string): Boolean;
var
  ExistingPath: string;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', ExistingPath) then
  begin
    Result := True;
    exit;
  end;
  Result := Pos(';' + Uppercase(Dir) + ';', ';' + Uppercase(ExistingPath) + ';') = 0;
end;
