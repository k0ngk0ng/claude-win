using System.Windows;

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

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var json = JsonTextBox.Text.Trim();
            if (string.IsNullOrEmpty(json))
            {
                MessageBox.Show("请输入 JSON 内容", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
