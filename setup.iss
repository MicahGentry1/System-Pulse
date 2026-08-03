; Inno Setup Script for System Monitor (SYSTEM PULSE)
[Setup]
AppName=SYSTEM PULSE System Monitor
AppVersion=1.0.0
AppPublisher=Antigravity C# Engineering
DefaultDirName={autopf}\SystemMonitor
DefaultGroupName=SYSTEM PULSE
OutputBaseFilename=SystemMonitorSetup_v1.0.0
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}";

[Files]
Source: "publish\SystemMonitor.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\wwwroot\*"; DestDir: "{app}\wwwroot"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "publish\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\SYSTEM PULSE System Monitor"; Filename: "{app}\SystemMonitor.exe"
Name: "{autodesktop}\SYSTEM PULSE System Monitor"; Filename: "{app}\SystemMonitor.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SystemMonitor.exe"; Description: "Launch SYSTEM PULSE System Monitor"; Flags: nowait postinstall skipifsilent
