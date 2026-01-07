using System.Windows;

namespace ClaudeCodeWin
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 确保配置目录存在
            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClaudeCodeWin"
            );

            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }
        }
    }
}
