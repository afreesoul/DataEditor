
using GameDataEditor.Commands;
using GameDataEditor.Models.Settings;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

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
                _dataFolderPath = value;
                OnPropertyChanged(nameof(DataFolderPath));
            }
        }

        private bool _expandNodesByDefault;
        public bool ExpandNodesByDefault
        {
            get => _expandNodesByDefault;
            set
            {
                _expandNodesByDefault = value;
                OnPropertyChanged(nameof(ExpandNodesByDefault));
            }
        }

        public ICommand BrowseFolderCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        private readonly Window _window;

        public SettingsViewModel(Window window, AppSettings settings)
        {
            _window = window;
            _dataFolderPath = settings.DataFolderPath ?? string.Empty;
            _expandNodesByDefault = settings.ExpandNodesByDefault;

            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void BrowseFolder()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Data Folder",
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false,
                FileName = "_Folder_Selection_"
            };

            if (dialog.ShowDialog() == true)
            {
                string? directory = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(directory))
                {
                    DataFolderPath = directory;
                }
            }
        }

        private void Save()
        {
            _window.DialogResult = true;
            _window.Close();
        }

        private void Cancel()
        {
            _window.DialogResult = false;
            _window.Close();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
