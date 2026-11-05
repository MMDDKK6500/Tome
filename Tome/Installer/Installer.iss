#define AppName "Tome"
#define AppPublisher "Urav Advanced Learning Systems Pvt Ltd"
#define AppURL "https://github.com/uralstech/Tome"
#define AppSupportURL "https://github.com/uralstech/Tome/issues"
#define AppUpdatesURL "https://github.com/uralstech/Tome/releases"

#ifndef AppVersion
    #error App version not specified
#endif

#ifndef AppExeName
    #define AppExeName "Tome.exe"
#endif

#ifndef AppBinariesPath
    #error Binaries path not specified
#endif

#ifndef AppIconPath
    #error Icon path not specified
#endif

#ifndef InstallerBaseFileName
    #error Installer name not specified
#endif

#ifndef InstallerOutputFolder
    #error Installer output folder not specified
#endif

#define DoubleAmp(Value) StringChange(Value, "&", "&&")
#define EscapeConstArgument(Value) StringChange(StringChange(StringChange(Value, "%", "%25"), ",", "%2c"), "}", "%7d")

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{CA2D7A38-6D01-4795-A2EB-C5758489DA00}
AppName={#AppName}
AppVersion={#AppVersion}
;AppVerName={cm:NameAndVersion,{#EscapeConstArgument(AppName)},{#EscapeConstArgument(AppVersion)}}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppSupportURL}
AppUpdatesURL={#AppUpdatesURL}
DefaultDirName={autopf}\{#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupArchitecture=x64
DefaultGroupName={#AppName}
AllowNoIcons=yes
PrivilegesRequired=admin
OutputDir={#InstallerOutputFolder}
OutputBaseFilename={#InstallerBaseFileName}
SetupIconFile={#AppIconPath}
SolidCompression=yes
WizardStyle=classic

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#AppBinariesPath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Code]
#define Dependency_Evergreen
#include "CodeDependencies.iss"

function InitializeSetup: Boolean;
begin
  Dependency_AddWebView2;           // WebView2 Runtime

  #if BinaryType == "slim"
    Dependency_AddDotNet100Desktop;   // .NET Desktop Runtime for WPF
  #endif

  Result := True;
end;

[Registry]
Root: HKA; Subkey: "Software\Classes\Tome"; ValueType: string; ValueName: ""; ValueData: "URL:tome"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Tome"; ValueType: none; ValueName: "URL Protocol"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Tome\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Flags: uninsdeletekey

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#DoubleAmp(AppName)}}"; Flags: nowait postinstall skipifsilent

