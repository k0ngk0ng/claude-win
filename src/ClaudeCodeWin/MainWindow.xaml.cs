using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaudeCodeWin.Services;
using ClaudeCodeWin.Views;
using Microsoft.Win32;

namespace ClaudeCodeWin
{
    public partial class MainWindow : Window
    {
        private readonly EnvironmentService _envService;
        private readonly ClaudeCodeService _claudeService;
        private readonly List<string> _commandHistory = new();
        private int _historyIndex = -1;
        private readonly List<string> _pendingImagePaths = new();

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

            // 显示欢迎消息
            AppendToTerminal("欢迎使用 Claude Code for Windows!\n", Colors.LightGreen);
            AppendToTerminal("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n", Colors.Gray);

            // 异步检查安装状态
            Loaded += async (s, e) => await CheckInstallationAsync();
        }

        private async Task CheckInstallationAsync()
        {
            // 检查 Node.js
            var nodeVersion = ClaudeCodeService.GetNodeVersion();
            var npmVersion = ClaudeCodeService.GetNpmVersion();

            if (string.IsNullOrEmpty(nodeVersion))
            {
                AppendToTerminal("✗ Node.js 未安装\n", Colors.Red);
                AppendToTerminal("  请先安装 Node.js: https://nodejs.org/\n\n", Colors.Yellow);
            }
            else
            {
                AppendToTerminal($"✓ Node.js {nodeVersion}", Colors.LightGreen);
                if (!string.IsNullOrEmpty(npmVersion))
                {
                    AppendToTerminal($" (npm {npmVersion})", Colors.Gray);
                }
                AppendToTerminal("\n", Colors.White);
            }

            // 检查 Claude Code
            if (!ClaudeCodeService.IsInstalled())
            {
                if (!string.IsNullOrEmpty(nodeVersion))
                {
                    AppendToTerminal("✗ Claude Code 未安装，正在自动安装...\n", Colors.Yellow);
                    AppendToTerminal("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n", Colors.Gray);

                    // 禁用启动按钮
                    StartButton.IsEnabled = false;

                    var (success, message) = await ClaudeCodeService.InstallClaudeCodeAsync(output =>
                    {
                        Dispatcher.Invoke(() => AppendToTerminal(output + "\n", Colors.Gray));
                    });

                    AppendToTerminal("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n", Colors.Gray);

                    if (success)
                    {
                        AppendToTerminal("✓ " + message + "\n", Colors.LightGreen);
                    }
                    else
                    {
                        AppendToTerminal("✗ " + message + "\n", Colors.Red);
                        AppendToTerminal("  请手动运行: npm install -g @anthropic-ai/claude-code\n", Colors.Yellow);
                    }

                    StartButton.IsEnabled = true;
                }
                else
                {
                    AppendToTerminal("✗ Claude Code 未安装（需要先安装 Node.js）\n", Colors.Red);
                }
            }
            else
            {
                AppendToTerminal("✓ Claude Code 已就绪\n", Colors.LightGreen);
            }

            AppendToTerminal("\n", Colors.White);

            // 检查 API 密钥
            if (string.IsNullOrEmpty(_envService.Config.ApiKey))
            {
                AppendToTerminal("⚠ 提示: 未配置 API 密钥，请点击 [⚙ 设置] 配置\n\n", Colors.Yellow);
            }

            AppendToTerminal("点击 [▶ 启动] 按钮开始使用 Claude Code\n", Colors.White);
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

            AppendToTerminal($"\n启动 Claude Code...\n", Colors.Cyan);
            AppendToTerminal($"工作目录: {workingDir}\n", Colors.Gray);
            AppendToTerminal("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n", Colors.Gray);

            var success = await _claudeService.StartAsync(workingDir);
            if (!success)
            {
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                InputBox.IsEnabled = false;
                WorkingDirectoryBox.IsEnabled = true;
            }
            else
            {
                InputBox.Focus();
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
                    AppendToTerminal($"⚠ 请先启动 Claude Code\n", Colors.Yellow);
                    return;
                }

                AppendToTerminal($"> {command}\n", Colors.LightBlue);
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
                    Foreground = new SolidColorBrush(Colors.White),
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
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Colors.Gray),
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

        private void PasteImageFromClipboard()
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
                AppendToTerminal($"📋 已粘贴剪贴板图片\n", Colors.Gray);
            }
            catch (Exception ex)
            {
                AppendToTerminal($"⚠ 粘贴图片失败: {ex.Message}\n", Colors.Yellow);
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
                    // Claude Code 支持直接发送图片路径
                    messageToSend += $" {imagePath}";
                }

                // 显示输入（包含图片信息）
                AppendToTerminal($"> {input}", Colors.LightBlue);
                AppendToTerminal($" [📎 {_pendingImagePaths.Count} 张图片]\n", Colors.Gray);

                // 清除附件
                _pendingImagePaths.Clear();
                UpdateAttachmentPreview();
            }
            else
            {
                // 显示输入
                AppendToTerminal($"> {input}\n", Colors.LightBlue);
            }

            InputBox.Text = "";
            await _claudeService.SendInputAsync(messageToSend);
        }

        private void OnClaudeOutput(string output)
        {
            Dispatcher.Invoke(() =>
            {
                AppendToTerminal(output + "\n", Colors.White);
            });
        }

        private void OnClaudeError(string error)
        {
            Dispatcher.Invoke(() =>
            {
                AppendToTerminal(error + "\n", Colors.OrangeRed);
            });
        }

        private void OnClaudeExited()
        {
            Dispatcher.Invoke(() =>
            {
                AppendToTerminal("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n", Colors.Gray);
                AppendToTerminal("Claude Code 已退出\n", Colors.Yellow);

                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                InputBox.IsEnabled = false;
                WorkingDirectoryBox.IsEnabled = true;
            });
        }

        private void AppendToTerminal(string text, Color color)
        {
            var paragraph = TerminalOutput.Document.Blocks.LastBlock as Paragraph;
            if (paragraph == null)
            {
                paragraph = new Paragraph();
                TerminalOutput.Document.Blocks.Add(paragraph);
            }

            var run = new Run(text)
            {
                Foreground = new SolidColorBrush(color)
            };
            paragraph.Inlines.Add(run);

            // 自动滚动到底部
            TerminalScrollViewer.ScrollToEnd();
        }

        protected override void OnClosed(EventArgs e)
        {
            _claudeService.Dispose();
            base.OnClosed(e);
        }
    }
}
