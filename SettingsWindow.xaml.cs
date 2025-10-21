
using GameDataEditor.Models.Settings;
using GameDataEditor.ViewModels;
using System.Windows;

namespace GameDataEditor
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _appSettings;

        public SettingsWindow(AppSettings appSettings)
        {
            InitializeComponent();
            _appSettings = appSettings;
            DataContext = new SettingsViewModel(_appSettings.DataFolderPath ?? string.Empty, _appSettings.CsvFolderPath ?? string.Empty, _appSettings.ExpandNodesByDefault);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
