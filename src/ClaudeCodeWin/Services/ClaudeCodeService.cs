using System.Diagnostics;
using System.IO;
using System.Text;
using ClaudeCodeWin.Native;

namespace ClaudeCodeWin.Services
{
    /// <summary>
    /// Claude Code 进程管理服务 - 使用 Windows ConPTY
    /// </summary>
    public class ClaudeCodeService : IDisposable
    {
        private PseudoConsoleSession? _ptySession;
        private readonly EnvironmentService _envService;
        private bool _isRunning;
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
        /// 启动 Claude Code（使用 ConPTY）
        /// </summary>
        public async Task<bool> StartAsync(string workingDirectory, string? initialCommand = null)
        {
            if (_isRunning)
            {
                return false;
            }

            var isDebug = _envService.Config.GuiDebug == true;

            // 获取 Node.js 路径
            var nodePath = GetNodeExecutablePath();
            if (string.IsNullOrEmpty(nodePath))
            {
                OnError?.Invoke("Cannot find Node.js. Please reinstall the application.");
                return false;
            }

            // 获取 Claude CLI.js 路径
            var cliJsPath = FindClaudeCliJsPath();
            if (string.IsNullOrEmpty(cliJsPath))
            {
                OnError?.Invoke("Cannot find Claude Code. Please run: npm install -g @anthropic-ai/claude-code");
                return false;
            }

            _workingDirectory = workingDirectory;
            _claudePath = cliJsPath;

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

                // 确保 Git Bash 路径已设置（Claude Code 需要）
                EnsureGitBashPath(environment);

                // 设置终端相关环境变量
                environment["TERM"] = "xterm-256color";
                environment["FORCE_COLOR"] = "1";
                environment["COLORTERM"] = "truecolor";

                // 构建命令行：直接用 node.exe 运行 CLI.js
                var args = new List<string> { $"\"{nodePath}\"", $"\"{cliJsPath}\"" };
                if (_envService.Config.SkipPermissions == true)
                {
                    args.Add("--dangerously-skip-permissions");
                }
                var commandLine = string.Join(" ", args);

                if (isDebug)
                {
                    OnOutput?.Invoke($"[DEBUG] ═══════════════════════════════════════════════");
                    OnOutput?.Invoke($"[DEBUG] Claude Code 启动 (ConPTY 模式)");
                    OnOutput?.Invoke($"[DEBUG] Node.js: {nodePath}");
                    OnOutput?.Invoke($"[DEBUG] CLI.js: {cliJsPath}");
                    OnOutput?.Invoke($"[DEBUG] 命令行: {commandLine}");
                    OnOutput?.Invoke($"[DEBUG] 工作目录: {_workingDirectory}");
                    if (environment.TryGetValue("CLAUDE_CODE_GIT_BASH_PATH", out var gitBashPath))
                    {
                        OnOutput?.Invoke($"[DEBUG] Git Bash: {gitBashPath}");
                    }
                    else
                    {
                        OnOutput?.Invoke($"[DEBUG] Git Bash: 未设置!");
                    }
                    OnOutput?.Invoke($"[DEBUG] ═══════════════════════════════════════════════");
                }

                // 使用 ConPTY 启动进程
                _ptySession = ConPty.Create(
                    commandLine,
                    _workingDirectory,
                    environment,
                    cols: 120,
                    rows: 40);

                _isRunning = true;

                if (isDebug)
                {
                    OnOutput?.Invoke($"[DEBUG] 进程已启动，PID: {_ptySession.ProcessId}");
                }

                // 启动读取输出的任务
                _readCts = new CancellationTokenSource();
                _ = ReadOutputAsync(_readCts.Token);

                // 启动进程监控任务
                _ = MonitorProcessAsync(_readCts.Token);

                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to start Claude Code: {ex.Message}");
                if (isDebug)
                {
                    OnError?.Invoke($"[DEBUG] Exception: {ex.StackTrace}");
                }
                return false;
            }
        }

        /// <summary>
        /// 监控进程退出
        /// </summary>
        private async Task MonitorProcessAsync(CancellationToken cancellationToken)
        {
            if (_ptySession == null) return;

            var isDebug = _envService.Config.GuiDebug == true;

            try
            {
                while (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    if (_ptySession.HasExited)
                    {
                        _isRunning = false;
                        if (isDebug)
                        {
                            OnOutput?.Invoke($"[DEBUG] Claude Code 进程已退出，退出码: {_ptySession.ExitCode}");
                        }
                        OnProcessExited?.Invoke();
                        break;
                    }
                    await Task.Delay(500, cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (isDebug)
                {
                    OnError?.Invoke($"[DEBUG] 进程监控异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 异步读取终端输出
        /// </summary>
        private async Task ReadOutputAsync(CancellationToken cancellationToken)
        {
            if (_ptySession == null) return;

            var buffer = new byte[4096];
            var decoder = Encoding.UTF8.GetDecoder();
            var charBuffer = new char[4096];
            var isDebug = _envService.Config.GuiDebug == true;

            try
            {
                while (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    var bytesRead = await _ptySession.OutputStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
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
            if (!_isRunning || _ptySession == null)
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

                // 向终端发送输入（加上换行符）
                var bytes = Encoding.UTF8.GetBytes(input + "\n");
                await _ptySession.InputStream.WriteAsync(bytes, 0, bytes.Length);
                await _ptySession.InputStream.FlushAsync();
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
                _ptySession?.Resize((short)cols, (short)rows);
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

            // 先保存 PID，因为清理后 _ptySession 会变成 null
            int? pid = _ptySession?.ProcessId;

            if (_ptySession != null)
            {
                try
                {
                    _ptySession.Kill();
                }
                catch { }

                try
                {
                    _ptySession.Dispose();
                }
                catch { }

                _ptySession = null;
            }

            // 使用保存的 PID 进行额外清理
            if (pid.HasValue)
            {
                try
                {
                    KillProcessTree(pid.Value);
                }
                catch { }
            }

            // 额外清理可能残留的 node 进程
            try
            {
                KillOrphanedNodeProcesses();
            }
            catch { }
        }

        /// <summary>
        /// 杀掉进程树
        /// </summary>
        private void KillProcessTree(int pid)
        {
            try
            {
                var killProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/F /T /PID {pid}",
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
        }

        /// <summary>
        /// 清理可能残留的 node 进程（只清理我们启动的）
        /// </summary>
        private void KillOrphanedNodeProcesses()
        {
            try
            {
                // 查找包含 claude 的 node 进程
                var processes = Process.GetProcessesByName("node");
                foreach (var process in processes)
                {
                    try
                    {
                        // 检查命令行是否包含 claude
                        var commandLine = GetProcessCommandLine(process.Id);
                        if (commandLine != null &&
                            (commandLine.Contains("claude", StringComparison.OrdinalIgnoreCase) ||
                             commandLine.Contains("@anthropic-ai", StringComparison.OrdinalIgnoreCase)))
                        {
                            process.Kill();
                        }
                    }
                    catch { }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 获取进程命令行
        /// </summary>
        private string? GetProcessCommandLine(int processId)
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");
                foreach (var obj in searcher.Get())
                {
                    return obj["CommandLine"]?.ToString();
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 查找 Claude Code CLI.js 文件路径
        /// </summary>
        private string? FindClaudeCliJsPath()
        {
            var possiblePaths = new List<string>();

            // 用户 npm 全局目录（我们安装的位置）
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            possiblePaths.Add(Path.Combine(appData, "npm", "node_modules", "@anthropic-ai", "claude-code", "cli.js"));

            // 应用程序目录下的内置 Claude
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            possiblePaths.Add(Path.Combine(appDir, "nodejs", "node_modules", "@anthropic-ai", "claude-code", "cli.js"));

            // 安装目录下的内置 Claude
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            possiblePaths.Add(Path.Combine(programFiles, "ClaudeCodeWin", "nodejs", "node_modules", "@anthropic-ai", "claude-code", "cli.js"));

            return possiblePaths.FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// 查找 Claude Code 可执行文件路径（用于检测是否已安装）
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
        /// 确保 Git Bash 路径已设置
        /// </summary>
        private void EnsureGitBashPath(Dictionary<string, string> environment)
        {
            // 如果已经通过配置或环境变量设置了，直接使用
            if (environment.ContainsKey("CLAUDE_CODE_GIT_BASH_PATH"))
            {
                return;
            }

            // 自动检测 Git Bash 路径
            var gitBashPath = FindGitBashPath();
            if (!string.IsNullOrEmpty(gitBashPath))
            {
                environment["CLAUDE_CODE_GIT_BASH_PATH"] = gitBashPath;
            }
        }

        /// <summary>
        /// 查找 Git Bash 可执行文件路径
        /// </summary>
        public static string? FindGitBashPath()
        {
            var possiblePaths = new List<string>();

            // 优先使用内置的 MinGit（安装目录）
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            possiblePaths.Add(Path.Combine(appDir, "git", "bin", "bash.exe"));
            possiblePaths.Add(Path.Combine(appDir, "git", "usr", "bin", "bash.exe"));

            // 安装目录下的 Git
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            possiblePaths.Add(Path.Combine(programFiles, "ClaudeCodeWin", "git", "bin", "bash.exe"));
            possiblePaths.Add(Path.Combine(programFiles, "ClaudeCodeWin", "git", "usr", "bin", "bash.exe"));

            // 常见的 Git Bash 安装位置
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            // Git for Windows 默认安装路径
            possiblePaths.Add(Path.Combine(programFiles, "Git", "bin", "bash.exe"));
            possiblePaths.Add(Path.Combine(programFilesX86, "Git", "bin", "bash.exe"));

            // Git for Windows (usr/bin)
            possiblePaths.Add(Path.Combine(programFiles, "Git", "usr", "bin", "bash.exe"));
            possiblePaths.Add(Path.Combine(programFilesX86, "Git", "usr", "bin", "bash.exe"));

            // Portable Git
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            possiblePaths.Add(Path.Combine(localAppData, "Programs", "Git", "bin", "bash.exe"));
            possiblePaths.Add(Path.Combine(localAppData, "Programs", "Git", "usr", "bin", "bash.exe"));

            // Scoop 安装
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            possiblePaths.Add(Path.Combine(userProfile, "scoop", "apps", "git", "current", "bin", "bash.exe"));
            possiblePaths.Add(Path.Combine(userProfile, "scoop", "apps", "git", "current", "usr", "bin", "bash.exe"));

            // 检查 PATH 环境变量中是否能找到 git，如果能找到就用同目录的 bash
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var pathDir in pathEnv.Split(Path.PathSeparator))
                {
                    if (string.IsNullOrEmpty(pathDir)) continue;

                    var gitExe = Path.Combine(pathDir, "git.exe");
                    if (File.Exists(gitExe))
                    {
                        // 找到 git，尝试在同目录或相关目录找 bash
                        var bashInSameDir = Path.Combine(pathDir, "bash.exe");
                        if (File.Exists(bashInSameDir))
                        {
                            possiblePaths.Add(bashInSameDir);
                        }

                        // 尝试 ../bin/bash.exe
                        var parentDir = Path.GetDirectoryName(pathDir);
                        if (parentDir != null)
                        {
                            var bashInBin = Path.Combine(parentDir, "bin", "bash.exe");
                            if (File.Exists(bashInBin))
                            {
                                possiblePaths.Add(bashInBin);
                            }

                            var bashInUsrBin = Path.Combine(parentDir, "usr", "bin", "bash.exe");
                            if (File.Exists(bashInUsrBin))
                            {
                                possiblePaths.Add(bashInUsrBin);
                            }
                        }
                    }
                }
            }

            return possiblePaths.FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// 检查 Claude Code 是否已安装
        /// </summary>
        public static bool IsInstalled()
        {
            var service = new ClaudeCodeService(new EnvironmentService());
            return service.FindClaudeCliJsPath() != null;
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
        /// 获取用户 npm 全局目录（用于安装包，避免 Program Files 权限问题）
        /// </summary>
        private static string GetUserNpmGlobalPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "npm");
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
                    return (false, "Cannot find npm. Please ensure Node.js is installed.");
                }

                // 使用用户目录作为 npm 全局安装路径，避免 Program Files 权限问题
                var userNpmPath = GetUserNpmGlobalPath();
                if (!Directory.Exists(userNpmPath))
                {
                    Directory.CreateDirectory(userNpmPath);
                }

                onOutput?.Invoke($"Installing to user directory: {userNpmPath}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = npmPath,
                    Arguments = $"install -g @anthropic-ai/claude-code --prefix \"{userNpmPath}\" --registry=https://registry.npmmirror.com",
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

                // 设置 npm prefix 环境变量
                startInfo.EnvironmentVariables["npm_config_prefix"] = userNpmPath;

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
                            return (true, $"Claude Code installed successfully! Version: {version}");
                        }
                        return (true, "Claude Code installed successfully!");
                    }
                    else
                    {
                        return (false, $"Installation failed with exit code: {process.ExitCode}");
                    }
                }
                return (false, "Failed to start npm process");
            }
            catch (Exception ex)
            {
                return (false, $"Installation failed: {ex.Message}");
            }
        }

        private static async Task<string?> GetClaudeVersionAsync(string? nodeDir, Action<string>? onOutput = null)
        {
            try
            {
                var claudePath = FindClaudeCodePathStatic(nodeDir);
                if (string.IsNullOrEmpty(claudePath))
                {
                    onOutput?.Invoke("⚠ Claude command not found");
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
                onOutput?.Invoke($"⚠ Verification failed: {ex.Message}");
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
