using GameDataEditor.Models;
using GameDataEditor.Models.DataEntries;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace GameDataEditor.ViewModels
{
    public class FieldViewModel : INotifyPropertyChanged
    {
        private readonly object _instance;
        private readonly PropertyInfo _propertyInfo;
        private readonly ObservableCollection<GameDataTable> _allTables;
        private readonly Action<string> _log;

        public string Key => _propertyInfo.Name;
        public bool HasSubFields => SubFields.Count > 0;
        public bool IsEnum { get; private set; }
        public bool IsForeignKey { get; private set; }

        public IEnumerable<string> EnumValues { get; private set; } = Enumerable.Empty<string>();
        
        private IEnumerable<BaseDataRow> _foreignKeyOptions = Enumerable.Empty<BaseDataRow>();
        public IEnumerable<BaseDataRow> ForeignKeyOptions
        {
            get => _foreignKeyOptions;
            private set
            {
                if (!Equals(_foreignKeyOptions, value))
                {
                    _foreignKeyOptions = value;
                    OnPropertyChanged(nameof(ForeignKeyOptions));
                }
            }
        }

        public ObservableCollection<FieldViewModel> SubFields { get; } = new();

        private bool _isExpanded;
        public bool IsExpanded { get => _isExpanded; set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); } } }

        public BaseDataRow? SelectedForeignKey
        {
            get
            {
                if (!IsForeignKey) return null;
                var fkValue = _propertyInfo.GetValue(_instance);
                var id = (int)fkValue?.GetType().GetProperty("ID")?.GetValue(fkValue)!;
                return ForeignKeyOptions.FirstOrDefault(o => o.ID == id);
            }
            set
            {
                if (!IsForeignKey || value == null) return;
                var id = value.ID;
                var fkType = _propertyInfo.PropertyType;
                var newFkInstance = Activator.CreateInstance(fkType);
                fkType.GetProperty("ID")?.SetValue(newFkInstance, id);
                _propertyInfo.SetValue(_instance, newFkInstance);
                OnPropertyChanged(nameof(SelectedForeignKey));
            }
        }

        public object? Value
        {
            get
            {
                object? val = _propertyInfo.GetValue(_instance);
                return IsEnum ? val?.ToString() : val;
            }
            set
            {
                try
                {
                    object? convertedValue;
                    if (IsEnum)
                    {
                        if (value == null) return;
                        convertedValue = Enum.Parse(_propertyInfo.PropertyType, value.ToString()!);
                    }
                    else
                    {
                        var propertyType = Nullable.GetUnderlyingType(_propertyInfo.PropertyType) ?? _propertyInfo.PropertyType;
                        convertedValue = (value == null || value.ToString() == string.Empty)
                            ? null
                            : Convert.ChangeType(value, propertyType, CultureInfo.InvariantCulture);
                    }

                    if (convertedValue == null && _propertyInfo.PropertyType.IsValueType && Nullable.GetUnderlyingType(_propertyInfo.PropertyType) == null) return;

                    if (!object.Equals(_propertyInfo.GetValue(_instance), convertedValue))
                    {
                        _propertyInfo.SetValue(_instance, convertedValue, null);
                        OnPropertyChanged(nameof(Value));
                    }
                }
                catch { /* Ignore conversion errors */ }
            }
        }

        public FieldViewModel(object instance, PropertyInfo propertyInfo, ObservableCollection<GameDataTable> allTables, Action<string> log)
        {
            _instance = instance;
            _propertyInfo = propertyInfo;
            _allTables = allTables;
            _log = log;

            CheckFieldType();
            PopulateSubFields();
        }

        private void CheckFieldType()
        {
            Type propType = _propertyInfo.PropertyType;
            IsEnum = propType.IsEnum;
            if (IsEnum) EnumValues = Enum.GetNames(propType);

            IsForeignKey = propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(ForeignKey<>);
            _log($"  Field '{Key}': IsForeignKey = {IsForeignKey}");
            if (IsForeignKey)
            {
                Type referencedType = propType.GetGenericArguments()[0];
                _log($"  Field '{Key}': Referenced data type is '{referencedType.Name}'");

                var targetTable = _allTables.FirstOrDefault(t => t.DataType == referencedType);
                if (targetTable != null)
                {
                    _log($"  Field '{Key}': Found target table '{targetTable.Name}' with {targetTable.Rows.Count} rows.");
                    ForeignKeyOptions = targetTable.Rows;
                }
                else
                {
                    _log($"  Field '{Key}': FAILED to find target table for type '{referencedType.Name}'.");
                }
            }
        }

        private void PopulateSubFields()
        {
            SubFields.Clear();
            object? currentValue = _propertyInfo.GetValue(_instance);
            if (currentValue == null) return;

            Type valueType = currentValue.GetType();
            if (valueType.IsClass && valueType != typeof(string) && !IsForeignKey)
            {
                var subProperties = valueType.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.MetadataToken);
                foreach (var subProp in subProperties)
                {
                    SubFields.Add(new FieldViewModel(currentValue, subProp, _allTables, _log));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}