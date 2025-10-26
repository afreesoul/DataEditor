using GameDataEditor.Models.DataEntries;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GameDataEditor.Models
{
    public enum DataItemType
    {
        Table,
        Directory
    }

    public interface IDataItem : INotifyPropertyChanged
    {
        string Name { get; set; }
        string DisplayName { get; }
        DataItemType ItemType { get; }
        bool IsExpanded { get; set; }
        IDataItem? Parent { get; set; }
    }

    public class DataDirectory : IDataItem
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public string DisplayName => $"📁 {Name}";
        public DataItemType ItemType => DataItemType.Directory;

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public IDataItem? Parent { get; set; }

        public ObservableCollection<IDataItem> Children { get; } = new ObservableCollection<IDataItem>();

        public DataDirectory(string name)
        {
            Name = name;
        }

        public void AddChild(IDataItem item)
        {
            item.Parent = this;
            Children.Add(item);
        }

        public void RemoveChild(IDataItem item)
        {
            item.Parent = null;
            Children.Remove(item);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DataTableWrapper : IDataItem
    {
        private readonly GameDataTable _table;

        public string Name 
        { 
            get => _table.Name; 
            set { /* 表名不应该被修改，但为了接口需要实现set方法 */ } 
        }
        
        public string DisplayName => _table.DisplayName;
        public DataItemType ItemType => DataItemType.Table;
        public bool IsExpanded { get; set; }
        public IDataItem? Parent { get; set; }
        public GameDataTable Table => _table;

        public DataTableWrapper(GameDataTable table)
        {
            _table = table;
            _table.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(GameDataTable.DisplayName))
                {
                    OnPropertyChanged(nameof(DisplayName));
                }
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}