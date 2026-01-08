using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Pty.Net;

namespace ClaudeCodeWin.Services
{
    /// <summary>
    /// Claude Code 进程管理服务 - 使用伪终端 (ConPTY)
    /// </summary>
    public class ClaudeCodeService : IDisposable
    {
        private IPtyConnection? _pty;
        private readonly EnvironmentService _envService;
        private bool _isRunning;
        private int? _processId;
        private CancellationTokenSource? _readCts;
        private string? _workingDirectory;
        private string? _claudePath;

        public event Action<string>? OnOutput;
        public event Action<string>? OnError;
        public event Action? OnProcessExited;

        public bool IsRunning => _isRunning;

        public ClaudeCodeService(EnvironmentService envService)
        {
            _envService = envService;
        }

        /// <summary>
        /// 启动 Claude Code（使用伪终端）
        /// </summary>
        public async Task<bool> StartAsync(string workingDirectory, string? initialCommand = null)
        {
            if (_isRunning)
            {
                return false;
            }

            var isDebug = _envService.Config.GuiDebug == true;

            var claudePath = FindClaudeCodePath();
            if (string.IsNullOrEmpty(claudePath))
            {
                OnError?.Invoke("找不到 Claude Code。请确保已通过 npm install -g @anthropic-ai/claude-code 安装。");
                return false;
            }

            _workingDirectory = workingDirectory;
            _claudePath = claudePath;

            try
            {
                // 构建环境变量
                var environment = new Dictionary<string, string>();

                // 复制当前环境变量
                foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                {
                    environment[entry.Key?.ToString() ?? ""] = entry.Value?.ToString() ?? "";
                }

                // 应用用户配置的环境变量
                var configEnv = _envService.Config.ToDictionary();
                foreach (var kv in configEnv)
                {
                    environment[kv.Key] = kv.Value;
                }

                // 确保 PATH 包含 Node.js
                EnsureNodeInPath(environment);

                // 设置终端相关环境变量
                environment["TERM"] = "xterm-256color";
                environment["FORCE_COLOR"] = "1";
                environment["COLORTERM"] = "truecolor";

                // 构建命令行参数
                var args = new List<string>();
                if (_envService.Config.SkipPermissions == true)
                {
                    args.Add("--dangerously-skip-permissions");
                }

                if (isDebug)
                {
                    OnOutput?.Invoke($"[DEBUG] ═══════════════════════════════════════════════");
                    OnOutput?.Invoke($"[DEBUG] Claude Code 启动 (PTY 模式)");
                    OnOutput?.Invoke($"[DEBUG] Claude 路径: {_claudePath}");
                    OnOutput?.Invoke($"[DEBUG] 工作目录: {_workingDirectory}");
                    OnOutput?.Invoke($"[DEBUG] 参数: {string.Join(" ", args)}");
                    OnOutput?.Invoke($"[DEBUG] ═══════════════════════════════════════════════");
                }

                // 使用伪终端启动进程
                var options = new PtyOptions
                {
                    Name = "Claude Code",
                    App = _claudePath,
                    CommandLine = args.ToArray(),
                    Cwd = _workingDirectory,
                    Environment = environment,
                    Rows = 40,
                    Cols = 120
                };

                _pty = await PtyProvider.SpawnAsync(options, CancellationToken.None);
                _processId = _pty.Pid;
                _isRunning = true;

                if (isDebug)
                {
                    OnOutput?.Invoke($"[DEBUG] 进程已启动，PID: {_processId}");
                }

                // 监听进程退出
                _pty.ProcessExited += (sender, exitCode) =>
                {
                    _isRunning = false;
                    if (isDebug)
                    {
                        OnOutput?.Invoke($"[DEBUG] Claude Code 进程已退出，退出码: {exitCode}");
                    }
                    OnProcessExited?.Invoke();
                };

                // 启动读取输出的任务
                _readCts = new CancellationTokenSource();
                _ = ReadOutputAsync(_readCts.Token);

                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"启动 Claude Code 失败: {ex.Message}");
                if (isDebug)
                {
                    OnError?.Invoke($"[DEBUG] 异常: {ex.StackTrace}");
                }
                return false;
            }
        }

        /// <summary>
        /// 异步读取伪终端输出
        /// </summary>
        private async Task ReadOutputAsync(CancellationToken cancellationToken)
        {
            if (_pty == null) return;

            var buffer = new byte[4096];
            var decoder = Encoding.UTF8.GetDecoder();
            var charBuffer = new char[4096];

            try
            {
                while (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    var bytesRead = await _pty.ReaderStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead > 0)
                    {
                        var charCount = decoder.GetChars(buffer, 0, bytesRead, charBuffer, 0);
                        var text = new string(charBuffer, 0, charCount);

                        // 处理 ANSI 转义序列（简单过滤，保留文本）
                        text = ProcessAnsiOutput(text);

                        if (!string.IsNullOrEmpty(text))
                        {
                            OnOutput?.Invoke(text);
                        }
                    }
                    else
                    {
                        // 流结束
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                var isDebug = _envService.Config.GuiDebug == true;
                if (isDebug)
                {
                    OnError?.Invoke($"[DEBUG] 读取输出异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 处理 ANSI 转义序列
        /// </summary>
        private string ProcessAnsiOutput(string text)
        {
            // 移除常见的 ANSI 控制序列，保留文本
            // CSI 序列: ESC [ ...
            var result = new StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '\x1b' && i + 1 < text.Length)
                {
                    // ESC 序列开始
                    if (text[i + 1] == '[')
                    {
                        // CSI 序列，跳过直到遇到字母
                        i += 2;
                        while (i < text.Length && !char.IsLetter(text[i]))
                        {
                            i++;
                        }
                        if (i < text.Length)
                        {
                            i++; // 跳过终止字母
                        }
                    }
                    else if (text[i + 1] == ']')
                    {
                        // OSC 序列，跳过直到 BEL 或 ST
                        i += 2;
                        while (i < text.Length && text[i] != '\x07' && text[i] != '\x1b')
                        {
                            i++;
                        }
                        if (i < text.Length)
                        {
                            if (text[i] == '\x07')
                            {
                                i++; // 跳过 BEL
                            }
                            else if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '\\')
                            {
                                i += 2; // 跳过 ST
                            }
                        }
                    }
                    else
                    {
                        // 其他 ESC 序列，跳过两个字符
                        i += 2;
                    }
                }
                else if (text[i] == '\r')
                {
                    // 跳过 CR，只保留 LF
                    i++;
                }
                else
                {
                    result.Append(text[i]);
                    i++;
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// 发送输入到 Claude Code
        /// </summary>
        public async Task SendInputAsync(string input)
        {
            if (!_isRunning || _pty == null)
            {
                OnError?.Invoke("Claude Code 未运行");
                return;
            }

            var isDebug = _envService.Config.GuiDebug == true;

            try
            {
                if (isDebug)
                {
                    OnOutput?.Invoke($"[DEBUG] 发送输入: {input}");
                }

                // 向伪终端发送输入（加上换行符）
                var bytes = Encoding.UTF8.GetBytes(input + "\n");
                await _pty.WriterStream.WriteAsync(bytes, 0, bytes.Length);
                await _pty.WriterStream.FlushAsync();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"发送失败: {ex.Message}");
                if (isDebug)
                {
                    OnError?.Invoke($"[DEBUG] 异常类型: {ex.GetType().Name}");
                    OnError?.Invoke($"[DEBUG] 堆栈跟踪:\n{ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// 调整终端大小
        /// </summary>
        public void Resize(int cols, int rows)
        {
            try
            {
                _pty?.Resize(cols, rows);
            }
            catch { }
        }

        /// <summary>
        /// 停止 Claude Code
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _readCts?.Cancel();

            if (_pty != null)
            {
                try
                {
                    _pty.Kill();
                }
                catch { }

                try
                {
                    _pty.Dispose();
                }
                catch { }

                _pty = null;
            }

            // 额外清理
            try
            {
                KillRelatedProcesses();
            }
            catch { }
        }

        /// <summary>
        /// 杀掉可能残留的相关进程
        /// </summary>
        private void KillRelatedProcesses()
        {
            if (!_processId.HasValue)
                return;

            try
            {
                var killProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/F /T /PID {_processId.Value}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                killProcess.Start();
                killProcess.WaitForExit(3000);
            }
            catch { }

            _processId = null;
        }

        /// <summary>
        /// 查找 Claude Code 可执行文件路径
        /// </summary>
        private string? FindClaudeCodePath()
        {
            var possiblePaths = new List<string>();

            // 应用程序目录下的内置 Claude（优先）
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            possiblePaths.Add(Path.Combine(appDir, "nodejs", "claude.cmd"));
            possiblePaths.Add(Path.Combine(appDir, "nodejs", "claude"));

            // 安装目录下的内置 Claude
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            possiblePaths.Add(Path.Combine(programFiles, "ClaudeCodeWin", "nodejs", "claude.cmd"));
            possiblePaths.Add(Path.Combine(programFiles, "ClaudeCodeWin", "nodejs", "claude"));

            // npm 全局目录
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            possiblePaths.Add(Path.Combine(appData, "npm", "claude.cmd"));
            possiblePaths.Add(Path.Combine(appData, "npm", "claude"));

            return possiblePaths.FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// 确保 PATH 包含 Node.js
        /// </summary>
        private void EnsureNodeInPath(Dictionary<string, string> environment)
        {
            var nodePaths = new List<string>();

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            nodePaths.Add(Path.Combine(appDir, "nodejs"));

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            nodePaths.Add(Path.Combine(programFiles, "ClaudeCodeWin", "nodejs"));

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            nodePaths.Add(Path.Combine(appData, "npm"));

            var pathParts = new List<string>();
            foreach (var nodePath in nodePaths)
            {
                if (Directory.Exists(nodePath))
                {
                    pathParts.Add(nodePath);
                }
            }

            // 添加原有 PATH（排除其他 Node.js）
            if (environment.TryGetValue("PATH", out var currentPath) ||
                environment.TryGetValue("Path", out currentPath))
            {
                foreach (var part in currentPath.Split(Path.PathSeparator))
                {
                    if (!string.IsNullOrEmpty(part) &&
                        !part.Contains("nodejs", StringComparison.OrdinalIgnoreCase) &&
                        !part.Contains("nvm", StringComparison.OrdinalIgnoreCase) &&
                        !part.Contains("node_modules", StringComparison.OrdinalIgnoreCase))
                    {
                        pathParts.Add(part);
                    }
                }
            }

            environment["PATH"] = string.Join(Path.PathSeparator.ToString(), pathParts);
        }

        /// <summary>
        /// 检查 Claude Code 是否已安装
        /// </summary>
        public static bool IsInstalled()
        {
            var service = new ClaudeCodeService(new EnvironmentService());
            return service.FindClaudeCodePath() != null;
        }

        /// <summary>
        /// 获取 Node.js 版本
        /// </summary>
        public static string? GetNodeVersion()
        {
            try
            {
                var nodePath = GetNodeExecutablePath();
                var startInfo = new ProcessStartInfo
                {
                    FileName = nodePath ?? "node",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(5000);
                    return output;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 获取 npm 版本
        /// </summary>
        public static string? GetNpmVersion()
        {
            try
            {
                var npmPath = GetNpmExecutablePath();
                var startInfo = new ProcessStartInfo
                {
                    FileName = npmPath ?? "npm",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                var nodeDir = Path.GetDirectoryName(GetNodeExecutablePath());
                if (!string.IsNullOrEmpty(nodeDir))
                {
                    startInfo.EnvironmentVariables["PATH"] = nodeDir + Path.PathSeparator +
                        Environment.GetEnvironmentVariable("PATH");
                }

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(5000);
                    return output;
                }
            }
            catch { }
            return null;
        }

        private static string? GetNodeExecutablePath()
        {
            var possiblePaths = new List<string>();
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            possiblePaths.Add(Path.Combine(appDir, "nodejs", "node.exe"));

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            possiblePaths.Add(Path.Combine(programFiles, "ClaudeCodeWin", "nodejs", "node.exe"));

            return possiblePaths.FirstOrDefault(File.Exists);
        }

        private static string? GetNpmExecutablePath()
        {
            var possiblePaths = new List<string>();
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            possiblePaths.Add(Path.Combine(appDir, "nodejs", "npm.cmd"));

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            possiblePaths.Add(Path.Combine(programFiles, "ClaudeCodeWin", "nodejs", "npm.cmd"));

            return possiblePaths.FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// 安装 Claude Code CLI
        /// </summary>
        public static async Task<(bool success, string message)> InstallClaudeCodeAsync(Action<string>? onOutput = null)
        {
            try
            {
                var npmPath = GetNpmExecutablePath();
                if (string.IsNullOrEmpty(npmPath))
                {
                    return (false, "找不到 npm，请确保已安装 Node.js");
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = npmPath,
                    Arguments = "install -g @anthropic-ai/claude-code --registry=https://registry.npmmirror.com",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                var nodeDir = Path.GetDirectoryName(GetNodeExecutablePath());
                if (!string.IsNullOrEmpty(nodeDir))
                {
                    startInfo.EnvironmentVariables["PATH"] = nodeDir + Path.PathSeparator +
                        Environment.GetEnvironmentVariable("PATH");
                }

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var outputTask = Task.Run(async () =>
                    {
                        while (!process.StandardOutput.EndOfStream)
                        {
                            var line = await process.StandardOutput.ReadLineAsync();
                            if (line != null) onOutput?.Invoke(line);
                        }
                    });

                    var errorTask = Task.Run(async () =>
                    {
                        while (!process.StandardError.EndOfStream)
                        {
                            var line = await process.StandardError.ReadLineAsync();
                            if (line != null) onOutput?.Invoke(line);
                        }
                    });

                    await Task.WhenAll(outputTask, errorTask);
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        var version = await GetClaudeVersionAsync(nodeDir, onOutput);
                        if (!string.IsNullOrEmpty(version))
                        {
                            return (true, $"Claude Code 安装成功！版本: {version}");
                        }
                        return (true, "Claude Code 安装成功！");
                    }
                    else
                    {
                        return (false, $"安装失败，退出码: {process.ExitCode}");
                    }
                }
                return (false, "无法启动 npm 进程");
            }
            catch (Exception ex)
            {
                return (false, $"安装失败: {ex.Message}");
            }
        }

        private static async Task<string?> GetClaudeVersionAsync(string? nodeDir, Action<string>? onOutput = null)
        {
            try
            {
                var claudePath = FindClaudeCodePathStatic(nodeDir);
                if (string.IsNullOrEmpty(claudePath))
                {
                    onOutput?.Invoke("⚠ 未找到 claude 命令");
                    return null;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = claudePath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                if (!string.IsNullOrEmpty(nodeDir))
                {
                    startInfo.EnvironmentVariables["PATH"] = nodeDir + Path.PathSeparator +
                        Environment.GetEnvironmentVariable("PATH");
                }

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    var version = output.Trim();
                    if (!string.IsNullOrEmpty(version))
                    {
                        onOutput?.Invoke($"✓ claude --version: {version}");
                        return version;
                    }
                }
            }
            catch (Exception ex)
            {
                onOutput?.Invoke($"⚠ 验证失败: {ex.Message}");
            }
            return null;
        }

        private static string? FindClaudeCodePathStatic(string? nodeDir)
        {
            var possiblePaths = new List<string>();

            if (!string.IsNullOrEmpty(nodeDir))
            {
                possiblePaths.Add(Path.Combine(nodeDir, "claude.cmd"));
                possiblePaths.Add(Path.Combine(nodeDir, "claude"));
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            possiblePaths.Add(Path.Combine(appData, "npm", "claude.cmd"));
            possiblePaths.Add(Path.Combine(appData, "npm", "claude"));

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            possiblePaths.Add(Path.Combine(appDir, "nodejs", "claude.cmd"));

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            possiblePaths.Add(Path.Combine(programFiles, "ClaudeCodeWin", "nodejs", "claude.cmd"));

            return possiblePaths.FirstOrDefault(File.Exists);
        }

        public void Dispose()
        {
            Stop();
            _readCts?.Dispose();
        }
    }
}
