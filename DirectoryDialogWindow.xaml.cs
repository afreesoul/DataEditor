using System.Windows;

namespace GameDataEditor
{
    public partial class DirectoryDialogWindow : Window
    {
        public string? DirectoryName { get; private set; }

        public DirectoryDialogWindow()
        {
            InitializeComponent();
            
            // 设置初始按钮状态
            UpdateOkButtonState();
        }

        public DirectoryDialogWindow(string title, string initialName = "")
        {
            InitializeComponent();
            
            // 设置窗口标题
            Title = title;
            
            // 设置初始名称
            if (!string.IsNullOrEmpty(initialName))
            {
                DirectoryTextBox.Text = initialName;
                DirectoryName = initialName;
            }
            
            // 设置初始按钮状态
            UpdateOkButtonState();
        }

        private void DirectoryTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            DirectoryName = DirectoryTextBox.Text?.Trim();
            UpdateOkButtonState();
        }

        private void UpdateOkButtonState()
        {
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(DirectoryName);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DirectoryName = DirectoryTextBox.Text?.Trim();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}