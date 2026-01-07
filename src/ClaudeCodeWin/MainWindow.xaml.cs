using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
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
            AppendToTerminal("点击 [▶ 启动] 按钮开始使用 Claude Code\n", Colors.White);
            AppendToTerminal("点击 [⚙ 设置] 配置环境变量和 API 密钥\n\n", Colors.White);

            // 检查安装状态
            CheckInstallation();
        }

        private void CheckInstallation()
        {
            if (!ClaudeCodeService.IsInstalled())
            {
                AppendToTerminal("⚠ 警告: 未检测到 Claude Code 安装\n", Colors.Orange);
                AppendToTerminal("请先安装: npm install -g @anthropic-ai/claude-code\n\n", Colors.Yellow);
            }
            else
            {
                AppendToTerminal("✓ Claude Code 已就绪\n\n", Colors.LightGreen);
            }

            if (string.IsNullOrEmpty(_envService.Config.ApiKey))
            {
                AppendToTerminal("⚠ 提示: 未配置 API 密钥，请在设置中配置\n\n", Colors.Yellow);
            }
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
            if (string.IsNullOrWhiteSpace(input))
                return;

            // 添加到历史记录
            _commandHistory.Add(input);
            _historyIndex = -1;

            // 显示输入
            AppendToTerminal($"> {input}\n", Colors.LightBlue);

            InputBox.Text = "";
            await _claudeService.SendInputAsync(input);
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
