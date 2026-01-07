; Claude Code for Windows Installer
; NSIS Script
; Requires NSIS 3.x with Unicode support

;--------------------------------
; Unicode support (required for Chinese)
Unicode true

;--------------------------------
; Basic definitions
!define PRODUCT_NAME "Claude Code for Windows"
!define PRODUCT_VERSION "1.0.0"
!define PRODUCT_PUBLISHER "Claude Code Windows Team"
!define PRODUCT_WEB_SITE "https://github.com/anthropics/claude-code"
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\App Paths\ClaudeCodeWin.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
!define PRODUCT_UNINST_ROOT_KEY "HKLM"

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
!define MUI_FINISHPAGE_RUN_TEXT "$(RunAfterInstall)"
!insertmacro MUI_PAGE_FINISH

; Uninstall pages
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; Languages
!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "English"

; Language strings
LangString RunAfterInstall ${LANG_SIMPCHINESE} "启动 Claude Code for Windows"
LangString RunAfterInstall ${LANG_ENGLISH} "Launch Claude Code for Windows"
LangString UninstallLink ${LANG_SIMPCHINESE} "卸载"
LangString UninstallLink ${LANG_ENGLISH} "Uninstall"
LangString DeleteConfig ${LANG_SIMPCHINESE} "是否删除配置文件？"
LangString DeleteConfig ${LANG_ENGLISH} "Delete configuration files?"
LangString MainSection ${LANG_SIMPCHINESE} "主程序"
LangString MainSection ${LANG_ENGLISH} "Main Program"

;--------------------------------
; Installer properties
Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "ClaudeCodeWin-Setup-${PRODUCT_VERSION}.exe"
InstallDir "$PROGRAMFILES64\ClaudeCodeWin"
InstallDirRegKey HKLM "${PRODUCT_DIR_REGKEY}" ""
ShowInstDetails show
ShowUnInstDetails show
RequestExecutionLevel admin

;--------------------------------
; Install section

Section "$(MainSection)" SEC_MAIN
    SectionIn RO
    SetOutPath "$INSTDIR"

    ; Copy main program files
    File /r "publish\*.*"

    ; Copy bundled Node.js
    SetOutPath "$INSTDIR\nodejs"
    File /r "nodejs\*.*"

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
    CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\$(UninstallLink).lnk" "$INSTDIR\uninstall.exe"

    ; Create Desktop shortcut
    CreateShortCut "$DESKTOP\Claude Code for Windows.lnk" "$INSTDIR\ClaudeCodeWin.exe"
SectionEnd

;--------------------------------
; Uninstall section
Section "Uninstall"
    ; Delete program files
    RMDir /r "$INSTDIR"

    ; Delete shortcuts
    SetShellVarContext all
    Delete "$DESKTOP\Claude Code for Windows.lnk"
    RMDir /r "$SMPROGRAMS\${PRODUCT_NAME}"

    ; Delete registry keys
    DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"
    DeleteRegKey HKLM "${PRODUCT_DIR_REGKEY}"

    ; Ask to delete config
    MessageBox MB_YESNO "$(DeleteConfig)" IDNO skip_config
        RMDir /r "$APPDATA\ClaudeCodeWin"
    skip_config:

    SetAutoClose true
SectionEnd

;--------------------------------
; Version info
VIProductVersion "${PRODUCT_VERSION}.0"
VIAddVersionKey /LANG=${LANG_SIMPCHINESE} "ProductName" "${PRODUCT_NAME}"
VIAddVersionKey /LANG=${LANG_SIMPCHINESE} "Comments" "Windows Claude Code Client"
VIAddVersionKey /LANG=${LANG_SIMPCHINESE} "CompanyName" "${PRODUCT_PUBLISHER}"
VIAddVersionKey /LANG=${LANG_SIMPCHINESE} "LegalCopyright" "Copyright (C) 2024"
VIAddVersionKey /LANG=${LANG_SIMPCHINESE} "FileDescription" "${PRODUCT_NAME} Setup"
VIAddVersionKey /LANG=${LANG_SIMPCHINESE} "FileVersion" "${PRODUCT_VERSION}"
VIAddVersionKey /LANG=${LANG_SIMPCHINESE} "ProductVersion" "${PRODUCT_VERSION}"
VIAddVersionKey /LANG=${LANG_ENGLISH} "ProductName" "${PRODUCT_NAME}"
VIAddVersionKey /LANG=${LANG_ENGLISH} "Comments" "Windows Claude Code Client"
VIAddVersionKey /LANG=${LANG_ENGLISH} "CompanyName" "${PRODUCT_PUBLISHER}"
VIAddVersionKey /LANG=${LANG_ENGLISH} "LegalCopyright" "Copyright (C) 2024"
VIAddVersionKey /LANG=${LANG_ENGLISH} "FileDescription" "${PRODUCT_NAME} Setup"
VIAddVersionKey /LANG=${LANG_ENGLISH} "FileVersion" "${PRODUCT_VERSION}"
VIAddVersionKey /LANG=${LANG_ENGLISH} "ProductVersion" "${PRODUCT_VERSION}"
