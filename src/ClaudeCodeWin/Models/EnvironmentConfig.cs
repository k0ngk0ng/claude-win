using Newtonsoft.Json;

namespace ClaudeCodeWin.Models
{
    /// <summary>
    /// Claude Code 环境变量配置
    /// 包含所有 ANTHROPIC 开头的环境变量
    /// </summary>
    public class EnvironmentConfig
    {
        // === API 配置 ===

        /// <summary>
        /// Anthropic API 密钥
        /// </summary>
        [JsonProperty("ANTHROPIC_API_KEY")]
        public string? ApiKey { get; set; }

        /// <summary>
        /// Anthropic API 基础 URL
        /// </summary>
        [JsonProperty("ANTHROPIC_BASE_URL")]
        public string? BaseUrl { get; set; }

        /// <summary>
        /// 使用的模型名称
        /// </summary>
        [JsonProperty("ANTHROPIC_MODEL")]
        public string? Model { get; set; }

        /// <summary>
        /// 小模型名称（用于快速任务）
        /// </summary>
        [JsonProperty("ANTHROPIC_SMALL_FAST_MODEL")]
        public string? SmallFastModel { get; set; }

        /// <summary>
        /// 默认 Haiku 模型
        /// </summary>
        [JsonProperty("ANTHROPIC_DEFAULT_HAIKU_MODEL")]
        public string? DefaultHaikuModel { get; set; }

        /// <summary>
        /// 默认 Sonnet 模型
        /// </summary>
        [JsonProperty("ANTHROPIC_DEFAULT_SONNET_MODEL")]
        public string? DefaultSonnetModel { get; set; }

        /// <summary>
        /// 默认 Opus 模型
        /// </summary>
        [JsonProperty("ANTHROPIC_DEFAULT_OPUS_MODEL")]
        public string? DefaultOpusModel { get; set; }

        // === 认证配置 ===

        /// <summary>
        /// 认证令牌
        /// </summary>
        [JsonProperty("ANTHROPIC_AUTH_TOKEN")]
        public string? AuthToken { get; set; }

        // === 代理配置 ===

        /// <summary>
        /// HTTP 代理地址
        /// </summary>
        [JsonProperty("ANTHROPIC_PROXY")]
        public string? Proxy { get; set; }

        // === 超时配置 ===

        /// <summary>
        /// API 请求超时时间（秒）
        /// </summary>
        [JsonProperty("ANTHROPIC_TIMEOUT")]
        public int? Timeout { get; set; }

        /// <summary>
        /// 最大重试次数
        /// </summary>
        [JsonProperty("ANTHROPIC_MAX_RETRIES")]
        public int? MaxRetries { get; set; }

        // === 功能开关 ===

        /// <summary>
        /// 禁用缓存
        /// </summary>
        [JsonProperty("ANTHROPIC_DISABLE_CACHE")]
        public bool? DisableCache { get; set; }

        /// <summary>
        /// 禁用遥测
        /// </summary>
        [JsonProperty("ANTHROPIC_DISABLE_TELEMETRY")]
        public bool? DisableTelemetry { get; set; }

        /// <summary>
        /// 禁用更新检查
        /// </summary>
        [JsonProperty("ANTHROPIC_DISABLE_UPDATE_CHECK")]
        public bool? DisableUpdateCheck { get; set; }

        // === 调试配置 ===

        /// <summary>
        /// 启用调试模式
        /// </summary>
        [JsonProperty("ANTHROPIC_DEBUG")]
        public bool? Debug { get; set; }

        /// <summary>
        /// 日志级别
        /// </summary>
        [JsonProperty("ANTHROPIC_LOG_LEVEL")]
        public string? LogLevel { get; set; }

        // === 权限配置 ===

        /// <summary>
        /// 跳过权限确认（--dangerously-skip-permissions）
        /// 默认启用以提升使用体验
        /// </summary>
        [JsonProperty("SKIP_PERMISSIONS")]
        public bool? SkipPermissions { get; set; } = true;

        // === GUI 调试配置 ===

        /// <summary>
        /// 启用 GUI 调试输出（显示执行命令、环境变量、退出码等）
        /// </summary>
        [JsonProperty("GUI_DEBUG")]
        public bool? GuiDebug { get; set; }

        // === Git Bash 配置 ===

        /// <summary>
        /// Git Bash 路径（Claude Code 需要）
        /// </summary>
        [JsonProperty("CLAUDE_CODE_GIT_BASH_PATH")]
        public string? GitBashPath { get; set; }

        // === 自定义配置 ===

        /// <summary>
        /// 额外的自定义环境变量
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, string> CustomVariables { get; set; } = new();

        /// <summary>
        /// 获取所有环境变量作为字典
        /// </summary>
        public Dictionary<string, string> ToDictionary()
        {
            var dict = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(ApiKey))
                dict["ANTHROPIC_API_KEY"] = ApiKey;

            if (!string.IsNullOrEmpty(BaseUrl))
                dict["ANTHROPIC_BASE_URL"] = BaseUrl;

            if (!string.IsNullOrEmpty(Model))
                dict["ANTHROPIC_MODEL"] = Model;

            if (!string.IsNullOrEmpty(SmallFastModel))
                dict["ANTHROPIC_SMALL_FAST_MODEL"] = SmallFastModel;

            if (!string.IsNullOrEmpty(DefaultHaikuModel))
                dict["ANTHROPIC_DEFAULT_HAIKU_MODEL"] = DefaultHaikuModel;

            if (!string.IsNullOrEmpty(DefaultSonnetModel))
                dict["ANTHROPIC_DEFAULT_SONNET_MODEL"] = DefaultSonnetModel;

            if (!string.IsNullOrEmpty(DefaultOpusModel))
                dict["ANTHROPIC_DEFAULT_OPUS_MODEL"] = DefaultOpusModel;

            if (!string.IsNullOrEmpty(AuthToken))
                dict["ANTHROPIC_AUTH_TOKEN"] = AuthToken;

            if (!string.IsNullOrEmpty(Proxy))
                dict["ANTHROPIC_PROXY"] = Proxy;

            if (Timeout.HasValue)
                dict["ANTHROPIC_TIMEOUT"] = Timeout.Value.ToString();

            if (MaxRetries.HasValue)
                dict["ANTHROPIC_MAX_RETRIES"] = MaxRetries.Value.ToString();

            if (DisableCache == true)
                dict["ANTHROPIC_DISABLE_CACHE"] = "1";

            if (DisableTelemetry == true)
                dict["ANTHROPIC_DISABLE_TELEMETRY"] = "1";

            if (DisableUpdateCheck == true)
                dict["ANTHROPIC_DISABLE_UPDATE_CHECK"] = "1";

            if (Debug == true)
                dict["ANTHROPIC_DEBUG"] = "1";

            if (!string.IsNullOrEmpty(LogLevel))
                dict["ANTHROPIC_LOG_LEVEL"] = LogLevel;

            if (!string.IsNullOrEmpty(GitBashPath))
                dict["CLAUDE_CODE_GIT_BASH_PATH"] = GitBashPath;

            // 添加自定义变量
            foreach (var kv in CustomVariables)
            {
                dict[kv.Key] = kv.Value;
            }

            return dict;
        }
    }
}
