; Claude Code for Windows Installer - LITE VERSION
; Downloads Node.js and Git during installation
; NSIS Script

;--------------------------------
; Basic definitions
!define PRODUCT_NAME "Claude Code for Windows"
!define PRODUCT_VERSION "1.0.0"
!define PRODUCT_PUBLISHER "Claude Code Windows Team"
!define PRODUCT_WEB_SITE "https://github.com/anthropics/claude-code"
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\App Paths\ClaudeCodeWin.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
!define PRODUCT_UNINST_ROOT_KEY "HKLM"
!define PRODUCT_EXE "ClaudeCodeWin.exe"

; Download URLs
!define NODE_VERSION "20.11.1"
!define GIT_VERSION "2.47.1"
!define NODE_URL "https://nodejs.org/dist/v${NODE_VERSION}/node-v${NODE_VERSION}-win-x64.zip"
!define GIT_URL "https://github.com/git-for-windows/git/releases/download/v${GIT_VERSION}.windows.1/PortableGit-${GIT_VERSION}-64-bit.7z.exe"

;--------------------------------
; Compression
SetCompressor /SOLID lzma

;--------------------------------
; Includes
!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "LogicLib.nsh"
!include "NSISdl.nsh"

;--------------------------------
; MUI Settings
!define MUI_ABORTWARNING
!define MUI_ICON "resources\claude.ico"
!define MUI_UNICON "resources\claude.ico"

; Welcome page
!insertmacro MUI_PAGE_WELCOME

; License page
!insertmacro MUI_PAGE_LICENSE "LICENSE.txt"

; Directory page
!insertmacro MUI_PAGE_DIRECTORY

; Install page
!insertmacro MUI_PAGE_INSTFILES

; Finish page
!define MUI_FINISHPAGE_RUN "$INSTDIR\ClaudeCodeWin.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch Claude Code for Windows"
!insertmacro MUI_PAGE_FINISH

; Uninstall pages
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; Language - English only
!insertmacro MUI_LANGUAGE "English"

;--------------------------------
; Installer properties
Name "${PRODUCT_NAME} ${PRODUCT_VERSION} (Lite)"
OutFile "ClaudeCodeWin-Lite-${PRODUCT_VERSION}.exe"
InstallDir "$PROGRAMFILES64\ClaudeCodeWin"
InstallDirRegKey HKLM "${PRODUCT_DIR_REGKEY}" ""
ShowInstDetails show
ShowUnInstDetails show
RequestExecutionLevel admin

;--------------------------------
; Functions

Function CheckAndKillProcess
    nsExec::ExecToStack 'taskkill /F /IM ${PRODUCT_EXE}'
    Pop $0
    Pop $1
    Sleep 500
FunctionEnd

Function un.CheckAndKillProcess
    nsExec::ExecToStack 'taskkill /F /IM ${PRODUCT_EXE}'
    Pop $0
    Pop $1
    Sleep 500
FunctionEnd

;--------------------------------
; Installer init
Function .onInit
    FindWindow $0 "" "Claude Code for Windows"
    ${If} $0 != 0
        MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION \
            "Claude Code for Windows is currently running.$\n$\nClick OK to close it and continue installation, or Cancel to abort." \
            IDOK kill_app
        Abort
        kill_app:
            Call CheckAndKillProcess
    ${EndIf}
FunctionEnd

Function un.onInit
    FindWindow $0 "" "Claude Code for Windows"
    ${If} $0 != 0
        MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION \
            "Claude Code for Windows is currently running.$\n$\nClick OK to close it and continue uninstallation, or Cancel to abort." \
            IDOK un_kill_app
        Abort
        un_kill_app:
            Call un.CheckAndKillProcess
    ${EndIf}
FunctionEnd

;--------------------------------
; Install section

Section "Main Program" SEC_MAIN
    SectionIn RO

    Call CheckAndKillProcess

    SetOutPath "$INSTDIR"

    ; Copy main program files
    File /r "publish\*.*"

    ; Create data directory
    CreateDirectory "$APPDATA\ClaudeCodeWin"

    ; Download and extract Node.js
    DetailPrint "Downloading Node.js v${NODE_VERSION}..."
    NSISdl::download "${NODE_URL}" "$TEMP\nodejs.zip"
    Pop $0
    ${If} $0 != "success"
        MessageBox MB_OK|MB_ICONEXCLAMATION "Failed to download Node.js: $0$\n$\nPlease check your internet connection and try again."
        Abort
    ${EndIf}

    DetailPrint "Extracting Node.js..."
    SetOutPath "$INSTDIR\nodejs"
    nsExec::ExecToStack 'powershell -Command "Expand-Archive -Path $TEMP\nodejs.zip -DestinationPath $TEMP\nodejs-extract -Force; Copy-Item -Path $TEMP\nodejs-extract\node-v${NODE_VERSION}-win-x64\* -Destination $INSTDIR\nodejs -Recurse -Force"'
    Pop $0
    Delete "$TEMP\nodejs.zip"

    ; Download and extract Git
    DetailPrint "Downloading Git v${GIT_VERSION}..."
    NSISdl::download "${GIT_URL}" "$TEMP\portablegit.7z.exe"
    Pop $0
    ${If} $0 != "success"
        MessageBox MB_OK|MB_ICONEXCLAMATION "Failed to download Git: $0$\n$\nPlease check your internet connection and try again."
        Abort
    ${EndIf}

    DetailPrint "Extracting Git..."
    SetOutPath "$INSTDIR\git"
    nsExec::ExecToStack '7z x "$TEMP\portablegit.7z.exe" -o"$INSTDIR\git" -y'
    Pop $0
    ${If} $0 != 0
        ; Try using PowerShell with 7z if system 7z is not available
        nsExec::ExecToStack 'powershell -Command "& { $7z = (Get-Command 7z -ErrorAction SilentlyContinue).Source; if ($7z) { & $7z x $env:TEMP\portablegit.7z.exe -o$INSTDIR\git -y } else { Write-Error \"7z not found\" } }"'
        Pop $0
    ${EndIf}
    Delete "$TEMP\portablegit.7z.exe"

    ; Write uninstall info
    WriteUninstaller "$INSTDIR\uninstall.exe"
    WriteRegStr HKLM "${PRODUCT_DIR_REGKEY}" "" "$INSTDIR\ClaudeCodeWin.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\uninstall.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\ClaudeCodeWin.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"

    ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
    IntFmt $0 "0x%08X" $0
    WriteRegDWORD ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "EstimatedSize" "$0"

    ; Create Start Menu shortcuts
    SetShellVarContext all
    CreateDirectory "$SMPROGRAMS\${PRODUCT_NAME}"
    CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\Claude Code for Windows.lnk" "$INSTDIR\ClaudeCodeWin.exe"
    CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\Uninstall.lnk" "$INSTDIR\uninstall.exe"

    ; Create Desktop shortcut
    CreateShortCut "$DESKTOP\Claude Code for Windows.lnk" "$INSTDIR\ClaudeCodeWin.exe"
SectionEnd

;--------------------------------
; Uninstall section
Section "Uninstall"
    Call un.CheckAndKillProcess

    RMDir /r "$INSTDIR"

    SetShellVarContext all
    Delete "$DESKTOP\Claude Code for Windows.lnk"
    RMDir /r "$SMPROGRAMS\${PRODUCT_NAME}"

    DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"
    DeleteRegKey HKLM "${PRODUCT_DIR_REGKEY}"

    MessageBox MB_YESNO "Delete configuration files?" IDNO skip_config
        RMDir /r "$APPDATA\ClaudeCodeWin"
    skip_config:

    SetAutoClose true
SectionEnd

;--------------------------------
; Version info
VIProductVersion "${PRODUCT_VERSION}.0"
VIAddVersionKey "ProductName" "${PRODUCT_NAME}"
VIAddVersionKey "Comments" "Windows Claude Code Client - Lite Version (Downloads dependencies)"
VIAddVersionKey "CompanyName" "${PRODUCT_PUBLISHER}"
VIAddVersionKey "LegalCopyright" "Copyright (C) 2024"
VIAddVersionKey "FileDescription" "${PRODUCT_NAME} Setup (Lite)"
VIAddVersionKey "FileVersion" "${PRODUCT_VERSION}"
VIAddVersionKey "ProductVersion" "${PRODUCT_VERSION}"
