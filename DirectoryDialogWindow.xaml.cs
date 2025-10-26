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