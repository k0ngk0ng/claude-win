using System.Diagnostics;
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
        /// 启动 Claude Code
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

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = claudePath,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
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

                if (!string.IsNullOrEmpty(initialCommand))
                {
                    startInfo.Arguments = initialCommand;
                }

                _process = new Process { StartInfo = startInfo };
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
                _process.Exited += (s, e) =>
                {
                    _isRunning = false;
                    OnProcessExited?.Invoke();
                };
                _process.EnableRaisingEvents = true;

                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                _isRunning = true;
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"启动 Claude Code 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 发送输入到 Claude Code
        /// </summary>
        public async Task SendInputAsync(string input)
        {
            if (_process?.StandardInput != null && _isRunning)
            {
                await _process.StandardInput.WriteLineAsync(input);
                await _process.StandardInput.FlushAsync();
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
        /// 查找 Claude Code 可执行文件路径
        /// </summary>
        private string? FindClaudeCodePath()
        {
            // 优先使用配置的路径
            var customPath = GetConfiguredPath();
            if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
            {
                return customPath;
            }

            // Windows 上通常在 npm 全局目录
            var possiblePaths = new List<string>();

            // npm 全局目录
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            possiblePaths.Add(Path.Combine(appData, "npm", "claude.cmd"));
            possiblePaths.Add(Path.Combine(appData, "npm", "claude"));

            // 用户本地目录
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            possiblePaths.Add(Path.Combine(localAppData, "npm", "claude.cmd"));
            possiblePaths.Add(Path.Combine(localAppData, "npm", "claude"));

            // 程序安装目录（我们的安装器放置的位置）
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            possiblePaths.Add(Path.Combine(programFiles, "ClaudeCodeWin", "node_modules", ".bin", "claude.cmd"));
            possiblePaths.Add(Path.Combine(programFiles, "ClaudeCodeWin", "node_modules", ".bin", "claude"));

            // 在 PATH 中查找
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var pathDir in pathEnv.Split(Path.PathSeparator))
                {
                    possiblePaths.Add(Path.Combine(pathDir, "claude.cmd"));
                    possiblePaths.Add(Path.Combine(pathDir, "claude"));
                    possiblePaths.Add(Path.Combine(pathDir, "claude.exe"));
                }
            }

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

        private void EnsureNodeInPath(ProcessStartInfo startInfo)
        {
            var currentPath = startInfo.EnvironmentVariables["PATH"] ?? "";

            var nodePaths = new List<string>();

            // 常见的 Node.js 安装路径
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            nodePaths.Add(Path.Combine(programFiles, "nodejs"));
            nodePaths.Add(Path.Combine(programFilesX86, "nodejs"));
            nodePaths.Add(Path.Combine(appData, "npm"));
            nodePaths.Add(Path.Combine(appData, "nvm"));

            // 添加我们安装器内置的 Node.js
            nodePaths.Add(Path.Combine(programFiles, "ClaudeCodeWin", "nodejs"));

            foreach (var nodePath in nodePaths)
            {
                if (Directory.Exists(nodePath) && !currentPath.Contains(nodePath))
                {
                    currentPath = nodePath + Path.PathSeparator + currentPath;
                }
            }

            startInfo.EnvironmentVariables["PATH"] = currentPath;
        }

        /// <summary>
        /// 检查 Claude Code 是否已安装
        /// </summary>
        public static bool IsInstalled()
        {
            var service = new ClaudeCodeService(new EnvironmentService());
            return service.FindClaudeCodePath() != null;
        }

        public void Dispose()
        {
            Stop();
            _process?.Dispose();
        }
    }
}
