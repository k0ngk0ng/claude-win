# Claude Code for Windows 构建脚本
# PowerShell 脚本

param(
    [switch]$Clean,
    [switch]$Release,
    [switch]$BuildInstaller,
    [switch]$DownloadNodejs,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# 配置
$ProjectRoot = $PSScriptRoot
$SrcPath = Join-Path $ProjectRoot "src\ClaudeCodeWin"
$PublishPath = Join-Path $ProjectRoot "installer\publish"
$InstallerPath = Join-Path $ProjectRoot "installer"
$NodejsVersion = "20.11.1"
$NodejsArch = "x64"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Claude Code for Windows 构建脚本" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 清理
if ($Clean) {
    Write-Host "[1/5] 清理之前的构建..." -ForegroundColor Yellow

    if (Test-Path $PublishPath) {
        Remove-Item -Path $PublishPath -Recurse -Force
    }

    $binPath = Join-Path $SrcPath "bin"
    $objPath = Join-Path $SrcPath "obj"

    if (Test-Path $binPath) {
        Remove-Item -Path $binPath -Recurse -Force
    }
    if (Test-Path $objPath) {
        Remove-Item -Path $objPath -Recurse -Force
    }

    Write-Host "  清理完成" -ForegroundColor Green
}

# 检查 .NET SDK
Write-Host "[2/5] 检查 .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    Write-Host "  发现 .NET SDK: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "  错误: 未找到 .NET SDK。请安装 .NET 8 SDK。" -ForegroundColor Red
    Write-Host "  下载地址: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    exit 1
}

# 还原依赖
Write-Host "[3/5] 还原 NuGet 依赖..." -ForegroundColor Yellow
Push-Location $SrcPath
try {
    dotnet restore
    if ($LASTEXITCODE -ne 0) {
        throw "还原依赖失败"
    }
    Write-Host "  依赖还原完成" -ForegroundColor Green
} finally {
    Pop-Location
}

# 构建项目
Write-Host "[4/5] 构建项目..." -ForegroundColor Yellow
Push-Location $SrcPath
try {
    # 发布为独立应用程序
    dotnet publish -c $Configuration -r win-x64 --self-contained true -o $PublishPath `
        -p:PublishSingleFile=false `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true

    if ($LASTEXITCODE -ne 0) {
        throw "构建失败"
    }
    Write-Host "  构建完成: $PublishPath" -ForegroundColor Green
} finally {
    Pop-Location
}

# 下载 Node.js (可选)
if ($DownloadNodejs) {
    Write-Host "[4.5/5] 下载 Node.js..." -ForegroundColor Yellow

    $nodejsDir = Join-Path $InstallerPath "nodejs"
    $nodejsZip = Join-Path $env:TEMP "nodejs.zip"
    $nodejsUrl = "https://nodejs.org/dist/v$NodejsVersion/node-v$NodejsVersion-win-$NodejsArch.zip"

    if (-not (Test-Path $nodejsDir)) {
        New-Item -ItemType Directory -Path $nodejsDir -Force | Out-Null
    }

    Write-Host "  下载: $nodejsUrl" -ForegroundColor Gray
    Invoke-WebRequest -Uri $nodejsUrl -OutFile $nodejsZip

    Write-Host "  解压中..." -ForegroundColor Gray
    Expand-Archive -Path $nodejsZip -DestinationPath $env:TEMP -Force

    # 移动文件
    $extractedDir = Join-Path $env:TEMP "node-v$NodejsVersion-win-$NodejsArch"
    Copy-Item -Path "$extractedDir\*" -Destination $nodejsDir -Recurse -Force

    # 清理
    Remove-Item -Path $nodejsZip -Force
    Remove-Item -Path $extractedDir -Recurse -Force

    Write-Host "  Node.js 已下载到: $nodejsDir" -ForegroundColor Green
}

# 构建安装程序
if ($BuildInstaller) {
    Write-Host "[5/5] 构建 NSIS 安装程序..." -ForegroundColor Yellow

    # 检查 NSIS
    $nsisPath = "C:\Program Files (x86)\NSIS\makensis.exe"
    if (-not (Test-Path $nsisPath)) {
        $nsisPath = "C:\Program Files\NSIS\makensis.exe"
    }

    if (-not (Test-Path $nsisPath)) {
        # 尝试从 PATH 中查找
        $nsisPath = Get-Command makensis -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
    }

    if (-not $nsisPath -or -not (Test-Path $nsisPath)) {
        Write-Host "  警告: 未找到 NSIS。请安装 NSIS 并添加到 PATH。" -ForegroundColor Yellow
        Write-Host "  下载地址: https://nsis.sourceforge.io/Download" -ForegroundColor Yellow
        Write-Host "  跳过安装程序构建" -ForegroundColor Yellow
    } else {
        Write-Host "  使用 NSIS: $nsisPath" -ForegroundColor Gray

        # 创建资源目录
        $resourcesDir = Join-Path $InstallerPath "resources"
        if (-not (Test-Path $resourcesDir)) {
            New-Item -ItemType Directory -Path $resourcesDir -Force | Out-Null
        }

        # 创建占位图标(如果不存在)
        $iconPath = Join-Path $resourcesDir "claude.ico"
        if (-not (Test-Path $iconPath)) {
            Write-Host "  警告: 未找到图标文件，请添加 resources/claude.ico" -ForegroundColor Yellow
        }

        Push-Location $InstallerPath
        try {
            & $nsisPath "installer.nsi"
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  安装程序构建完成" -ForegroundColor Green

                $installerFile = Get-ChildItem -Path $InstallerPath -Filter "*.exe" |
                    Where-Object { $_.Name -like "*Setup*" } |
                    Select-Object -First 1

                if ($installerFile) {
                    Write-Host "  输出: $($installerFile.FullName)" -ForegroundColor Cyan
                }
            } else {
                Write-Host "  安装程序构建失败" -ForegroundColor Red
            }
        } finally {
            Pop-Location
        }
    }
} else {
    Write-Host "[5/5] 跳过安装程序构建 (使用 -BuildInstaller 参数启用)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " 构建完成!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "输出目录: $PublishPath" -ForegroundColor White
Write-Host ""
Write-Host "后续步骤:" -ForegroundColor Yellow
Write-Host "  1. 运行 .\build.ps1 -BuildInstaller 构建安装程序" -ForegroundColor White
Write-Host "  2. 或直接运行 $PublishPath\ClaudeCodeWin.exe 测试" -ForegroundColor White
