; NooXAIR - Inno Setup betigi (build.bat tarafindan derlenir)
#define AppName "NooXAIR"
#define AppVersion "3.0.0"
#ifndef Payload
  #define Payload "..\build\payload"
#endif

[Setup]
AppId={{6E0F3B5A-9C1D-4E7A-B2A4-7D1C0A3E5F21}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=NooXAIR
VersionInfoVersion={#AppVersion}
VersionInfoDescription=NooXAIR AirPlay Alıcısı Kurulumu
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableWelcomePage=no
OutputDir=..\dist
OutputBaseFilename=NooXAIR_Setup
SetupIconFile=..\assets\nooxair.ico
UninstallDisplayIcon={app}\NooXAIR.exe
UninstallDisplayName={#AppName}
WizardStyle=modern
WizardImageFile=..\assets\wizard.bmp
WizardSmallImageFile=..\assets\wizard_small.bmp
Compression=lzma2/max
SolidCompression=yes
LZMANumBlockThreads=4
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UsedUserAreasWarning=no
CloseApplications=force
RestartApplications=no
MinVersion=10.0
ShowLanguageDialog=no

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaüstü kısayolu oluştur"; GroupDescription: "Ek seçenekler:"
Name: "privatenet"; Description: "Ağ profilini 'Özel' yap (iPhone'un bilgisayarı bulması için önerilir)"; GroupDescription: "Ağ ve performans:"
Name: "wifiperf"; Description: "Wi-Fi güç tasarrufunu kapat (gecikme dalgalanmalarını azaltır)"; GroupDescription: "Ağ ve performans:"

[Files]
Source: "{#Payload}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\NooXAIR.exe"
Name: "{group}\{#AppName} Kaldır"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\NooXAIR.exe"; Tasks: desktopicon

[Registry]
; Windows acilisinda arka planda (tepside) otomatik baslat
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "NooXAIR"; ValueData: """{app}\NooXAIR.exe"" --tray"; Flags: uninsdeletevalue

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\postinstall.ps1"" -AppDir ""{app}""{code:PostArgs}"; StatusMsg: "Bonjour, güvenlik duvarı ve ağ ayarları yapılandırılıyor (gerekirse indiriliyor)..."; Flags: runhidden waituntilterminated
Filename: "{app}\NooXAIR.exe"; Description: "NooXAIR'i şimdi başlat"; Flags: postinstall nowait skipifsilent runasoriginaluser
Filename: "{app}\NooXAIR.exe"; Parameters: "--tray"; Flags: nowait runasoriginaluser; Check: WizardSilent

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\uninstall.ps1"""; Flags: runhidden waituntilterminated; RunOnceId: "NooXAIRCleanup"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\engine"

[Code]
function PostArgs(Param: String): String;
begin
  Result := '';
  if WizardIsTaskSelected('privatenet') then Result := Result + ' -PrivateNetwork';
  if WizardIsTaskSelected('wifiperf') then Result := Result + ' -WifiPerformance';
end;

procedure KillApp();
var rc: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM NooXAIR.exe /T', '', SW_HIDE, ewWaitUntilTerminated, rc);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM uxplay.exe /T', '', SW_HIDE, ewWaitUntilTerminated, rc);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  KillApp();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  KillApp();
  Result := True;
end;

