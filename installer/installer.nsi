; Claude Code for Windows 安装程序
; NSIS 脚本
; 使用 NSIS 3.x 编译

;--------------------------------
; 基本定义
!define PRODUCT_NAME "Claude Code for Windows"
!define PRODUCT_VERSION "1.0.0"
!define PRODUCT_PUBLISHER "Claude Code Windows Team"
!define PRODUCT_WEB_SITE "https://github.com/anthropics/claude-code"
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\App Paths\ClaudeCodeWin.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
!define PRODUCT_UNINST_ROOT_KEY "HKLM"

; Node.js 版本
!define NODE_VERSION "20.11.1"
!define NODE_ARCH "x64"

;--------------------------------
; 压缩设置
SetCompressor /SOLID lzma
SetCompressorDictSize 64

;--------------------------------
; 现代界面
!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "LogicLib.nsh"

;--------------------------------
; MUI 设置
!define MUI_ABORTWARNING
!define MUI_ICON "resources\claude.ico"
!define MUI_UNICON "resources\claude.ico"
!define MUI_WELCOMEFINISHPAGE_BITMAP "resources\welcome.bmp"

; 欢迎页面
!insertmacro MUI_PAGE_WELCOME

; 许可协议页面
!insertmacro MUI_PAGE_LICENSE "LICENSE.txt"

; 目录选择页面
!insertmacro MUI_PAGE_DIRECTORY

; 组件选择页面
!insertmacro MUI_PAGE_COMPONENTS

; 安装页面
!insertmacro MUI_PAGE_INSTFILES

; 完成页面
!define MUI_FINISHPAGE_RUN "$INSTDIR\ClaudeCodeWin.exe"
!define MUI_FINISHPAGE_RUN_TEXT "启动 Claude Code for Windows"
!insertmacro MUI_PAGE_FINISH

; 卸载页面
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; 语言
!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "English"

;--------------------------------
; 安装程序属性
Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "ClaudeCodeWin-Setup-${PRODUCT_VERSION}.exe"
InstallDir "$PROGRAMFILES64\ClaudeCodeWin"
InstallDirRegKey HKLM "${PRODUCT_DIR_REGKEY}" ""
ShowInstDetails show
ShowUnInstDetails show
RequestExecutionLevel admin

;--------------------------------
; 安装类型
InstType "完整安装 (推荐)"
InstType "最小安装"
InstType "自定义"

;--------------------------------
; 安装段

Section "主程序" SEC_MAIN
    SectionIn 1 2 3 RO
    SetOutPath "$INSTDIR"

    ; 复制主程序文件
    File /r "publish\*.*"

    ; 创建数据目录
    CreateDirectory "$APPDATA\ClaudeCodeWin"

    ; 写入卸载信息
    WriteUninstaller "$INSTDIR\uninstall.exe"
    WriteRegStr HKLM "${PRODUCT_DIR_REGKEY}" "" "$INSTDIR\ClaudeCodeWin.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\uninstall.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\ClaudeCodeWin.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"

    ; 获取安装大小
    ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
    IntFmt $0 "0x%08X" $0
    WriteRegDWORD ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "EstimatedSize" "$0"
SectionEnd

Section "Node.js 运行时" SEC_NODEJS
    SectionIn 1 3
    SetOutPath "$INSTDIR\nodejs"

    ; 检查是否已安装 Node.js
    nsExec::ExecToStack 'cmd /c node --version'
    Pop $0
    Pop $1

    ${If} $0 != 0
        ; 下载并安装 Node.js
        DetailPrint "正在下载 Node.js ${NODE_VERSION}..."

        ; 如果 Node.js 不存在，从安装包中解压或下载
        File /nonfatal /r "nodejs\*.*"

        ${If} ${FileExists} "$INSTDIR\nodejs\node.exe"
            DetailPrint "Node.js 已安装到 $INSTDIR\nodejs"
        ${Else}
            ; 下载 Node.js
            inetc::get /NOCANCEL \
                "https://nodejs.org/dist/v${NODE_VERSION}/node-v${NODE_VERSION}-win-${NODE_ARCH}.zip" \
                "$TEMP\nodejs.zip" /END
            Pop $0

            ${If} $0 == "OK"
                ; 解压 Node.js
                DetailPrint "正在解压 Node.js..."
                nsisunz::UnzipToLog "$TEMP\nodejs.zip" "$INSTDIR"
                Rename "$INSTDIR\node-v${NODE_VERSION}-win-${NODE_ARCH}" "$INSTDIR\nodejs"
                Delete "$TEMP\nodejs.zip"
            ${Else}
                MessageBox MB_ICONEXCLAMATION "下载 Node.js 失败！请手动安装 Node.js 或检查网络连接。"
            ${EndIf}
        ${EndIf}

        ; 添加到 PATH
        EnVar::SetHKLM
        EnVar::AddValue "PATH" "$INSTDIR\nodejs"
    ${Else}
        DetailPrint "检测到已安装 Node.js: $1"
    ${EndIf}
SectionEnd

Section "Claude Code CLI" SEC_CLAUDE_CODE
    SectionIn 1 3
    SetOutPath "$INSTDIR"

    ; 检查 npm 是否可用
    DetailPrint "正在安装 Claude Code CLI..."

    ; 设置 npm 路径
    ${If} ${FileExists} "$INSTDIR\nodejs\npm.cmd"
        nsExec::ExecToLog '"$INSTDIR\nodejs\npm.cmd" install -g @anthropic-ai/claude-code'
    ${Else}
        nsExec::ExecToLog 'npm install -g @anthropic-ai/claude-code'
    ${EndIf}

    Pop $0
    ${If} $0 != 0
        MessageBox MB_ICONEXCLAMATION "安装 Claude Code CLI 失败！\n请稍后手动运行：npm install -g @anthropic-ai/claude-code"
    ${Else}
        DetailPrint "Claude Code CLI 安装成功"
    ${EndIf}
SectionEnd

Section "开始菜单快捷方式" SEC_STARTMENU
    SectionIn 1 2 3
    SetShellVarContext all
    CreateDirectory "$SMPROGRAMS\${PRODUCT_NAME}"
    CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\Claude Code for Windows.lnk" "$INSTDIR\ClaudeCodeWin.exe"
    CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\卸载.lnk" "$INSTDIR\uninstall.exe"
SectionEnd

Section "桌面快捷方式" SEC_DESKTOP
    SectionIn 1 3
    SetShellVarContext all
    CreateShortCut "$DESKTOP\Claude Code for Windows.lnk" "$INSTDIR\ClaudeCodeWin.exe"
SectionEnd

;--------------------------------
; 组件描述
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
    !insertmacro MUI_DESCRIPTION_TEXT ${SEC_MAIN} "Claude Code for Windows 主程序（必需）"
    !insertmacro MUI_DESCRIPTION_TEXT ${SEC_NODEJS} "Node.js 运行时环境（如果系统未安装则需要）"
    !insertmacro MUI_DESCRIPTION_TEXT ${SEC_CLAUDE_CODE} "Claude Code 命令行工具"
    !insertmacro MUI_DESCRIPTION_TEXT ${SEC_STARTMENU} "在开始菜单创建快捷方式"
    !insertmacro MUI_DESCRIPTION_TEXT ${SEC_DESKTOP} "在桌面创建快捷方式"
!insertmacro MUI_FUNCTION_DESCRIPTION_END

;--------------------------------
; 卸载段
Section "Uninstall"
    ; 删除程序文件
    RMDir /r "$INSTDIR"

    ; 删除快捷方式
    SetShellVarContext all
    Delete "$DESKTOP\Claude Code for Windows.lnk"
    RMDir /r "$SMPROGRAMS\${PRODUCT_NAME}"

    ; 删除注册表项
    DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"
    DeleteRegKey HKLM "${PRODUCT_DIR_REGKEY}"

    ; 询问是否删除配置
    MessageBox MB_YESNO "是否删除配置文件？" IDNO skip_config
        RMDir /r "$APPDATA\ClaudeCodeWin"
    skip_config:

    SetAutoClose true
SectionEnd

;--------------------------------
; 版本信息
VIProductVersion "${PRODUCT_VERSION}.0"
VIAddVersionKey /LANG=${LANG_SIMPCHINESE} "ProductName" "${PRODUCT_NAME}"
VIAddVersionKey /LANG=${LANG_SIMPCHINESE} "Comments" "Windows 原生 Claude Code 客户端"
VIAddVersionKey /LANG=${LANG_SIMPCHINESE} "CompanyName" "${PRODUCT_PUBLISHER}"
VIAddVersionKey /LANG=${LANG_SIMPCHINESE} "LegalCopyright" "Copyright (C) 2024"
VIAddVersionKey /LANG=${LANG_SIMPCHINESE} "FileDescription" "${PRODUCT_NAME} 安装程序"
VIAddVersionKey /LANG=${LANG_SIMPCHINESE} "FileVersion" "${PRODUCT_VERSION}"
VIAddVersionKey /LANG=${LANG_SIMPCHINESE} "ProductVersion" "${PRODUCT_VERSION}"
