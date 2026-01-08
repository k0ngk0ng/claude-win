using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ClaudeCodeWin.Services;
using ClaudeCodeWin.Views;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace ClaudeCodeWin
{
    public partial class MainWindow : Window
    {
        private readonly EnvironmentService _envService;
        private readonly ClaudeCodeService _claudeService;
        private bool _terminalReady = false;
        private int _terminalCols = 120;
        private int _terminalRows = 30;

        public MainWindow()
        {
            InitializeComponent();

            _envService = new EnvironmentService();
            _claudeService = new ClaudeCodeService(_envService);

            // 设置事件处理
            _claudeService.OnOutput += OnClaudeOutput;
            _claudeService.OnError += OnClaudeError;
            _claudeService.OnProcessExited += OnClaudeExited;

            // 设置默认工作目录
            WorkingDirectoryBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // 显示版本号
            VersionText.Text = GetVersionString();

            // 初始化 WebView2
            Loaded += async (s, e) => await InitializeWebView2Async();
        }

        private async Task InitializeWebView2Async()
        {
            try
            {
                // 设置用户数据文件夹
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ClaudeCodeWin", "WebView2");

                var env = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userDataFolder);

                await TerminalWebView.EnsureCoreWebView2Async(env);

                // 配置 WebView2
                TerminalWebView.CoreWebView2.Settings.IsScriptEnabled = true;
                TerminalWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                TerminalWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                TerminalWebView.CoreWebView2.Settings.AreDevToolsEnabled = _envService.Config.GuiDebug == true;

                // 处理来自 JavaScript 的消息
                TerminalWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                // 加载终端 HTML
                var html = LoadTerminalHtml();
                TerminalWebView.CoreWebView2.NavigateToString(html);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化终端失败: {ex.Message}\n\n请确保已安装 WebView2 Runtime。",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string LoadTerminalHtml()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "ClaudeCodeWin.Terminal.terminal.html";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new Exception($"找不到嵌入资源: {resourceName}");
            }

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = JsonConvert.DeserializeObject<TerminalMessage>(e.WebMessageAsJson);
                if (message == null) return;

                switch (message.Type)
                {
                    case "ready":
                        _terminalReady = true;
                        _terminalCols = message.Cols ?? 120;
                        _terminalRows = message.Rows ?? 30;
                        Dispatcher.Invoke(() => OnTerminalReady());
                        break;

                    case "input":
                        if (!string.IsNullOrEmpty(message.Data))
                        {
                            _ = _claudeService.SendInputRawAsync(message.Data);
                        }
                        break;

                    case "resize":
                        _terminalCols = message.Cols ?? _terminalCols;
                        _terminalRows = message.Rows ?? _terminalRows;
                        _claudeService.Resize(_terminalCols, _terminalRows);
                        break;

                    case "binary":
                        if (!string.IsNullOrEmpty(message.Data))
                        {
                            _ = _claudeService.SendInputRawAsync(message.Data);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理 WebView 消息失败: {ex.Message}");
            }
        }

        private async void OnTerminalReady()
        {
            // 显示欢迎消息
            await WriteToTerminalAsync("欢迎使用 Claude Code for Windows!\r\n");
            await WriteToTerminalAsync("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\r\n");

            // 检查安装状态
            await CheckInstallationAsync();
        }

        private async Task WriteToTerminalAsync(string text)
        {
            if (!_terminalReady || TerminalWebView.CoreWebView2 == null) return;

            // 使用 Base64 编码确保所有字节正确传输（包括 \r 等控制字符）
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
            await TerminalWebView.CoreWebView2.ExecuteScriptAsync($"window.terminalApi.writeBase64('{base64}')");
        }

        private async Task CheckInstallationAsync()
        {
            // 检查 Node.js
            var nodeVersion = ClaudeCodeService.GetNodeVersion();
            var npmVersion = ClaudeCodeService.GetNpmVersion();

            if (string.IsNullOrEmpty(nodeVersion))
            {
                await WriteToTerminalAsync("\x1b[31m✗ Node.js 未安装\x1b[0m\r\n");
                await WriteToTerminalAsync("\x1b[33m  请先安装 Node.js: https://nodejs.org/\x1b[0m\r\n\r\n");
            }
            else
            {
                await WriteToTerminalAsync($"\x1b[32m✓ Node.js {nodeVersion}\x1b[0m");
                if (!string.IsNullOrEmpty(npmVersion))
                {
                    await WriteToTerminalAsync($"\x1b[90m (npm {npmVersion})\x1b[0m");
                }
                await WriteToTerminalAsync("\r\n");
            }

            // 检查 Git Bash
            var gitBashPath = ClaudeCodeService.FindGitBashPath();
            if (string.IsNullOrEmpty(gitBashPath))
            {
                await WriteToTerminalAsync("\x1b[31m✗ Git Bash 未找到\x1b[0m\r\n");
                await WriteToTerminalAsync("\x1b[33m  Claude Code 需要 Git Bash，请检查安装\x1b[0m\r\n");
            }
            else
            {
                await WriteToTerminalAsync($"\x1b[32m✓ Git Bash: {gitBashPath}\x1b[0m\r\n");
            }

            // 检查 Claude Code
            if (!ClaudeCodeService.IsInstalled())
            {
                if (!string.IsNullOrEmpty(nodeVersion))
                {
                    await WriteToTerminalAsync("\x1b[33m✗ Claude Code 未安装，正在自动安装...\x1b[0m\r\n");
                    await WriteToTerminalAsync("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\r\n");

                    // 禁用启动按钮
                    StartButton.IsEnabled = false;

                    var (success, message) = await ClaudeCodeService.InstallClaudeCodeAsync(async output =>
                    {
                        await Dispatcher.InvokeAsync(async () =>
                        {
                            await WriteToTerminalAsync($"\x1b[90m{output}\x1b[0m\r\n");
                        });
                    });

                    await WriteToTerminalAsync("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\r\n");

                    if (success)
                    {
                        await WriteToTerminalAsync($"\x1b[32m✓ {message}\x1b[0m\r\n");
                    }
                    else
                    {
                        await WriteToTerminalAsync($"\x1b[31m✗ {message}\x1b[0m\r\n");
                        await WriteToTerminalAsync("\x1b[33m  请手动运行: npm install -g @anthropic-ai/claude-code\x1b[0m\r\n");
                    }

                    StartButton.IsEnabled = true;
                }
                else
                {
                    await WriteToTerminalAsync("\x1b[31m✗ Claude Code 未安装（需要先安装 Node.js）\x1b[0m\r\n");
                }
            }
            else
            {
                await WriteToTerminalAsync("\x1b[32m✓ Claude Code 已就绪\x1b[0m\r\n");
            }

            await WriteToTerminalAsync("\r\n");

            // 检查 API 密钥或认证令牌
            if (string.IsNullOrEmpty(_envService.Config.ApiKey) &&
                string.IsNullOrEmpty(_envService.Config.AuthToken))
            {
                await WriteToTerminalAsync("\x1b[33m⚠ 提示: 未配置认证信息，请点击 [⚙ 设置] 配置 API 密钥或认证令牌\x1b[0m\r\n\r\n");
            }

            await WriteToTerminalAsync("点击 \x1b[36m[▶ 启动]\x1b[0m 按钮开始使用 Claude Code\r\n");
        }

        private void BrowseDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "选择工作目录",
                InitialDirectory = WorkingDirectoryBox.Text
            };

            if (dialog.ShowDialog() == true)
            {
                WorkingDirectoryBox.Text = dialog.FolderName;
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            var workingDir = WorkingDirectoryBox.Text;
            if (string.IsNullOrEmpty(workingDir) || !Directory.Exists(workingDir))
            {
                MessageBox.Show("请选择有效的工作目录", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            WorkingDirectoryBox.IsEnabled = false;

            await WriteToTerminalAsync($"\r\n\x1b[36m启动 Claude Code...\x1b[0m\r\n");
            await WriteToTerminalAsync($"\x1b[90m工作目录: {workingDir}\x1b[0m\r\n");
            await WriteToTerminalAsync("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\r\n\r\n");

            // 清除欢迎消息，为 TUI 准备干净的屏幕
            if (TerminalWebView.CoreWebView2 != null)
            {
                await TerminalWebView.CoreWebView2.ExecuteScriptAsync("window.terminalApi.clear()");
            }

            var success = await _claudeService.StartAsync(workingDir, _terminalCols, _terminalRows);
            if (!success)
            {
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                WorkingDirectoryBox.IsEnabled = true;
            }
            else
            {
                // 聚焦到终端
                if (TerminalWebView.CoreWebView2 != null)
                {
                    await TerminalWebView.CoreWebView2.ExecuteScriptAsync("window.terminalApi.focus()");
                }
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _claudeService.Stop();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_envService);
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }

        private async void SlashCommand_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string command)
            {
                if (!_claudeService.IsRunning)
                {
                    await WriteToTerminalAsync("\x1b[33m⚠ 请先启动 Claude Code\x1b[0m\r\n");
                    return;
                }

                await _claudeService.SendInputAsync(command);
            }
        }

        private void OnClaudeOutput(string output)
        {
            Dispatcher.InvokeAsync(async () =>
            {
                await WriteToTerminalAsync(output);
            });
        }

        private void OnClaudeError(string error)
        {
            Dispatcher.InvokeAsync(async () =>
            {
                await WriteToTerminalAsync($"\x1b[31m{error}\x1b[0m");
            });
        }

        private void OnClaudeExited()
        {
            Dispatcher.InvokeAsync(async () =>
            {
                await WriteToTerminalAsync("\r\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\r\n");
                await WriteToTerminalAsync("\x1b[33mClaude Code 已退出\x1b[0m\r\n");

                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                WorkingDirectoryBox.IsEnabled = true;
            });
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 检查 Claude Code 是否正在运行
            if (_claudeService.IsRunning)
            {
                var result = MessageBox.Show(
                    "Claude Code 正在运行中，是否要关闭？\n\n关闭后当前会话将丢失。",
                    "确认关闭",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            // 确保杀掉所有相关进程
            _claudeService.Dispose();
            base.OnClosed(e);
        }

        /// <summary>
        /// 获取版本字符串
        /// </summary>
        private static string GetVersionString()
        {
            var assembly = Assembly.GetExecutingAssembly();

            var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrEmpty(infoVersion))
            {
                var plusIndex = infoVersion.IndexOf('+');
                if (plusIndex > 0)
                {
                    infoVersion = infoVersion.Substring(0, plusIndex);
                }
                return $"v{infoVersion}";
            }

            var version = assembly.GetName().Version;
            if (version != null)
            {
                return $"v{version.Major}.{version.Minor}.{version.Build}";
            }

            return "";
        }
    }

    /// <summary>
    /// 终端消息结构
    /// </summary>
    public class TerminalMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "";

        [JsonProperty("data")]
        public string? Data { get; set; }

        [JsonProperty("cols")]
        public int? Cols { get; set; }

        [JsonProperty("rows")]
        public int? Rows { get; set; }
    }
}
