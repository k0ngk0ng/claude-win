using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
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
        private readonly List<string> _commandHistory = new();
        private int _historyIndex = -1;
        private readonly List<string> _pendingImagePaths = new();
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

            // 转义字符串用于 JavaScript
            var escaped = JsonConvert.SerializeObject(text);
            await TerminalWebView.CoreWebView2.ExecuteScriptAsync($"window.terminalApi.write({escaped})");
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
            InputBox.IsEnabled = true;
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
                InputBox.IsEnabled = false;
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

                _commandHistory.Add(command);
                _historyIndex = -1;
                await _claudeService.SendInputAsync(command);
            }
        }

        private void AddImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择图片",
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|所有文件|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var filePath in dialog.FileNames)
                {
                    AddImageAttachment(filePath);
                }
            }
        }

        private void AddImageAttachment(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            _pendingImagePaths.Add(filePath);
            UpdateAttachmentPreview();
        }

        private void UpdateAttachmentPreview()
        {
            AttachmentList.Items.Clear();

            foreach (var imagePath in _pendingImagePaths)
            {
                var fileName = Path.GetFileName(imagePath);
                var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 4, 0) };

                // 缩略图
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imagePath);
                    bitmap.DecodePixelWidth = 32;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    var image = new Image { Source = bitmap, Width = 24, Height = 24, Margin = new Thickness(0, 0, 4, 0) };
                    panel.Children.Add(image);
                }
                catch
                {
                    panel.Children.Add(new TextBlock { Text = "🖼", VerticalAlignment = VerticalAlignment.Center });
                }

                panel.Children.Add(new TextBlock
                {
                    Text = fileName.Length > 20 ? fileName.Substring(0, 17) + "..." : fileName,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 11
                });

                // 删除按钮
                var removeBtn = new Button
                {
                    Content = "×",
                    FontSize = 10,
                    Padding = new Thickness(4, 0, 4, 0),
                    Margin = new Thickness(4, 0, 0, 0),
                    Background = System.Windows.Media.Brushes.Transparent,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Tag = imagePath
                };
                removeBtn.Click += RemoveAttachment_Click;
                panel.Children.Add(removeBtn);

                AttachmentList.Items.Add(panel);
            }

            AttachmentPreviewBorder.Visibility = _pendingImagePaths.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
            {
                _pendingImagePaths.Remove(path);
                UpdateAttachmentPreview();
            }
        }

        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 处理 Ctrl+V 粘贴图片
            if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (Clipboard.ContainsImage())
                {
                    e.Handled = true;
                    PasteImageFromClipboard();
                }
                else if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    foreach (string? file in files)
                    {
                        if (file != null && IsImageFile(file))
                        {
                            e.Handled = true;
                            AddImageAttachment(file);
                        }
                    }
                }
            }
        }

        private bool IsImageFile(string path)
        {
            var ext = Path.GetExtension(path).ToLower();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" ||
                   ext == ".gif" || ext == ".bmp" || ext == ".webp";
        }

        private async void PasteImageFromClipboard()
        {
            try
            {
                var image = Clipboard.GetImage();
                if (image == null) return;

                // 保存到临时文件
                var tempDir = Path.Combine(Path.GetTempPath(), "ClaudeCodeWin");
                if (!Directory.Exists(tempDir))
                    Directory.CreateDirectory(tempDir);

                var tempPath = Path.Combine(tempDir, $"clipboard_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                using (var fileStream = new FileStream(tempPath, FileMode.Create))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    encoder.Save(fileStream);
                }

                AddImageAttachment(tempPath);
                await WriteToTerminalAsync("\x1b[90m📋 已粘贴剪贴板图片\x1b[0m\r\n");
            }
            catch (Exception ex)
            {
                await WriteToTerminalAsync($"\x1b[33m⚠ 粘贴图片失败: {ex.Message}\x1b[0m\r\n");
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendInput();
        }

        private async void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                await SendInput();
            }
            else if (e.Key == Key.Up)
            {
                // 历史记录向上
                if (_commandHistory.Count > 0 && _historyIndex < _commandHistory.Count - 1)
                {
                    _historyIndex++;
                    InputBox.Text = _commandHistory[_commandHistory.Count - 1 - _historyIndex];
                    InputBox.CaretIndex = InputBox.Text.Length;
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                // 历史记录向下
                if (_historyIndex > 0)
                {
                    _historyIndex--;
                    InputBox.Text = _commandHistory[_commandHistory.Count - 1 - _historyIndex];
                    InputBox.CaretIndex = InputBox.Text.Length;
                }
                else if (_historyIndex == 0)
                {
                    _historyIndex = -1;
                    InputBox.Text = "";
                }
                e.Handled = true;
            }
        }

        private async Task SendInput()
        {
            var input = InputBox.Text;

            // 如果有附件但没有文本，也允许发送
            if (string.IsNullOrWhiteSpace(input) && _pendingImagePaths.Count == 0)
                return;

            // 添加到历史记录
            if (!string.IsNullOrWhiteSpace(input))
            {
                _commandHistory.Add(input);
                _historyIndex = -1;
            }

            // 构建发送内容
            var messageToSend = input ?? "";

            // 如果有图片附件，添加图片路径
            if (_pendingImagePaths.Count > 0)
            {
                foreach (var imagePath in _pendingImagePaths)
                {
                    messageToSend += $" {imagePath}";
                }

                // 清除附件
                _pendingImagePaths.Clear();
                UpdateAttachmentPreview();
            }

            InputBox.Text = "";
            await _claudeService.SendInputAsync(messageToSend);
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
                InputBox.IsEnabled = false;
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
