; BinancePulse Installer Script
[Setup]
AppId={{B9E8F2A1-5C7D-4A3E-8F2C-9D7E5B4A3C2F}}
AppName=BinancePulse
AppVersion={#MyAppVersion}
AppPublisher=Kuper-666
AppPublisherURL=https://github.com/Kuper-666/BinancePulse
AppSupportURL=https://github.com/Kuper-666/BinancePulse
AppUpdatesURL=https://github.com/Kuper-666/BinancePulse/releases
DefaultDirName={autopf}\BinancePulse
DefaultGroupName=BinancePulse
AllowNoIcons=yes
LicenseFile=LICENSE
UninstallDisplayIcon={app}\BinancePulse.exe
Compression=lzma2
SolidCompression=yes
OutputDir=.\Installers
OutputBaseFilename=BinancePulse_Setup
WizardStyle=modern
DisableProgramGroupPage=no
DisableReadyPage=yes
DisableFinishedPage=no
UsePreviousAppDir=yes
AlwaysRestart=no
RestartIfNeededByRun=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: ".\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\BinancePulse"; Filename: "{app}\BinancePulse.exe"; IconFilename: "{app}\BinancePulse.exe"
Name: "{group}\Uninstall BinancePulse"; Filename: "{uninstallexe}"
Name: "{autodesktop}\BinancePulse"; Filename: "{app}\BinancePulse.exe"; Tasks: desktopicon; IconFilename: "{app}\BinancePulse.exe"

[Tasks]
Name: desktopicon; Description: "Создать значок на рабочем столе"; GroupDescription: "Дополнительные значки:"; Flags: unchecked

[Run]
Filename: "{app}\BinancePulse.exe"; Description: "Запустить BinancePulse"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Data"
Type: filesandordirs; Name: "{app}\Logs"

[Code]
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if FileExists(ExpandConstant('{app}\BinancePulse.exe')) then
    if ShellExec('', ExpandConstant('{app}\BinancePulse.exe'), '', '', SW_SHOWNORMAL, ewNoWait, ResultCode) then
    begin
      MsgBox('BinancePulse уже запущен. Закройте его перед установкой.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    if MsgBox('Удалить сохранённые настройки и данные (Data, Logs)?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      DelTree(ExpandConstant('{app}\Data'), True, True, True);
      DelTree(ExpandConstant('{app}\Logs'), True, True, True);
    end;
  end;
end;