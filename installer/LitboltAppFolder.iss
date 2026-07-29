; Inno Setup — Litbolt's App Folder
; Build: scripts\build-installer.ps1 -App AppFolder

#define AppName        "Litbolt's App Folder"
#define AppShortName   "LitboltAppFolder"
#ifndef AppVersion
  #ifexist "version.inc"
    #include "version.inc"
  #endif
#endif
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#define AppPublisher   "Litbolt"
#define AppExe         "LitboltAppFolder.exe"
#define PublishDir     "..\src\LitboltAppFolder\bin\Publish\win-x64"

[Setup]
AppId={{A4E8C1F2-9B3D-4E7A-8C5F-1D2E3F4A5B6C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppVerName={#AppName} {#AppVersion}
DefaultDirName={localappdata}\Programs\{#AppShortName}
DefaultGroupName={#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
MinVersion=10.0.17763
OutputDir=Output
OutputBaseFilename={#AppShortName}-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
SetupIconFile=..\src\LitboltAppFolder\app.ico
InfoBeforeFile=legal\notice-before-install-appfolder.txt
LicenseFile=legal\terms-license.txt
InfoAfterFile=legal\notice-after-install.txt

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
const
  APP_UNINSTALL_SUBKEY = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{A4E8C1F2-9B3D-4E7A-8C5F-1D2E3F4A5B6C}_is1';
  TERMS_VERSION = '1';
  REG_APP_KEY = 'Software\{#AppShortName}';
  APP_DISPLAY = 'Litbolt''s App Folder';

function GetExistingDisplayVersion: String;
begin
  Result := '';
  RegQueryStringValue(HKCU, APP_UNINSTALL_SUBKEY, 'DisplayVersion', Result);
  Result := Trim(Result);
end;

function IsExistingInstallLikely: Boolean;
begin
  Result := (GetExistingDisplayVersion <> '') or
    FileExists(ExpandConstant('{localappdata}') + '\Programs\{#AppShortName}\{#AppExe}');
end;

function GetAcceptedTermsVersion: String;
begin
  Result := '';
  if RegQueryStringValue(HKCU, REG_APP_KEY, 'TermsAcceptedVersion', Result) then
    Result := Trim(Result);
end;

procedure SetAcceptedTermsVersion(const Ver: String);
begin
  RegWriteStringValue(HKCU, REG_APP_KEY, 'TermsAcceptedVersion', Ver);
end;

function ShouldSkipLegalPagesOnUpdate: Boolean;
begin
  Result := IsExistingInstallLikely and (CompareText(GetAcceptedTermsVersion, TERMS_VERSION) = 0);
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if not ShouldSkipLegalPagesOnUpdate then
    Exit;
  if (PageID = wpLicense) or (PageID = wpInfoBefore) then
    Result := True;
end;

procedure ApplyWizardCaption;
var
  Ev: String;
begin
  Ev := GetExistingDisplayVersion;
  if Ev <> '' then
    WizardForm.Caption := APP_DISPLAY + ' Setup - update from ' + Ev + ' to {#AppVersion}'
  else if IsExistingInstallLikely then
    WizardForm.Caption := APP_DISPLAY + ' Setup - reinstall / update to {#AppVersion}'
  else
    WizardForm.Caption := APP_DISPLAY + ' Setup - {#AppVersion}';
end;

procedure InitializeWizard;
begin
  ApplyWizardCaption;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  ApplyWizardCaption;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    SetAcceptedTermsVersion(TERMS_VERSION);
end;
