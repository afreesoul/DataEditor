using System.Windows;

namespace GameDataEditor
{
    public partial class CommentDialogWindow : Window
    {
        public string? CommentText { get; private set; }

        public CommentDialogWindow(string currentComment)
        {
            InitializeComponent();
            CommentTextBox.Text = currentComment;
            CommentText = currentComment;
            
            // 设置初始按钮状态
            UpdateOkButtonState();
        }

        private void CommentTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            CommentText = CommentTextBox.Text?.Trim();
            UpdateOkButtonState();
        }

        private void UpdateOkButtonState()
        {
            // 始终启用确定按钮，允许清空注释
            OkButton.IsEnabled = true;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            CommentText = CommentTextBox.Text?.Trim();
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