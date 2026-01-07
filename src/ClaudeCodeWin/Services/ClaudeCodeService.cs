using System.Diagnostics;
using System.IO;
using System.Text;

namespace ClaudeCodeWin.Services
{
    /// <summary>
    /// Claude Code 进程管理服务
    /// </summary>
    public class ClaudeCodeService : IDisposable
    {
        private Process? _process;
        private readonly EnvironmentService _envService;
        private bool _isRunning;

        public event Action<string>? OnOutput;
        public event Action<string>? OnError;
        public event Action? OnProcessExited;

        public bool IsRunning => _isRunning;

        public ClaudeCodeService(EnvironmentService envService)
        {
            _envService = envService;
        }

        /// <summary>
        /// 启动 Claude Code（使用非交互模式）
        /// </summary>
        public async Task<bool> StartAsync(string workingDirectory, string? initialCommand = null)
        {
            if (_isRunning)
            {
                return false;
            }

            var claudePath = FindClaudeCodePath();
            if (string.IsNullOrEmpty(claudePath))
            {
                OnError?.Invoke("找不到 Claude Code。请确保已通过 npm install -g @anthropic-ai/claude-code 安装。");
                return false;
            }

            _workingDirectory = workingDirectory;
            _claudePath = claudePath;
            _isRunning = true;

            OnOutput?.Invoke("Claude Code 已就绪，请输入您的问题...\n");
            return true;
        }

        private string? _workingDirectory;
        private string? _claudePath;

        /// <summary>
        /// 发送输入到 Claude Code（每次发送都是一个独立的请求）
        /// </summary>
        public async Task SendInputAsync(string input)
        {
            if (!_isRunning || string.IsNullOrEmpty(_claudePath) || string.IsNullOrEmpty(_workingDirectory))
            {
                OnError?.Invoke("Claude Code 未启动");
                return;
            }

            try
            {
                // 使用 cmd.exe 来执行 claude.cmd
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    WorkingDirectory = _workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                // 应用环境变量
                _envService.ApplyToProcess(startInfo);

                // 确保 PATH 包含 Node.js
                EnsureNodeInPath(startInfo);

                // 构建命令行参数
                var claudeArgs = new StringBuilder();

                // 添加 --dangerously-skip-permissions 参数
                if (_envService.Config.SkipPermissions == true)
                {
                    claudeArgs.Append("--dangerously-skip-permissions ");
                }

                // 使用 -p 模式（--print 的简写）
                claudeArgs.Append("-p ");

                // 添加用户输入 - 对特殊字符进行转义
                var escapedInput = input.Replace("\"", "\\\"");
                claudeArgs.Append($"\"{escapedInput}\"");

                // 使用 /C 执行命令后退出
                startInfo.Arguments = $"/C \"\"{_claudePath}\" {claudeArgs}\"";

                OnOutput?.Invoke($"[调试] 执行: {_claudePath} {claudeArgs}");

                _process = new Process { StartInfo = startInfo };
                _process.EnableRaisingEvents = true;

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                _process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        OnOutput?.Invoke(e.Data);
                    }
                };

                _process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        OnError?.Invoke(e.Data);
                    }
                };

                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                // 等待进程完成，设置超时
                var completed = await Task.Run(() => _process.WaitForExit(300000)); // 5分钟超时

                if (!completed)
                {
                    OnError?.Invoke("执行超时（5分钟）");
                    try { _process.Kill(entireProcessTree: true); } catch { }
                }
                else
                {
                    OnOutput?.Invoke($"[调试] 进程退出码: {_process.ExitCode}");
                }

                OnOutput?.Invoke(""); // 添加空行分隔
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"执行失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 停止 Claude Code
        /// </summary>
        public void Stop()
        {
            if (_process != null && !_process.HasExited)
            {
                try
                {
                    _process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 进程可能已经退出
                }
            }
            _isRunning = false;
        }

        /// <summary>
        /// 查找 Claude Code 可执行文件路径（只使用内置路径）
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

            // npm 全局目录（使用内置 npm 安装的位置）
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            possiblePaths.Add(Path.Combine(appData, "npm", "claude.cmd"));
            possiblePaths.Add(Path.Combine(appData, "npm", "claude"));

            return possiblePaths.FirstOrDefault(File.Exists);
        }

        private string? GetConfiguredPath()
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClaudeCodeWin",
                "claude-path.txt"
            );
            if (File.Exists(configPath))
            {
                return File.ReadAllText(configPath).Trim();
            }
            return null;
        }

        /// <summary>
        /// 设置内置 Node.js 到 PATH（只使用内置路径）
        /// </summary>
        private void EnsureNodeInPath(ProcessStartInfo startInfo)
        {
            var nodePaths = new List<string>();

            // 应用程序目录下的内置 Node.js（优先）
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            nodePaths.Add(Path.Combine(appDir, "nodejs"));

            // 安装目录下的内置 Node.js
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            nodePaths.Add(Path.Combine(programFiles, "ClaudeCodeWin", "nodejs"));

            // 构建新的 PATH，内置 Node.js 优先
            var pathParts = new List<string>();
            foreach (var nodePath in nodePaths)
            {
                if (Directory.Exists(nodePath))
                {
                    pathParts.Add(nodePath);
                }
            }

            // 添加 npm 全局目录
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var npmPath = Path.Combine(appData, "npm");
            if (Directory.Exists(npmPath))
            {
                pathParts.Add(npmPath);
            }

            // 最后添加原有的 PATH（但排除其他 Node.js 路径）
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var part in currentPath.Split(Path.PathSeparator))
            {
                // 排除系统的 Node.js 路径，只使用我们的内置版本
                if (!string.IsNullOrEmpty(part) &&
                    !part.Contains("nodejs", StringComparison.OrdinalIgnoreCase) &&
                    !part.Contains("nvm", StringComparison.OrdinalIgnoreCase) &&
                    !part.Contains("node_modules", StringComparison.OrdinalIgnoreCase))
                {
                    pathParts.Add(part);
                }
            }

            startInfo.EnvironmentVariables["PATH"] = string.Join(Path.PathSeparator.ToString(), pathParts);
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
            catch
            {
                // Node.js 未安装
            }
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

                // 确保 Node.js 在 PATH 中
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
            catch
            {
                // npm 未安装
            }
            return null;
        }

        /// <summary>
        /// 获取 Node.js 可执行文件路径（只使用内置 Node.js）
        /// </summary>
        private static string? GetNodeExecutablePath()
        {
            var possiblePaths = new List<string>();

            // 应用程序目录下的内置 Node.js（优先）
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            possiblePaths.Add(Path.Combine(appDir, "nodejs", "node.exe"));

            // 安装目录下的内置 Node.js
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            possiblePaths.Add(Path.Combine(programFiles, "ClaudeCodeWin", "nodejs", "node.exe"));

            // 只使用内置 Node.js，不使用系统 Node.js
            return possiblePaths.FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// 获取 npm 可执行文件路径（只使用内置 npm）
        /// </summary>
        private static string? GetNpmExecutablePath()
        {
            var possiblePaths = new List<string>();

            // 应用程序目录下的内置 npm（优先）
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            possiblePaths.Add(Path.Combine(appDir, "nodejs", "npm.cmd"));

            // 安装目录下的内置 npm
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            possiblePaths.Add(Path.Combine(programFiles, "ClaudeCodeWin", "nodejs", "npm.cmd"));

            // 只使用内置 npm，不使用系统 npm
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

                // 确保 Node.js 在 PATH 中
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
                            if (line != null)
                            {
                                onOutput?.Invoke(line);
                            }
                        }
                    });

                    var errorTask = Task.Run(async () =>
                    {
                        while (!process.StandardError.EndOfStream)
                        {
                            var line = await process.StandardError.ReadLineAsync();
                            if (line != null)
                            {
                                onOutput?.Invoke(line);
                            }
                        }
                    });

                    await Task.WhenAll(outputTask, errorTask);
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        // 验证安装，获取版本号
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

        /// <summary>
        /// 获取 Claude Code 版本
        /// </summary>
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

        /// <summary>
        /// 静态方法查找 Claude Code 路径
        /// </summary>
        private static string? FindClaudeCodePathStatic(string? nodeDir)
        {
            var possiblePaths = new List<string>();

            // npm 全局目录（在 node 目录下）
            if (!string.IsNullOrEmpty(nodeDir))
            {
                possiblePaths.Add(Path.Combine(nodeDir, "claude.cmd"));
                possiblePaths.Add(Path.Combine(nodeDir, "claude"));
            }

            // npm 全局目录
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            possiblePaths.Add(Path.Combine(appData, "npm", "claude.cmd"));
            possiblePaths.Add(Path.Combine(appData, "npm", "claude"));

            // 应用程序目录
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            possiblePaths.Add(Path.Combine(appDir, "nodejs", "claude.cmd"));

            // 安装目录
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            possiblePaths.Add(Path.Combine(programFiles, "ClaudeCodeWin", "nodejs", "claude.cmd"));

            return possiblePaths.FirstOrDefault(File.Exists);
        }

        public void Dispose()
        {
            Stop();
            _process?.Dispose();
        }
    }
}
