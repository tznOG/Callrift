#ifndef PackageDir
  #define PackageDir "..\dist\Callrift"
#endif
#ifndef AppVersion
  #define AppVersion GetStringFileInfo(PackageDir + "\Callrift.exe", "ProductVersion")
#endif
#ifndef AppIdentity
  #define AppIdentity "Callrift-04DC697F-6EBD-4CCD-B977-65B167AE2838"
#endif
#ifndef AppName
  #define AppName "Callrift"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif
#ifndef AppSessionMutex
  #define AppSessionMutex "Local\Callrift-Installer"
#endif

[Setup]
AppId={#AppIdentity}
AppName={#AppName}
AppVersion={#AppVersion}
VersionInfoVersion={#GetFileVersion(PackageDir + "\Callrift.exe")}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Callrift contributors
AppComments=VPN para Discord + Presence
DefaultDirName={localappdata}\Programs\Callrift
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=Callrift-Setup-{#AppVersion}
SetupIconFile=..\assets\callrift.ico
UninstallDisplayIcon={app}\Callrift.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardResizable=yes
AppMutex={#AppSessionMutex}
CloseApplications=no
RestartApplications=no
SetupMutex=Local\Callrift-Setup
LicenseFile=..\LICENSE
InfoBeforeFile=installer-notes.txt
UninstallLogMode=append

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; Flags: unchecked

[Files]
Source: "{#PackageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "installed.marker"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\Callrift.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\Callrift.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Callrift.exe"; Description: "Abrir {#AppName}"; Flags: nowait postinstall skipifsilent unchecked

[Code]
function InitializeSetup: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then
    SuppressibleMsgBox('O Callrift precisa do .NET Framework 4.8. Instale pelo site da Microsoft e execute este instalador novamente: https://dotnet.microsoft.com/download/dotnet-framework/net48', mbError, MB_OK, IDOK);
end;

function InitializeUninstall: Boolean;
var
  ExitCode: Integer;
begin
  if CheckForMutexes('{#AppSessionMutex}') then begin
    SuppressibleMsgBox('Saia do Callrift pelo menu da bandeja antes de desinstalar.', mbError, MB_OK, IDOK);
    Result := False;
    Exit;
  end;
  Result := True;
  if FileExists(ExpandConstant('{app}\Callrift.exe')) then begin
    Result := Exec(ExpandConstant('{app}\Callrift.exe'), '--uninstall-cleanup', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ExitCode);
    if Result then Result := ExitCode = 0;
    if not Result then
      SuppressibleMsgBox('Não foi possível concluir a limpeza da inicialização ou do componente de rede. Encerre o Callrift e aceite a confirmação de administrador quando solicitada. Depois tente novamente.', mbError, MB_OK, IDOK);
  end;
end;
