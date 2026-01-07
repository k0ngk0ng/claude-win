# Claude Code for Windows

Windows 原生的 Claude Code 客户端，提供图形化界面和简化的安装体验。

## 功能特性

- **Windows 原生界面**: 使用 WPF 开发，提供原生 Windows 体验
- **环境变量管理**: 图形化配置所有 ANTHROPIC 开头的环境变量
- **一键安装**: NSIS 安装包，包含所有必需组件
- **终端模拟**: 内置终端界面，直接与 Claude Code 交互
- **命令历史**: 支持上下键浏览历史命令

## 支持的环境变量

| 变量名 | 说明 |
|--------|------|
| `ANTHROPIC_API_KEY` | Anthropic API 密钥 (必需) |
| `ANTHROPIC_BASE_URL` | 自定义 API 端点 URL |
| `ANTHROPIC_MODEL` | 使用的模型名称 |
| `ANTHROPIC_SMALL_FAST_MODEL` | 快速任务使用的小模型 |
| `ANTHROPIC_AUTH_TOKEN` | OAuth 认证令牌 |
| `ANTHROPIC_PROXY` | HTTP 代理地址 |
| `ANTHROPIC_TIMEOUT` | API 请求超时时间（秒） |
| `ANTHROPIC_MAX_RETRIES` | 最大重试次数 |
| `ANTHROPIC_DISABLE_CACHE` | 禁用缓存 |
| `ANTHROPIC_DISABLE_TELEMETRY` | 禁用遥测数据收集 |
| `ANTHROPIC_DISABLE_UPDATE_CHECK` | 禁用自动更新检查 |
| `ANTHROPIC_DEBUG` | 启用调试模式 |
| `ANTHROPIC_LOG_LEVEL` | 日志级别 (debug/info/warn/error) |

## 系统要求

- Windows 10/11 (64位)
- .NET 8.0 Runtime (安装包会自动包含)
- Node.js 18+ (可选，安装包可自动安装)

## 安装方式

### 方式一：使用安装程序（推荐）

1. 下载 `ClaudeCodeWin-Setup-x.x.x.exe`
2. 运行安装程序
3. 按提示完成安装
4. 从开始菜单或桌面启动

### 方式二：从源码构建

```powershell
# 克隆仓库
git clone https://github.com/your-repo/claude-win.git
cd claude-win

# 构建（需要 .NET 8 SDK）
.\build.ps1

# 构建并创建安装程序（需要 NSIS）
.\build.ps1 -BuildInstaller
```

## 开发环境设置

### 必需工具

1. **Visual Studio 2022** 或 **VS Code** + C# 扩展
2. **.NET 8 SDK**: https://dotnet.microsoft.com/download/dotnet/8.0
3. **NSIS** (仅构建安装程序需要): https://nsis.sourceforge.io/Download

### 项目结构

```
claude-win/
├── src/
│   └── ClaudeCodeWin/          # WPF 主项目
│       ├── Models/             # 数据模型
│       ├── Services/           # 服务层
│       ├── Views/              # 视图（窗口）
│       ├── MainWindow.xaml     # 主窗口
│       └── App.xaml            # 应用入口
├── installer/
│   ├── installer.nsi           # NSIS 安装脚本
│   └── resources/              # 安装程序资源
├── build.ps1                   # PowerShell 构建脚本
└── build.bat                   # 批处理构建脚本
```

## 使用说明

1. 启动 Claude Code for Windows
2. 点击 **⚙ 设置** 配置您的 API 密钥
3. 选择工作目录
4. 点击 **▶ 启动** 开始使用
5. 在底部输入框输入命令并按 Enter 发送

## 快捷键

| 快捷键 | 功能 |
|--------|------|
| `Enter` | 发送命令 |
| `↑` / `↓` | 浏览命令历史 |
| `Shift+Enter` | 输入多行 |

## 故障排除

### 找不到 Claude Code

确保已通过 npm 安装 Claude Code：

```bash
npm install -g @anthropic-ai/claude-code
```

### API 连接失败

1. 检查 API 密钥是否正确
2. 如果使用代理，确保代理设置正确
3. 检查网络连接

### 中文显示问题

确保系统已安装中文字体，程序使用 Consolas 和系统默认字体。

## CI/CD

项目使用 GitHub Actions 自动构建：

[![Build and Release](https://github.com/k0ngk0ng/claude-win/actions/workflows/build.yml/badge.svg)](https://github.com/k0ngk0ng/claude-win/actions/workflows/build.yml)
[![CI](https://github.com/k0ngk0ng/claude-win/actions/workflows/ci.yml/badge.svg)](https://github.com/k0ngk0ng/claude-win/actions/workflows/ci.yml)

### 自动构建

- **CI**: 每次 push 和 PR 自动运行构建检查
- **Release**: 推送 `v*` 标签时自动创建 Release

### 手动发布

```bash
# 创建版本标签
git tag v1.0.0
git push origin v1.0.0
```

## 许可证

MIT License

## 贡献

欢迎提交 Issue 和 Pull Request！
