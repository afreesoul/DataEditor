using GameDataEditor.Models;
using GameDataEditor.Models.DataEntries;
using GameDataEditor.Models.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace GameDataEditor.ViewModels
{
    public class FieldViewModel : INotifyPropertyChanged
    {
        private readonly object _instance;
        private readonly PropertyInfo _propertyInfo;
        private readonly ObservableCollection<GameDataTable> _allTables;
        private readonly Action<string> _log;
        private readonly string _tableName;
        private readonly Action<string, object, int, int> _onIdChanged;

        // For collection items
        private readonly bool _isCollectionItem;
        private readonly int _collectionIndex = -1;

        public string Key { get; private set; }
        public bool HasSubFields => SubFields.Count > 0;
        public bool IsEnum { get; private set; }
        public bool IsForeignKey { get; private set; }
        public bool IsIdField { get; }
        public bool IsCollection { get; private set; }
        public bool IsCollectionItem => _isCollectionItem;

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

                _log($"SelectedForeignKey GET: Trying to find item with ID {id} in ForeignKeyOptions for field '{Key}'.");
                var result = ForeignKeyOptions.FirstOrDefault(o => o.ID == id);
                if (result == null)
                {
                    _log($"SelectedForeignKey GET: FAILED to find item with ID {id}.");
                }
                else
                {
                    _log($"SelectedForeignKey GET: Found item '{result.CompositeDisplayName}'.");
                }
                return result;
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
                if (IsCollection) return "(Collection)";

                object? val;
                if (_isCollectionItem)
                {
                    val = ((IList)_instance)[_collectionIndex];
                }
                else
                {
                    val = _propertyInfo.GetValue(_instance);
                }
                
                return IsEnum ? val?.ToString() : val;
            }
            set
            {
                try
                {
                    Type propertyType;
                    if (_isCollectionItem)
                    {
                        propertyType = _propertyInfo.PropertyType.GetGenericArguments()[0];
                    }
                    else
                    {
                        propertyType = _propertyInfo.PropertyType;
                    }

                    object? convertedValue;
                    if (IsEnum)
                    {
                        if (value == null) return;
                        convertedValue = Enum.Parse(propertyType, value.ToString()!);
                    }
                    else
                    {
                        var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
                        convertedValue = (value == null || value.ToString() == string.Empty)
                            ? null
                            : Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                    }

                    if (convertedValue == null && propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) == null) return;

                    if (_isCollectionItem)
                    {
                        var list = (IList)_instance;
                        if (!object.Equals(list[_collectionIndex], convertedValue))
                        {
                            list[_collectionIndex] = convertedValue;
                            OnPropertyChanged(nameof(Value));
                        }
                        return;
                    }

                    if (!object.Equals(_propertyInfo.GetValue(_instance), convertedValue))
                    {
                        if (Key == "ID")
                        {
                            var newId = (int)convertedValue!;

                            var table = _allTables.FirstOrDefault(t => t.Name == _tableName);
                            if (table != null && table.Rows.Any(r => r.ID == newId && r != _instance))
                            {
                                MessageBox.Show($"ID {newId} already exists in table '{_tableName}'. Please choose a unique ID.", "Duplicate ID", MessageBoxButton.OK, MessageBoxImage.Error);
                                OnPropertyChanged(nameof(Value));
                                return;
                            }

                            var oldId = (int)_propertyInfo.GetValue(_instance)!;
                            _log($"ID field for table '{_tableName}' changed. Old: {oldId}, New: {newId}. Invoking callback.");
                            _propertyInfo.SetValue(_instance, convertedValue, null);
                            _onIdChanged?.Invoke(_tableName, _instance, oldId, newId);
                        }
                        else
                        {
                            _propertyInfo.SetValue(_instance, convertedValue, null);
                        }
                        OnPropertyChanged(nameof(Value));
                    }
                }
                catch (Exception ex) { _log($"Error setting value for {Key}: {ex.Message}"); }
            }
        }

        // Main constructor
        public FieldViewModel(object instance, PropertyInfo propertyInfo, ObservableCollection<GameDataTable> allTables, Action<string> log, string tableName, Action<string, object, int, int> onIdChanged)
        {
            _instance = instance;
            _propertyInfo = propertyInfo;
            _allTables = allTables;
            _log = log;
            _tableName = tableName;
            _onIdChanged = onIdChanged;

            Key = _propertyInfo.Name;
            IsIdField = (Key == "ID");

            CheckFieldType();
            PopulateSubFields();
        }

        // Constructor for collection items
        private FieldViewModel(object collectionInstance, PropertyInfo collectionPropInfo, int index, Action<string> log)
        {
            _instance = collectionInstance;
            _propertyInfo = collectionPropInfo; // This is the PropertyInfo of the collection itself
            _collectionIndex = index;
            _isCollectionItem = true;
            _log = log;

            Key = $"[{index}]";
            
            // Defaults for non-applicable fields
            _allTables = new ObservableCollection<GameDataTable>();
            _tableName = string.Empty;
            _onIdChanged = (a,b,c,d) => {};
            IsIdField = false;

            CheckFieldType(); // Check if the item itself is an enum, etc.
            PopulateSubFields();
        }

        private void CheckFieldType()
        {
            Type propType;
            if (_isCollectionItem)
            {
                // For collection items, the "property type" is the generic argument of the list
                propType = _propertyInfo.PropertyType.GetGenericArguments()[0];
            }
            else
            {
                propType = _propertyInfo.PropertyType;
            }

            IsEnum = propType.IsEnum;
            if (IsEnum) EnumValues = Enum.GetNames(propType);

            IsForeignKey = propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(ForeignKey<>);
            if (IsForeignKey)
            {
                Type referencedType = propType.GetGenericArguments()[0];
                var targetTable = _allTables.FirstOrDefault(t => t.DataType == referencedType);
                if (targetTable != null)
                {
                    ForeignKeyOptions = targetTable.Rows;
                }
            }

            // This check is only for top-level properties, not items within collections.
            if (!_isCollectionItem)
            {
                // 检查是否为FixedLengthArray<T>类型
                bool isFixedLengthArray = propType.IsGenericType && 
                                        propType.GetGenericTypeDefinition() == typeof(FixedLengthArray<>);
                
                // 如果是FixedLengthArray<T>，将其标记为集合，以便为每个元素创建独立的编辑控件
                if (isFixedLengthArray)
                {
                    IsCollection = true;
                }
                else
                {
                    IsCollection = typeof(IList).IsAssignableFrom(propType) && propType != typeof(string);
                }
            }
        }

        private void PopulateSubFields()
        {
            SubFields.Clear();
            object? currentValue;

            if (_isCollectionItem)
            {
                currentValue = ((IList)_instance)[_collectionIndex];
            }
            else
            {
                try
                {
                    currentValue = _propertyInfo.GetValue(_instance);
                }
                catch (System.Reflection.TargetParameterCountException)
                {
                    // 处理FixedLengthArray<T>的反射问题
                    _log($"Warning: Failed to get value for property {_propertyInfo.Name} due to parameter count mismatch. Skipping subfields.");
                    return;
                }
            }

            if (currentValue == null) return;

            if (IsCollection)
            {
                var list = (IList)currentValue;
                for (int i = 0; i < list.Count; i++)
                {
                    SubFields.Add(new FieldViewModel(list, _propertyInfo, i, _log));
                }
            }
            else
            {
                Type valueType = currentValue.GetType();
                
                if (valueType.IsClass && valueType != typeof(string) && !IsForeignKey)
                {
                    var subProperties = valueType.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.MetadataToken);
                    foreach (var subProp in subProperties)
                    {
                        // Pass the parent's _onIdChanged delegate down to children
                        SubFields.Add(new FieldViewModel(currentValue, subProp, _allTables, _log, _tableName, _onIdChanged));
                    }
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}