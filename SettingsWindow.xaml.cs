
using GameDataEditor.Models.Settings;
using GameDataEditor.ViewModels;
using System.Windows;

namespace GameDataEditor
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            DataContext = new SettingsViewModel(this, settings);
        }
    }
}
