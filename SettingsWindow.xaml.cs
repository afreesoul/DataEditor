
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
            var viewModel = new SettingsViewModel(_appSettings.DataFolderPath ?? string.Empty, _appSettings.CsvFolderPath ?? string.Empty, _appSettings.ExpandNodesByDefault);
            DataContext = viewModel;
            
            // Subscribe to the RequestClose event
            viewModel.RequestClose += (result) =>
            {
                DialogResult = result;
                Close();
            };
        }
    }
}
