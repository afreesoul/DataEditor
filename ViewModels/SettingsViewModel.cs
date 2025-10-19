
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

        public string CsvFolderPath
        {
            get => _csvFolderPath;
            set
            {
                _csvFolderPath = value;
                OnPropertyChanged(nameof(CsvFolderPath));
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
        public ICommand BrowseCsvFolderCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        private readonly Window _window;
        private string _csvFolderPath;

        public SettingsViewModel(Window window, AppSettings settings)
        {
            _window = window;
            _dataFolderPath = settings.DataFolderPath ?? string.Empty;
            _csvFolderPath = settings.CsvFolderPath ?? string.Empty;
            _expandNodesByDefault = settings.ExpandNodesByDefault;

            BrowseFolderCommand = new RelayCommand(BrowseDataFolder);
            BrowseCsvFolderCommand = new RelayCommand(BrowseCsvFolder);
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void BrowseDataFolder()
        {
            var directory = ShowFolderBrowser("Select Data Folder");
            if (directory != null) DataFolderPath = directory;
        }

        private void BrowseCsvFolder()
        {
            var directory = ShowFolderBrowser("Select CSV Folder");
            if (directory != null) CsvFolderPath = directory;
        }

        private string ShowFolderBrowser(string title)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false,
                FileName = "_Folder_Selection_"
            };

            if (dialog.ShowDialog() == true)
            {
                return Path.GetDirectoryName(dialog.FileName);
            }
            return null;
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
