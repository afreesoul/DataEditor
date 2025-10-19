using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace GameDataEditor.Models.DataEntries
{
    public abstract class BaseDataRow : INotifyPropertyChanged
    {
        private int _id;
        public int ID
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); OnPropertyChanged(nameof(CompositeDisplayName)); }
        }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(CompositeDisplayName)); }
        }

        [JsonIgnore]
        public string CompositeDisplayName => $"{ID} - {Name}";

        private DataState _state;
        public DataState State
        {
            get => _state;
            set { _state = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
