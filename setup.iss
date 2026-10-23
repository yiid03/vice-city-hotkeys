#define AppName "Vice City Hotkeys"
#define AppVersion "1.0.0"

[Setup]
AppId={{596FF05B-8AB8-4F7F-A188-C884A90FC879}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
VersionInfoVersion=1.0.0.0
DefaultDirName={localappdata}\Programs\Vice City Hotkeys
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\ViceCityHotkeys.exe
PrivilegesRequired=lowest
MinVersion=10.0
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
OutputDir=dist
OutputBaseFilename=ViceCityHotkeys-1.0.0-Setup
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
AppMutex=Local\ViceCityHotkeys
SetupLogging=yes
LicenseFile=LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[CustomMessages]
english.DesktopShortcut=Create a desktop shortcut
turkish.DesktopShortcut=Masaüstü kısayolu oluştur
english.LaunchApp=Launch Vice City Hotkeys
turkish.LaunchApp=Vice City Hotkeys uygulamasını aç
english.FrameworkMissing=Microsoft .NET Framework 4.8 or later is required. Install it from Microsoft, then run this setup again.
turkish.FrameworkMissing=Microsoft .NET Framework 4.8 veya üzeri gerekiyor. Microsoft üzerinden yükledikten sonra bu kurulumu yeniden aç.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; Flags: unchecked

[Files]
Source: "build\ViceCityHotkeys.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "build\ViceCityHotkeys.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\ViceCityHotkeys.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\ViceCityHotkeys.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ViceCityHotkeys.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM32, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release);
  if Result then Result := Release >= 528040;
  if not Result then MsgBox(CustomMessage('FrameworkMissing'), mbError, MB_OK);
end;

