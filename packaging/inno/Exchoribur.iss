; Exchoribur 的 Windows 安装脚本(Inno Setup 6.5 以上)。
;
; 打包入口是 scripts/package-win.ps1,它把这几项传进来:
;   /DMyAppVersion=0.1.1-beta.1     版本号,带预发布后缀也照实写
;   /DMyAppVersionInfo=0.1.1.0      四段数字,只给 exe 的文件属性用
;   /DMySourceDir=<绝对路径>        dotnet publish 出来的程序目录
;
; 应用内自动更新会拿这个安装器静默覆盖安装(见 App 的 GithubReleaseUpdateFeed),
; 所以这里要用固定的 AppId 和固定的产物名,不能随便改。

#define MyAppName "Exchoribur"
#define MyAppPublisher "NaBCberry"
#define MyAppUrl "https://github.com/NaBCberry/Exchoribur"
#define MyAppExeName "Exchoribur.exe"

[Setup]
; 固定 AppId:升级时认得出旧版本,卸载项也不会重复
AppId={{7C3E1F4A-9B26-4D58-8E71-2A5C6D0B9E13}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
AppUpdatesURL={#MyAppUrl}
VersionInfoVersion={#MyAppVersionInfo}

; 默认每用户安装(免管理员);向导里可以改成"为所有用户",那时才提权
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#MyAppName}
DisableDirPage=no
; 重装默认沿用上次的目录,但向导里仍然可以改
UsePreviousAppDir=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

WizardStyle=modern
SetupIconFile=..\..\src\Exchoribur.App\Assets\icon-symbol.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=..\..\LICENSE

Compression=lzma2/max
SolidCompression=yes
; 注意:OutputDir 是打包脚本共用的产物目录。想单独试编译(比如只为验证语言文件)时,
; 一定要用假源目录 + /O<临时目录>,否则会把真正的安装包覆盖成一个空壳。
OutputDir=..\..\artifacts\releases
OutputBaseFilename=Exchoribur-win-Setup

; 静默覆盖安装时,让安装器把还在跑的旧版本关掉,但不要自己拉起来——
; 重启由应用那边决定(见 GithubReleaseUpdateFeed)。
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "chinese"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; 这是"安装版"的标记:应用据此判断能不能自更新(便携包和开发运行没有这个文件)
[INI]
Filename: "{app}\install.ini"; Section: "install"; Key: "version"; String: "{#MyAppVersion}"

[Run]
; 不加 skipifsilent:应用内更新就是用 /SILENT 跑这个安装器,装完要能把新版拉起来
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
