using System.IO;
using Newtonsoft.Json;
using ClaudeCodeWin.Models;

namespace ClaudeCodeWin.Services
{
    /// <summary>
    /// 环境变量管理服务
    /// </summary>
    public class EnvironmentService
    {
        private readonly string _configPath;
        private EnvironmentConfig _config;

        public EnvironmentService()
        {
            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClaudeCodeWin"
            );
            _configPath = Path.Combine(configDir, "environment.json");
            _config = new EnvironmentConfig();
            Load();
        }

        public EnvironmentConfig Config => _config;

        /// <summary>
        /// 加载配置
        /// </summary>
        public void Load()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    _config = JsonConvert.DeserializeObject<EnvironmentConfig>(json) ?? new EnvironmentConfig();
                }
                catch
                {
                    _config = new EnvironmentConfig();
                }
            }

            // 尝试从系统环境变量读取
            LoadFromSystemEnvironment();
        }

        /// <summary>
        /// 从系统环境变量加载 ANTHROPIC 开头的变量
        /// </summary>
        private void LoadFromSystemEnvironment()
        {
            var envVars = Environment.GetEnvironmentVariables();
            foreach (var key in envVars.Keys)
            {
                var keyStr = key?.ToString();
                if (keyStr != null && keyStr.StartsWith("ANTHROPIC_"))
                {
                    var value = envVars[key]?.ToString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        SetConfigValue(keyStr, value);
                    }
                }
            }
        }

        private void SetConfigValue(string key, string value)
        {
            switch (key)
            {
                case "ANTHROPIC_API_KEY":
                    if (string.IsNullOrEmpty(_config.ApiKey)) _config.ApiKey = value;
                    break;
                case "ANTHROPIC_BASE_URL":
                    if (string.IsNullOrEmpty(_config.BaseUrl)) _config.BaseUrl = value;
                    break;
                case "ANTHROPIC_MODEL":
                    if (string.IsNullOrEmpty(_config.Model)) _config.Model = value;
                    break;
                case "ANTHROPIC_SMALL_FAST_MODEL":
                    if (string.IsNullOrEmpty(_config.SmallFastModel)) _config.SmallFastModel = value;
                    break;
                case "ANTHROPIC_AUTH_TOKEN":
                    if (string.IsNullOrEmpty(_config.AuthToken)) _config.AuthToken = value;
                    break;
                case "ANTHROPIC_PROXY":
                    if (string.IsNullOrEmpty(_config.Proxy)) _config.Proxy = value;
                    break;
                case "ANTHROPIC_TIMEOUT":
                    if (!_config.Timeout.HasValue && int.TryParse(value, out int timeout))
                        _config.Timeout = timeout;
                    break;
                case "ANTHROPIC_MAX_RETRIES":
                    if (!_config.MaxRetries.HasValue && int.TryParse(value, out int retries))
                        _config.MaxRetries = retries;
                    break;
                case "ANTHROPIC_DISABLE_CACHE":
                    if (!_config.DisableCache.HasValue)
                        _config.DisableCache = value == "1" || value.ToLower() == "true";
                    break;
                case "ANTHROPIC_DISABLE_TELEMETRY":
                    if (!_config.DisableTelemetry.HasValue)
                        _config.DisableTelemetry = value == "1" || value.ToLower() == "true";
                    break;
                case "ANTHROPIC_DISABLE_UPDATE_CHECK":
                    if (!_config.DisableUpdateCheck.HasValue)
                        _config.DisableUpdateCheck = value == "1" || value.ToLower() == "true";
                    break;
                case "ANTHROPIC_DEBUG":
                    if (!_config.Debug.HasValue)
                        _config.Debug = value == "1" || value.ToLower() == "true";
                    break;
                case "ANTHROPIC_LOG_LEVEL":
                    if (string.IsNullOrEmpty(_config.LogLevel)) _config.LogLevel = value;
                    break;
                default:
                    // 保存到自定义变量
                    if (!_config.CustomVariables.ContainsKey(key))
                        _config.CustomVariables[key] = value;
                    break;
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public void Save()
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            File.WriteAllText(_configPath, json);
        }

        /// <summary>
        /// 应用环境变量到进程
        /// </summary>
        public void ApplyToProcess(System.Diagnostics.ProcessStartInfo startInfo)
        {
            var envVars = _config.ToDictionary();
            foreach (var kv in envVars)
            {
                startInfo.EnvironmentVariables[kv.Key] = kv.Value;
            }
        }

        /// <summary>
        /// 获取所有 ANTHROPIC 环境变量定义
        /// </summary>
        public static List<EnvVarDefinition> GetAllDefinitions()
        {
            return new List<EnvVarDefinition>
            {
                new("ANTHROPIC_API_KEY", "API 密钥", "您的 Anthropic API 密钥", true, true),
                new("ANTHROPIC_BASE_URL", "API 地址", "自定义 API 端点 URL", false, false),
                new("ANTHROPIC_MODEL", "模型名称", "使用的模型 (如 claude-sonnet-4-20250514)", false, false),
                new("ANTHROPIC_SMALL_FAST_MODEL", "快速模型", "用于快速任务的小模型", false, false),
                new("ANTHROPIC_AUTH_TOKEN", "认证令牌", "认证令牌（如使用 OAuth）", false, true),
                new("ANTHROPIC_PROXY", "代理地址", "HTTP 代理地址 (如 http://127.0.0.1:7890)", false, false),
                new("ANTHROPIC_TIMEOUT", "超时时间", "API 请求超时时间（秒）", false, false),
                new("ANTHROPIC_MAX_RETRIES", "重试次数", "最大重试次数", false, false),
                new("ANTHROPIC_DISABLE_CACHE", "禁用缓存", "设为 1 禁用缓存", false, false),
                new("ANTHROPIC_DISABLE_TELEMETRY", "禁用遥测", "设为 1 禁用遥测数据收集", false, false),
                new("ANTHROPIC_DISABLE_UPDATE_CHECK", "禁用更新检查", "设为 1 禁用自动更新检查", false, false),
                new("ANTHROPIC_DEBUG", "调试模式", "设为 1 启用调试模式", false, false),
                new("ANTHROPIC_LOG_LEVEL", "日志级别", "日志级别 (debug/info/warn/error)", false, false),
            };
        }
    }

    /// <summary>
    /// 环境变量定义
    /// </summary>
    public record EnvVarDefinition(
        string Name,
        string DisplayName,
        string Description,
        bool IsRequired,
        bool IsSecret
    );
}
