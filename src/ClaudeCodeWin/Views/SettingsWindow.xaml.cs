using System.Windows;
using System.Windows.Controls;
using ClaudeCodeWin.Services;

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
            if (!string.IsNullOrEmpty(config.ApiKey))
                ApiKeyBox.Password = config.ApiKey;
            BaseUrlBox.Text = config.BaseUrl ?? "";
            ModelBox.Text = config.Model ?? "";
            SmallFastModelBox.Text = config.SmallFastModel ?? "";

            // 网络配置
            ProxyBox.Text = config.Proxy ?? "";
            TimeoutBox.Text = config.Timeout?.ToString() ?? "";
            MaxRetriesBox.Text = config.MaxRetries?.ToString() ?? "";

            // 功能开关
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

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var config = _envService.Config;

            // API 配置
            config.ApiKey = string.IsNullOrEmpty(ApiKeyBox.Password) ? null : ApiKeyBox.Password;
            config.BaseUrl = string.IsNullOrEmpty(BaseUrlBox.Text) ? null : BaseUrlBox.Text;
            config.Model = string.IsNullOrEmpty(ModelBox.Text) ? null : ModelBox.Text;
            config.SmallFastModel = string.IsNullOrEmpty(SmallFastModelBox.Text) ? null : SmallFastModelBox.Text;

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
