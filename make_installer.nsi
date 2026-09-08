; -------------------------------------------------------------------------
; Lenovo Legion Toolkit - NSIS Modern Installer Script
; Specially refined for Lenovo LOQ (including LOQ 15IRX10) and Legion series
; -------------------------------------------------------------------------

!ifndef VERSION
  !define VERSION "2.35.4.6"
!endif

!ifndef NUMERIC_VERSION
  !define NUMERIC_VERSION "2.35.4.6"
!endif

!ifndef BUILD_DATE
  !define BUILD_DATE "20260908"
!endif

!define PRODUCT_NAME "Lenovo Legion Toolkit"
!define PRODUCT_NAME_COMPACT "LenovoLegionToolkit"
!define PRODUCT_PUBLISHER "LenovoLegionToolkit-Team"
!define PRODUCT_WEB_SITE "https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit"
!define PRODUCT_EXE "Lenovo Legion Toolkit.exe"
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\App Paths\${PRODUCT_EXE}"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME_COMPACT}"
!define PRODUCT_MUTEX "LenovoLegionToolkit_Mutex_6efcc882-924c-4cbc-8fec-f45c25696f98"
!define LOQ_DISCORD "https://discord.gg/3GKzQtwdNf"
!define LEGION_DISCORD "https://discord.com/invite/legionseries"

; General NSIS attributes
Unicode true
SetCompressor /SOLID lzma
RequestExecutionLevel admin

Name "${PRODUCT_NAME} v${VERSION}"
OutFile "build_installer\LenovoLegionToolkitSetup-v${VERSION}_Build${BUILD_DATE}_NSIS.exe"
InstallDir "$PROGRAMFILES64\${PRODUCT_NAME_COMPACT}"
InstallDirRegKey HKLM "${PRODUCT_DIR_REGKEY}" ""

; Version information
VIProductVersion "${NUMERIC_VERSION}"
VIAddVersionKey "ProductName" "${PRODUCT_NAME}"
VIAddVersionKey "CompanyName" "${PRODUCT_PUBLISHER}"
VIAddVersionKey "LegalCopyright" "© 2026 Bartosz Cichecki, Kaguya, and Dr. Skinner"
VIAddVersionKey "FileDescription" "${PRODUCT_NAME} Setup (Refined for LOQ 15IRX10)"
VIAddVersionKey "FileVersion" "${NUMERIC_VERSION}"
VIAddVersionKey "ProductVersion" "${VERSION}"

; Modern UI 2 Configuration
!include "MUI2.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"

!define MUI_ICON "InnoDependencies\Images\setup_icon.ico"
!define MUI_UNICON "InnoDependencies\Images\setup_icon.ico"
!define MUI_ABORTWARNING

; Welcome page
!define MUI_WELCOMEPAGE_TITLE "Welcome to ${PRODUCT_NAME} Setup"
!define MUI_WELCOMEPAGE_TEXT "This will install ${PRODUCT_NAME} v${VERSION} on your computer.$\r$\n$\r$\nSpecially refined with custom 24-Zone RGB and Spectrum support for Lenovo LOQ (15IRX10) and Legion series.$\r$\n$\r$\nClick Next to continue."
!insertmacro MUI_PAGE_WELCOME

; License page
!insertmacro MUI_PAGE_LICENSE "LICENSE"

; Components selection page
!define MUI_COMPONENTSPAGE_NODESC
!insertmacro MUI_PAGE_COMPONENTS

; Destination folder page
!insertmacro MUI_PAGE_DIRECTORY

; Installation progress page
!insertmacro MUI_PAGE_INSTFILES

; Finish page
!define MUI_FINISHPAGE_RUN "$INSTDIR\${PRODUCT_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Launch ${PRODUCT_NAME}"
!define MUI_FINISHPAGE_SHOWREADME "${LOQ_DISCORD}"
!define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Join LOQ Series Discord Community"
!define MUI_FINISHPAGE_LINK "Visit GitHub Repository"
!define MUI_FINISHPAGE_LINK_LOCATION "${PRODUCT_WEB_SITE}"
!insertmacro MUI_PAGE_FINISH

; Uninstaller pages
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

; Languages
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Indonesian"

; Section: Core Application (Mandatory)
Section "!Lenovo Legion Toolkit (Core)" SecMain
  SectionIn RO
  SetRegView 64

  DetailPrint "Stopping existing processes..."
  nsExec::Exec 'taskkill /F /IM "${PRODUCT_EXE}"'

  DetailPrint "Cleaning legacy installations if present..."
  RMDir /r "$LOCALAPPDATA\Programs\${PRODUCT_NAME_COMPACT}"
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\{0C37B9AC-9C3D-4302-8ABB-125C7C7D83D5}_is1"
  Delete "$INSTDIR\unins000.exe"
  Delete "$INSTDIR\unins000.dat"

  SetOutPath "$INSTDIR"
  DetailPrint "Extracting files..."
  File /r "build\*.*"
  File "LICENSE"

  ; Create uninstaller executable
  WriteUninstaller "$INSTDIR\uninstall.exe"

  ; Registry: App Path
  WriteRegStr HKLM "${PRODUCT_DIR_REGKEY}" "" "$INSTDIR\${PRODUCT_EXE}"
  WriteRegStr HKLM "${PRODUCT_DIR_REGKEY}" "Path" "$INSTDIR"

  ; Registry: Uninstall information (Apps & Features / Control Panel)
  WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "DisplayName" "${PRODUCT_NAME}"
  WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\uninstall.exe" /S'
  WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\${PRODUCT_EXE}"
  WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
  WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "HelpLink" "${PRODUCT_WEB_SITE}"
  WriteRegStr HKLM "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
  WriteRegDWORD HKLM "${PRODUCT_UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${PRODUCT_UNINST_KEY}" "NoRepair" 1

  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKLM "${PRODUCT_UNINST_KEY}" "EstimatedSize" "$0"
SectionEnd

; Section: Start Menu Shortcut
Section "Start Menu Shortcut" SecStartMenu
  SetRegView 64
  CreateDirectory "$SMPROGRAMS\${PRODUCT_NAME}"
  CreateShortcut "$SMPROGRAMS\${PRODUCT_NAME}\${PRODUCT_NAME}.lnk" "$INSTDIR\${PRODUCT_EXE}" "" "$INSTDIR\${PRODUCT_EXE}" 0
  CreateShortcut "$SMPROGRAMS\${PRODUCT_NAME}\Uninstall ${PRODUCT_NAME}.lnk" "$INSTDIR\uninstall.exe" "" "$INSTDIR\uninstall.exe" 0
SectionEnd

; Section: Desktop Shortcut
Section "Desktop Shortcut" SecDesktop
  SetRegView 64
  CreateShortcut "$DESKTOP\${PRODUCT_NAME}.lnk" "$INSTDIR\${PRODUCT_EXE}" "" "$INSTDIR\${PRODUCT_EXE}" 0
SectionEnd

; Section: Windows Dynamic Lighting & Sparse Identity
Section "Windows Dynamic Lighting (LampArray)" SecIdentity
  SetRegView 64
  DetailPrint "Configuring application package identity..."
  
  ; Remove previous identity
  nsExec::Exec 'powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Get-AppxPackage -Name ''eef45acd-2cf3-4d7d-9d33-92f37c74cc31'' | Remove-AppxPackage -ErrorAction SilentlyContinue"'
  
  ; Import certificate if present
  nsExec::Exec 'powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "if (Test-Path ''$INSTDIR\LenovoLegionToolkit.cer'') { Import-Certificate -FilePath ''$INSTDIR\LenovoLegionToolkit.cer'' -CertStoreLocation ''Cert:\LocalMachine\TrustedPeople'' -ErrorAction SilentlyContinue; Import-Certificate -FilePath ''$INSTDIR\LenovoLegionToolkit.cer'' -CertStoreLocation ''Cert:\LocalMachine\Root'' -ErrorAction SilentlyContinue }"'
  
  ; Register Sparse Package identity (MSIX or Manifest)
  nsExec::Exec 'powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "if (Test-Path ''$INSTDIR\LenovoLegionToolkit.LampArray.msix'') { Add-AppxPackage -Path ''$INSTDIR\LenovoLegionToolkit.LampArray.msix'' -ExternalLocation ''$INSTDIR'' -ErrorAction SilentlyContinue } elseif (Test-Path ''$INSTDIR\AppxManifest.xml'') { Add-AppxPackage -Register ''$INSTDIR\AppxManifest.xml'' -ExternalLocation ''$INSTDIR'' -ErrorAction SilentlyContinue }"'
SectionEnd

; Uninstaller Section
Section "Uninstall"
  SetRegView 64

  DetailPrint "Terminating ${PRODUCT_NAME}..."
  nsExec::Exec 'taskkill /F /IM "${PRODUCT_EXE}"'

  DetailPrint "Removing autorun tasks..."
  nsExec::Exec 'schtasks /Delete /TN "LenovoLegionToolkit_Autorun_6efcc882-924c-4cbc-8fec-f45c25696f98" /F'

  DetailPrint "Removing package identity..."
  nsExec::Exec 'powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Get-AppxPackage -Name ''eef45acd-2cf3-4d7d-9d33-92f37c74cc31'' | Remove-AppxPackage -ErrorAction SilentlyContinue"'
  nsExec::Exec 'powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem Cert:\LocalMachine\Root | Where-Object { $$_.Subject -match ''LenovoLegionToolkit'' } | Remove-Item -Force -ErrorAction SilentlyContinue"'
  nsExec::Exec 'powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object { $$_.Subject -match ''LenovoLegionToolkit'' } | Remove-Item -Force -ErrorAction SilentlyContinue"'

  DetailPrint "Removing shortcuts..."
  Delete "$DESKTOP\${PRODUCT_NAME}.lnk"
  Delete "$SMPROGRAMS\${PRODUCT_NAME}\${PRODUCT_NAME}.lnk"
  Delete "$SMPROGRAMS\${PRODUCT_NAME}\Uninstall ${PRODUCT_NAME}.lnk"
  RMDir "$SMPROGRAMS\${PRODUCT_NAME}"

  DetailPrint "Removing files..."
  RMDir /r "$INSTDIR"

  DetailPrint "Cleaning registry..."
  DeleteRegKey HKLM "${PRODUCT_UNINST_KEY}"
  DeleteRegKey HKLM "${PRODUCT_DIR_REGKEY}"
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\{0C37B9AC-9C3D-4302-8ABB-125C7C7D83D5}_is1"
  Delete "$INSTDIR\unins000.exe"
  Delete "$INSTDIR\unins000.dat"

  ; Ask user if they wish to keep or delete settings
  MessageBox MB_YESNO|MB_ICONQUESTION "Do you want to delete all Lenovo Legion Toolkit settings, customizations, and configurations?" IDNO skip_settings
  RMDir /r "$LOCALAPPDATA\${PRODUCT_NAME_COMPACT}"

skip_settings:
SectionEnd

; Initialization Functions
Function .onInit
  ; Require 64-bit Windows
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "This application requires a 64-bit version of Windows."
    Abort
  ${EndIf}

  ; Check if running via mutex
  System::Call 'kernel32::OpenMutex(i 0x100000, i 0, t "${PRODUCT_MUTEX}") i .r0'
  IntCmp $0 0 not_running
  System::Call 'kernel32::CloseHandle(i r0)'
  MessageBox MB_OKCANCEL|MB_ICONQUESTION "${PRODUCT_NAME} is currently running.$\r$\n$\r$\nClick OK to automatically close it and proceed with the installation." IDCANCEL abort_install
  nsExec::Exec 'taskkill /F /IM "${PRODUCT_EXE}"'
  Sleep 1000
  Goto not_running

abort_install:
  Abort

not_running:
  ; Check .NET 9 Desktop Runtime
  ; Method 1: Check filesystem directly in 64-bit Program Files
  FindFirst $3 $4 "$PROGRAMFILES64\dotnet\shared\Microsoft.WindowsDesktop.App\9.*"
  StrCmp $4 "" check_dotnet_cmd 0
  FindClose $3
  Goto dotnet_ok

check_dotnet_cmd:
  FindClose $3
  ; Method 2: Check via findstr exit code (avoids 1024-char buffer truncation)
  nsExec::Exec 'cmd.exe /C "dotnet --list-runtimes | findstr /C:\"Microsoft.WindowsDesktop.App 9.\""'
  Pop $0
  IntCmp $0 0 dotnet_ok

  MessageBox MB_YESNO|MB_ICONEXCLAMATION "Microsoft .NET Desktop Runtime 9 is required to run ${PRODUCT_NAME}, but was not detected.$\r$\n$\r$\nWould you like to open the .NET download page now?" IDNO dotnet_ok
  ExecShell "open" "https://dotnet.microsoft.com/download/dotnet/9.0/runtime"

dotnet_ok:
FunctionEnd

Function un.onInit
  System::Call 'kernel32::OpenMutex(i 0x100000, i 0, t "${PRODUCT_MUTEX}") i .r0'
  IntCmp $0 0 not_running_un
  System::Call 'kernel32::CloseHandle(i r0)'
  MessageBox MB_OKCANCEL|MB_ICONQUESTION "${PRODUCT_NAME} is currently running.$\r$\n$\r$\nClick OK to automatically close it and proceed with uninstallation." IDCANCEL abort_uninst
  nsExec::Exec 'taskkill /F /IM "${PRODUCT_EXE}"'
  Sleep 1000
  Goto not_running_un

abort_uninst:
  Abort

not_running_un:
FunctionEnd
