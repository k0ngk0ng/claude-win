using System.Windows;
using System.Windows.Controls;
using ClaudeCodeWin.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ClaudeCodeWin.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly EnvironmentService _envService;

        public SettingsWindow(EnvironmentService envService)
        {
            InitializeComponent();
            _envService = envService;
            LoadSettings();
        }

        private void LoadSettings()
        {
            var config = _envService.Config;

            // API 配置
            if (!string.IsNullOrEmpty(config.AuthToken))
                AuthTokenBox.Password = config.AuthToken;
            if (!string.IsNullOrEmpty(config.ApiKey))
                ApiKeyBox.Password = config.ApiKey;
            BaseUrlBox.Text = config.BaseUrl ?? "";
            ModelBox.Text = config.Model ?? "";
            SmallFastModelBox.Text = config.SmallFastModel ?? "";

            // 默认模型配置
            DefaultHaikuModelBox.Text = config.DefaultHaikuModel ?? "";
            DefaultSonnetModelBox.Text = config.DefaultSonnetModel ?? "";
            DefaultOpusModelBox.Text = config.DefaultOpusModel ?? "";

            // 网络配置
            ProxyBox.Text = config.Proxy ?? "";
            TimeoutBox.Text = config.Timeout?.ToString() ?? "";
            MaxRetriesBox.Text = config.MaxRetries?.ToString() ?? "";

            // 功能开关
            SkipPermissionsCheck.IsChecked = config.SkipPermissions ?? true;
            DisableCacheCheck.IsChecked = config.DisableCache ?? false;
            DisableTelemetryCheck.IsChecked = config.DisableTelemetry ?? false;
            DisableUpdateCheckCheck.IsChecked = config.DisableUpdateCheck ?? false;

            // 调试配置
            DebugCheck.IsChecked = config.Debug ?? false;

            // 日志级别
            if (!string.IsNullOrEmpty(config.LogLevel))
            {
                foreach (ComboBoxItem item in LogLevelCombo.Items)
                {
                    if (item.Content?.ToString() == config.LogLevel)
                    {
                        LogLevelCombo.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void ImportJsonButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ImportJsonDialog();
            dialog.Owner = this;

            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.JsonContent))
            {
                try
                {
                    ImportFromJson(dialog.JsonContent);
                    MessageBox.Show("导入成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportFromJson(string json)
        {
            var obj = JObject.Parse(json);

            // 检查是否有 "env" 包装
            JObject envObj;
            if (obj.TryGetValue("env", out var envToken) && envToken is JObject)
            {
                envObj = (JObject)envToken;
            }
            else
            {
                envObj = obj;
            }

            // 填充各字段
            if (envObj.TryGetValue("ANTHROPIC_AUTH_TOKEN", out var authToken))
                AuthTokenBox.Password = authToken.ToString();

            if (envObj.TryGetValue("ANTHROPIC_API_KEY", out var apiKey))
                ApiKeyBox.Password = apiKey.ToString();

            if (envObj.TryGetValue("ANTHROPIC_BASE_URL", out var baseUrl))
                BaseUrlBox.Text = baseUrl.ToString();

            if (envObj.TryGetValue("ANTHROPIC_MODEL", out var model))
                ModelBox.Text = model.ToString();

            if (envObj.TryGetValue("ANTHROPIC_SMALL_FAST_MODEL", out var smallFastModel))
                SmallFastModelBox.Text = smallFastModel.ToString();

            if (envObj.TryGetValue("ANTHROPIC_DEFAULT_HAIKU_MODEL", out var haikuModel))
                DefaultHaikuModelBox.Text = haikuModel.ToString();

            if (envObj.TryGetValue("ANTHROPIC_DEFAULT_SONNET_MODEL", out var sonnetModel))
                DefaultSonnetModelBox.Text = sonnetModel.ToString();

            if (envObj.TryGetValue("ANTHROPIC_DEFAULT_OPUS_MODEL", out var opusModel))
                DefaultOpusModelBox.Text = opusModel.ToString();

            if (envObj.TryGetValue("ANTHROPIC_PROXY", out var proxy))
                ProxyBox.Text = proxy.ToString();

            if (envObj.TryGetValue("ANTHROPIC_TIMEOUT", out var timeout))
                TimeoutBox.Text = timeout.ToString();

            if (envObj.TryGetValue("ANTHROPIC_MAX_RETRIES", out var maxRetries))
                MaxRetriesBox.Text = maxRetries.ToString();

            if (envObj.TryGetValue("ANTHROPIC_DISABLE_CACHE", out var disableCache))
                DisableCacheCheck.IsChecked = disableCache.ToString() == "1" || disableCache.ToString().ToLower() == "true";

            if (envObj.TryGetValue("ANTHROPIC_DISABLE_TELEMETRY", out var disableTelemetry))
                DisableTelemetryCheck.IsChecked = disableTelemetry.ToString() == "1" || disableTelemetry.ToString().ToLower() == "true";

            if (envObj.TryGetValue("ANTHROPIC_DISABLE_UPDATE_CHECK", out var disableUpdateCheck))
                DisableUpdateCheckCheck.IsChecked = disableUpdateCheck.ToString() == "1" || disableUpdateCheck.ToString().ToLower() == "true";

            if (envObj.TryGetValue("ANTHROPIC_DEBUG", out var debug))
                DebugCheck.IsChecked = debug.ToString() == "1" || debug.ToString().ToLower() == "true";

            if (envObj.TryGetValue("ANTHROPIC_LOG_LEVEL", out var logLevel))
            {
                foreach (ComboBoxItem item in LogLevelCombo.Items)
                {
                    if (item.Content?.ToString() == logLevel.ToString())
                    {
                        LogLevelCombo.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var config = _envService.Config;

            // API 配置
            config.AuthToken = string.IsNullOrEmpty(AuthTokenBox.Password) ? null : AuthTokenBox.Password;
            config.ApiKey = string.IsNullOrEmpty(ApiKeyBox.Password) ? null : ApiKeyBox.Password;
            config.BaseUrl = string.IsNullOrEmpty(BaseUrlBox.Text) ? null : BaseUrlBox.Text;
            config.Model = string.IsNullOrEmpty(ModelBox.Text) ? null : ModelBox.Text;
            config.SmallFastModel = string.IsNullOrEmpty(SmallFastModelBox.Text) ? null : SmallFastModelBox.Text;

            // 默认模型配置
            config.DefaultHaikuModel = string.IsNullOrEmpty(DefaultHaikuModelBox.Text) ? null : DefaultHaikuModelBox.Text;
            config.DefaultSonnetModel = string.IsNullOrEmpty(DefaultSonnetModelBox.Text) ? null : DefaultSonnetModelBox.Text;
            config.DefaultOpusModel = string.IsNullOrEmpty(DefaultOpusModelBox.Text) ? null : DefaultOpusModelBox.Text;

            // 网络配置
            config.Proxy = string.IsNullOrEmpty(ProxyBox.Text) ? null : ProxyBox.Text;

            if (int.TryParse(TimeoutBox.Text, out int timeout))
                config.Timeout = timeout;
            else
                config.Timeout = null;

            if (int.TryParse(MaxRetriesBox.Text, out int retries))
                config.MaxRetries = retries;
            else
                config.MaxRetries = null;

            // 功能开关
            config.SkipPermissions = SkipPermissionsCheck.IsChecked == true ? true : false;
            config.DisableCache = DisableCacheCheck.IsChecked == true ? true : null;
            config.DisableTelemetry = DisableTelemetryCheck.IsChecked == true ? true : null;
            config.DisableUpdateCheck = DisableUpdateCheckCheck.IsChecked == true ? true : null;

            // 调试配置
            config.Debug = DebugCheck.IsChecked == true ? true : null;

            var selectedLogLevel = (LogLevelCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            config.LogLevel = string.IsNullOrEmpty(selectedLogLevel) ? null : selectedLogLevel;

            // 保存
            _envService.Save();

            MessageBox.Show("设置已保存！\n\n注意：部分设置需要重新启动 Claude Code 才能生效。",
                "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);

            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
