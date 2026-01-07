@echo off
REM Claude Code for Windows 构建脚本 (批处理版)
REM 用于在没有 PowerShell 执行权限时使用

echo ========================================
echo  Claude Code for Windows 构建脚本
echo ========================================
echo.

REM 检查 .NET SDK
echo [1/4] 检查 .NET SDK...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo   错误: 未找到 .NET SDK
    echo   请安装 .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

for /f "tokens=*" %%i in ('dotnet --version') do echo   发现 .NET SDK: %%i

REM 还原依赖
echo [2/4] 还原 NuGet 依赖...
cd src\ClaudeCodeWin
dotnet restore
if errorlevel 1 (
    echo   还原依赖失败
    pause
    exit /b 1
)
echo   依赖还原完成

REM 构建项目
echo [3/4] 构建项目...
dotnet publish -c Release -r win-x64 --self-contained true -o ..\..\installer\publish
if errorlevel 1 (
    echo   构建失败
    pause
    exit /b 1
)
echo   构建完成

cd ..\..

REM 检查 NSIS
echo [4/4] 检查 NSIS 安装程序构建器...
where makensis >nul 2>&1
if errorlevel 1 (
    echo   未找到 NSIS，跳过安装程序构建
    echo   如需构建安装程序，请安装 NSIS: https://nsis.sourceforge.io/Download
) else (
    echo   构建安装程序...
    cd installer
    makensis installer.nsi
    cd ..
    if errorlevel 1 (
        echo   安装程序构建失败
    ) else (
        echo   安装程序构建完成
    )
)

echo.
echo ========================================
echo  构建完成!
echo ========================================
echo.
echo 输出目录: installer\publish
echo.
pause
