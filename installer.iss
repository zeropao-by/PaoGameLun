; PaoGameLun - Inno Setup 安装脚本
; 鸣潮游戏启动器安装程序

#define MyAppName "PaoGameLun"
#define MyAppVersion "0.3.4"
#define MyAppPublisher "zeropao-by"
#define MyAppURL "https://github.com/zeropao-by/PaoGameLun"
#define MyAppExeName "PaoGameLun.exe"
#define SourceDir "C:\Users\Administrator\WorkBuddy\20260419231142\publish_v34"

[Setup]
; 应用程序基本信息
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultGroupName={#MyAppName}

; 输出设置
OutputDir=C:\Users\Administrator\WorkBuddy\20260419231142\installer
OutputBaseFilename=PaoGameLun_v{#MyAppVersion}_Setup
SetupIconFile=C:\Users\Administrator\WorkBuddy\20260419231142\GameLauncherNet\Assets\app.ico
Compression=lzma2/ultra64
SolidCompression=yes

; 安装设置
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline dialog

; UI 设置
WizardStyle=modern
WizardSizePercent=120
WizardResizable=no
ShowLanguageDialog=no

; 其他
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create desktop shortcut"; GroupDescription: "Additional icons:"; Flags: checkablealone
Name: "startupicon"; Description: "Auto start on boot"; GroupDescription: "Other options:"

[Files]
; 发布目录中的所有文件
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; 开始菜单
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
; 桌面快捷方式
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
; 开机启动
Name: "{autostartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
; 安装完成后可选运行
Filename: "{app}\{#MyAppExeName}"; Description: "Run {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; 卸载时清理用户数据
Type: filesandordirs; Name: "{userappdata}\{#MyAppName}"
