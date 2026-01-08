; Claude Code for Windows Installer - FULL VERSION
; Includes bundled Node.js and Git
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

;--------------------------------
; Compression
SetCompressor /SOLID lzma

;--------------------------------
; Modern UI
!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "LogicLib.nsh"

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
Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "ClaudeCodeWin-Full-${PRODUCT_VERSION}.exe"
InstallDir "$PROGRAMFILES64\ClaudeCodeWin"
InstallDirRegKey HKLM "${PRODUCT_DIR_REGKEY}" ""
ShowInstDetails show
ShowUnInstDetails show
RequestExecutionLevel admin

;--------------------------------
; Functions

; Check if application is running and kill it (silently)
Function CheckAndKillProcess
    nsExec::ExecToStack 'taskkill /F /IM ${PRODUCT_EXE}'
    Pop $0
    Pop $1
    Sleep 500
FunctionEnd

; Uninstall version
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

; Uninstaller init
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

    ; Kill process again just in case
    Call CheckAndKillProcess

    SetOutPath "$INSTDIR"

    ; Copy main program files
    File /r "publish\*.*"

    ; Copy bundled Node.js
    SetOutPath "$INSTDIR\nodejs"
    File /r "nodejs\*.*"

    ; Copy bundled Git (PortableGit with bash.exe)
    SetOutPath "$INSTDIR\git"
    File /r "git\*.*"

    ; Create data directory
    CreateDirectory "$APPDATA\ClaudeCodeWin"

    ; Write uninstall info
    WriteUninstaller "$INSTDIR\uninstall.exe"
    WriteRegStr HKLM "${PRODUCT_DIR_REGKEY}" "" "$INSTDIR\ClaudeCodeWin.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\uninstall.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\ClaudeCodeWin.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"

    ; Get install size
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
VIAddVersionKey "Comments" "Windows Claude Code Client - Full Version"
VIAddVersionKey "CompanyName" "${PRODUCT_PUBLISHER}"
VIAddVersionKey "LegalCopyright" "Copyright (C) 2024"
VIAddVersionKey "FileDescription" "${PRODUCT_NAME} Setup (Full)"
VIAddVersionKey "FileVersion" "${PRODUCT_VERSION}"
VIAddVersionKey "ProductVersion" "${PRODUCT_VERSION}"
