using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ClaudeCodeWin.Views
{
    public partial class ImportJsonDialog : Window
    {
        public string JsonContent { get; private set; } = "";

        public ImportJsonDialog()
        {
            InitializeComponent();
            JsonTextBox.Focus();
        }

        private void BrowseFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择 JSON 配置文件",
                Filter = "JSON 文件|*.json|所有文件|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var content = File.ReadAllText(dialog.FileName);
                    FilePathBox.Text = dialog.FileName;
                    JsonTextBox.Text = content;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"读取文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var json = JsonTextBox.Text.Trim();
            if (string.IsNullOrEmpty(json))
            {
                MessageBox.Show("请输入 JSON 内容或选择文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 简单验证是否是有效的 JSON
            if (!json.StartsWith("{") || !json.EndsWith("}"))
            {
                MessageBox.Show("请输入有效的 JSON 格式", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            JsonContent = json;
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
