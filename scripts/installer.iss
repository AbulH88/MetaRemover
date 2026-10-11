[Setup]
AppId={{D28B519A-25FC-44AD-93A3-3A5AC85594F1}
AppName=MetaRemover
AppVersion=1.3.0
AppPublisher=MetaRemover
DefaultDirName={localappdata}\Programs\MetaRemover
DefaultGroupName=MetaRemover
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=MetaRemover-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\MetaRemover.exe
LicenseFile=..\LICENSE
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "..\dist\MetaRemover\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\MetaRemover"; Filename: "{app}\MetaRemover.exe"
Name: "{autodesktop}\MetaRemover"; Filename: "{app}\MetaRemover.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\MetaRemover.exe"; Description: "Launch MetaRemover"; Flags: nowait postinstall skipifsilent
