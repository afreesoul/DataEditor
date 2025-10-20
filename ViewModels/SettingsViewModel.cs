
using GameDataEditor.Commands;
using System.ComponentModel;
using System.Windows.Forms;

namespace GameDataEditor.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
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

        public RelayCommand SelectDataFolderCommand { get; }
        public RelayCommand SelectCsvFolderCommand { get; }

        public SettingsViewModel(string dataPath, string csvPath, bool expandNodes)
        {
            _dataFolderPath = dataPath;
            _csvFolderPath = csvPath;
            _expandNodesByDefault = expandNodes;

            SelectDataFolderCommand = new RelayCommand(SelectDataFolder);
            SelectCsvFolderCommand = new RelayCommand(SelectCsvFolder);
        }

        private void SelectDataFolder()
        {
            var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                DataFolderPath = dialog.SelectedPath ?? string.Empty;
            }
        }

        private void SelectCsvFolder()
        {
            var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                CsvFolderPath = dialog.SelectedPath ?? string.Empty;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
