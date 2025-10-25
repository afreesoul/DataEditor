
using GameDataEditor.Commands;
using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace GameDataEditor.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        public event Action<bool?>? RequestClose; // true for OK, false for Cancel
        private string _dataFolderPath;
        public string DataFolderPath
        {
            get => _dataFolderPath;
            set
            {
                if (_dataFolderPath != value)
                {
                    _dataFolderPath = value;
                    OnPropertyChanged(nameof(DataFolderPath));
                }
            }
        }

        private string _csvFolderPath;
        public string CsvFolderPath
        {
            get => _csvFolderPath;
            set
            {
                if (_csvFolderPath != value)
                {
                    _csvFolderPath = value;
                    OnPropertyChanged(nameof(CsvFolderPath));
                }
            }
        }

        private bool _expandNodesByDefault;
        public bool ExpandNodesByDefault
        {
            get => _expandNodesByDefault;
            set
            {
                if (_expandNodesByDefault != value)
                {
                    _expandNodesByDefault = value;
                    OnPropertyChanged(nameof(ExpandNodesByDefault));
                }
            }
        }

        public RelayCommand BrowseFolderCommand { get; }
        public RelayCommand BrowseCsvFolderCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public SettingsViewModel(string dataPath, string csvPath, bool expandNodes)
        {
            _dataFolderPath = dataPath;
            _csvFolderPath = csvPath;
            _expandNodesByDefault = expandNodes;

            BrowseFolderCommand = new RelayCommand(SelectDataFolder);
            BrowseCsvFolderCommand = new RelayCommand(SelectCsvFolder);
            SaveCommand = new RelayCommand(SaveSettings);
            CancelCommand = new RelayCommand(CancelSettings);
        }

        private void SelectDataFolder()
        {
            var dialog = new FolderBrowserDialog();
            
            // Set initial directory: if current DataFolderPath exists, use it; otherwise use app base directory
            if (!string.IsNullOrEmpty(_dataFolderPath) && System.IO.Directory.Exists(_dataFolderPath))
            {
                dialog.SelectedPath = _dataFolderPath;
            }
            else
            {
                dialog.SelectedPath = System.AppDomain.CurrentDomain.BaseDirectory;
            }
            
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                DataFolderPath = dialog.SelectedPath ?? string.Empty;
            }
        }

        private void SelectCsvFolder()
        {
            var dialog = new FolderBrowserDialog();
            
            // Set initial directory: if current CsvFolderPath exists, use it; otherwise use app base directory
            if (!string.IsNullOrEmpty(_csvFolderPath) && System.IO.Directory.Exists(_csvFolderPath))
            {
                dialog.SelectedPath = _csvFolderPath;
            }
            else
            {
                dialog.SelectedPath = System.AppDomain.CurrentDomain.BaseDirectory;
            }
            
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                CsvFolderPath = dialog.SelectedPath ?? string.Empty;
            }
        }

        private void SaveSettings()
        {
            // Settings are already updated via data binding
            // Just notify the window to close with OK result
            RequestClose?.Invoke(true);
        }

        private void CancelSettings()
        {
            // Notify the window to close with Cancel result
            RequestClose?.Invoke(false);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
