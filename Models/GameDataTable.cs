using GameDataEditor.Models.DataEntries;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace GameDataEditor.Models
{
    public class GameDataTable : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public ObservableCollection<BaseDataRow> Rows { get; set; }
        public Type DataType { get; set; }

        private string _comment = string.Empty;
        public string Comment
        {
            get => _comment;
            set
            {
                if (_comment != value)
                {
                    _comment = value;
                    OnPropertyChanged(nameof(Comment));
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public string DisplayName => string.IsNullOrEmpty(Comment) ? Name : $"{Name} - {Comment}";

        public GameDataTable(string name, Type dataType)
        {
            Name = name;
            DataType = dataType;
            Rows = new ObservableCollection<BaseDataRow>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}