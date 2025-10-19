using GameDataEditor.Commands;
using GameDataEditor.Models;
using GameDataEditor.Models.DataEntries;
using GameDataEditor.Models.Settings;
using GameDataEditor.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using GameDataEditor.Models.DataEntries.Complex;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;

namespace GameDataEditor.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService _settingsService;
        private AppSettings _appSettings;
        private readonly Dictionary<string, int> _lastSelectedRowIds = new Dictionary<string, int>();

        // Logging
        private string _logOutput = string.Empty;
        public string LogOutput
        {
            get => _logOutput;
            set
            {
                _logOutput = value;
                OnPropertyChanged(nameof(LogOutput));
            }
        }

        // Mapping from table name to data type
        private static readonly Dictionary<string, Type> TableTypeMapping = new()
        {
            { "Items", typeof(Item) },
            { "Monsters", typeof(Monster) },
            { "Quests", typeof(Quest) }
        };

        private ObservableCollection<GameDataTable> _gameTables = new();
        public ObservableCollection<GameDataTable> GameTables
        {
            get => _gameTables;
            set { _gameTables = value; OnPropertyChanged(nameof(GameTables)); }
        }

        public ObservableCollection<FieldViewModel> SelectedRowFields { get; } = new();

        public ICommand SaveCommand { get; }
        public ICommand LoadCommand { get; }
        public ICommand SettingsCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand AddNewRowCommand { get; }
        public ICommand DeleteRowCommand { get; }

        public bool IsRowSelected => SelectedRow != null;

        private GameDataTable? _selectedTable;
        public GameDataTable? SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (_selectedTable != value)
                {
                    _selectedTable = value;
                    Log($"Table selection changed to: {(_selectedTable == null ? "null" : _selectedTable.Name)}");
                    OnPropertyChanged(nameof(SelectedTable));

                    if (_selectedTable != null)
                    {
                        // Try to restore last selected row
                        if (_lastSelectedRowIds.TryGetValue(_selectedTable.Name, out int lastSelectedId))
                        {
                            SelectedRow = _selectedTable.Rows.FirstOrDefault(r => r.ID == lastSelectedId);
                        }
                        
                        // If no last selection or it wasn't found, default to the first row
                        if (SelectedRow == null)
                        {
                            SelectedRow = _selectedTable.Rows.FirstOrDefault();
                        }
                    }
                    else
                    {
                        SelectedRow = null;
                    }
                }
            }
        }

        private BaseDataRow? _selectedRow;
        public BaseDataRow? SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (_selectedRow != value)
                {
                    _selectedRow = value;
                    OnPropertyChanged(nameof(SelectedRow));
                    OnPropertyChanged(nameof(IsRowSelected));
                    UpdateFieldsDisplay();

                    // Remember the last selected row for the current table
                    if (_selectedRow != null && _selectedTable != null)
                    {
                        _lastSelectedRowIds[_selectedTable.Name] = _selectedRow.ID;
                    }
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainViewModel()
        {
            Log("ViewModel initializing...");
            _settingsService = new SettingsService();
            _appSettings = _settingsService.LoadSettings();

            SaveCommand = new RelayCommand(SaveAllToFolder);
            LoadCommand = new RelayCommand(LoadFromFolder);
            SettingsCommand = new RelayCommand(OpenSettings);
            ExpandAllCommand = new RelayCommand(ExpandAllFields);
            CollapseAllCommand = new RelayCommand(CollapseAllFields);
            ClearLogCommand = new RelayCommand(ClearLog);
            AddNewRowCommand = new RelayCommand(AddNewRow);
            DeleteRowCommand = new RelayCommand(DeleteRow);
            LoadSampleData();
            Log("ViewModel initialized.");
        }

        private void OpenSettings()
        {
            var settingsWindow = new SettingsWindow(_appSettings);
            settingsWindow.Owner = Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive);
            if (settingsWindow.ShowDialog() == true)
            {
                // Retrieve the updated settings from the dialog's ViewModel
                var settingsVm = (SettingsViewModel)settingsWindow.DataContext;
                _appSettings.DataFolderPath = settingsVm.DataFolderPath;
                _appSettings.ExpandNodesByDefault = settingsVm.ExpandNodesByDefault;
                _settingsService.SaveSettings(_appSettings);
                Log("Settings saved.");
            }
            else
            {
                Log("Settings changes cancelled.");
            }
        }

        public void Log(string message)
        {
            LogOutput += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
        }

        private void ClearLog()
        {
            LogOutput = string.Empty;
        }

        private void ExpandAllFields()
        {
            foreach (var field in SelectedRowFields)
            {
                SetExpandedRecursively(field, true);
            }
            _appSettings.ExpandNodesByDefault = true;
            _settingsService.SaveSettings(_appSettings);
        }

        private void CollapseAllFields()
        {
            foreach (var field in SelectedRowFields)
            {
                SetExpandedRecursively(field, false);
            }
            _appSettings.ExpandNodesByDefault = false;
            _settingsService.SaveSettings(_appSettings);
        }

        private void SetExpandedRecursively(FieldViewModel field, bool isExpanded)
        {
            if (field.HasSubFields)
            {
                field.IsExpanded = isExpanded;
                foreach (var subField in field.SubFields)
                {
                    SetExpandedRecursively(subField, isExpanded);
                }
            }
        }

        private void AddNewRow()
        {
            if (SelectedTable == null || SelectedRow == null) return;

            // 1. Generate new ID by finding the next available slot after the selected row's ID
            int newId = SelectedRow.ID + 1;
            var existingIds = new HashSet<int>(SelectedTable.Rows.Select(r => r.ID));
            while (existingIds.Contains(newId))
            {
                newId++;
            }

            // 2. Deep copy the selected row via serialization
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                TypeInfoResolver = new OrderedPropertiesResolver(),
                Converters = { new JsonStringEnumConverter(), new ForeignKeyConverterFactory() }
            };
            string json = JsonSerializer.Serialize(SelectedRow, SelectedRow.GetType(), options);
            var newRow = (BaseDataRow)JsonSerializer.Deserialize(json, SelectedRow.GetType(), options)!;

            // 3. Update the ID and Name
            newRow.ID = newId;
            newRow.Name += " (Copy)";

            // 4. Add to the collection in sorted order
            int insertIndex = 0;
            for (int i = 0; i < SelectedTable.Rows.Count; i++)
            {
                if (SelectedTable.Rows[i].ID > newRow.ID)
                {
                    break;
                }
                insertIndex++;
            }
            SelectedTable.Rows.Insert(insertIndex, newRow);
        }

        private void DeleteRow()
        {
            if (SelectedRow == null || SelectedTable == null) return;

            var rowToDelete = SelectedRow; // Keep a reference for logging
            SelectedTable.Rows.Remove(SelectedRow);
            Log($"Row {rowToDelete.ID} - {rowToDelete.Name} deleted.");
            SelectedRow = null;
        }

        private void LoadFromFolder()
        {
            string? directory = _appSettings.DataFolderPath;

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                Log("Data folder not set or invalid. Please configure it in Settings.");
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Select a Data Folder",
                    CheckFileExists = false,
                    CheckPathExists = true,
                    ValidateNames = false,
                    FileName = "_Folder_Selection_"
                };

                if (openFileDialog.ShowDialog() != true) return;
                
                directory = Path.GetDirectoryName(openFileDialog.FileName);
            }

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                Log("Invalid directory selected.");
                return;
            }

            var loadedTables = new ObservableCollection<GameDataTable>();
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(), new ForeignKeyConverterFactory() },
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };

            foreach (var kvp in TableTypeMapping)
            {
                string fileName = $"{kvp.Key}.json";
                string filePath = Path.Combine(directory, fileName);

                if (!File.Exists(filePath)) continue;

                var table = new GameDataTable(kvp.Key, kvp.Value);
                string jsonString = File.ReadAllText(filePath);

                var rows = (IEnumerable<object>?)JsonSerializer.Deserialize(jsonString, typeof(ObservableCollection<>).MakeGenericType(kvp.Value), options);
                if (rows != null)
                {
                    var sortedRows = rows.Cast<BaseDataRow>().OrderBy(r => r.ID);
                    foreach (var row in sortedRows)
                    {
                        table.Rows.Add(row);
                    }
                }
                loadedTables.Add(table);
            }

            GameTables = loadedTables;
            SelectedTable = null;
            SelectedRow = null;
            Log($"Data loaded from {directory}.");
        }

        private void SaveAllToFolder()
        {
            string? targetDirectory = _appSettings.DataFolderPath;
            if (string.IsNullOrEmpty(targetDirectory))
            {
                Log("Data folder not set. Please configure it in Settings or use Save As.");
                var saveFileDialog = new SaveFileDialog
                {
                    Title = "Select Folder to Save Files",
                    FileName = "_Select_a_Folder_"
                };

                if (saveFileDialog.ShowDialog() != true) return;

                targetDirectory = Path.GetDirectoryName(saveFileDialog.FileName);
            }

            if (string.IsNullOrEmpty(targetDirectory))
            {
                Log("Invalid save directory.");
                return;
            }

            var options = new JsonSerializerOptions 
            {
                WriteIndented = true,
                TypeInfoResolver = new OrderedPropertiesResolver(),
                Converters = { new JsonStringEnumConverter(), new ForeignKeyConverterFactory() }
            };

            foreach (var table in GameTables)
            {
                string filePath = Path.Combine(targetDirectory, $"{table.Name}.json");

                using var stream = new MemoryStream();
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

                writer.WriteStartArray();
                foreach (var row in table.Rows)
                {
                    // Serialize each object with its actual runtime type to avoid the '$type' field
                    JsonSerializer.Serialize(writer, row, row.GetType(), options);
                }
                writer.WriteEndArray();
                writer.Flush();

                File.WriteAllBytes(filePath, stream.ToArray());
            }
            Log($"All data saved to {targetDirectory}.");
        }

        public void UpdateForeignKeyReferences(Type changedDataType, int oldId, int newId)
        {
            Log($"--- Starting Foreign Key Update ---");
            Log($"Searching for references to {changedDataType.Name} with old ID {oldId} to update to {newId}.");
            foreach (var table in GameTables)
            {
                foreach (var row in table.Rows)
                {
                    var properties = row.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var prop in properties)
                    {
                        var propType = prop.PropertyType;
                        if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(ForeignKey<>))
                        {
                            Type referencedType = propType.GetGenericArguments()[0];
                            if (referencedType == changedDataType)
                            {
                                var fkValue = prop.GetValue(row);
                                if (fkValue == null) continue;

                                var idProp = fkValue.GetType().GetProperty("ID");
                                if (idProp == null) continue;

                                var currentId = (int)idProp.GetValue(fkValue);

                                if (currentId == oldId)
                                {
                                    Log($"Found match in table '{table.Name}', row '{row.ID} - {row.Name}', property '{prop.Name}'. Updating value to {newId}.");
                                    var newFkInstance = Activator.CreateInstance(prop.PropertyType);
                                    prop.PropertyType.GetProperty("ID").SetValue(newFkInstance, newId);
                                    prop.SetValue(row, newFkInstance);
                                }
                            }
                        }
                    }
                }
            }
            Log($"--- Finished Foreign Key Update ---");
        }

        private void UpdateFieldsDisplay()
        {
            SelectedRowFields.Clear();
            if (SelectedRow == null || SelectedTable == null) return;

            var properties = SelectedRow.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != "CompositeDisplayName");

            // Define the explicit order for base class properties
            var baseOrder = new Dictionary<string, int> { { "ID", 0 }, { "Name", 1 }, { "State", 2 } };

            var orderedProperties = properties
                // Sort by group: base properties first, then derived
                .OrderBy(p => p.DeclaringType != typeof(BaseDataRow))
                // Sort base properties by the defined order
                .ThenBy(p => baseOrder.TryGetValue(p.Name, out var order) ? order : int.MaxValue)
                // Sort derived properties by their declaration order
                .ThenBy(p => p.MetadataToken);

            foreach (var prop in orderedProperties)
            {
                SelectedRowFields.Add(new FieldViewModel(SelectedRow, prop, GameTables, Log, SelectedTable.Name, (tableName, item, oldId, newId) => {
                    Log($"OnIdChanged delegate fired for table '{tableName}', old ID {oldId}, new ID {newId}.");
                    var changedTable = GameTables.FirstOrDefault(t => t.Name == tableName);
                    if (changedTable != null && item is BaseDataRow dataRow)
                    {
                        Log($"Found table '{changedTable.Name}'. Item to move: {dataRow.ID} - {dataRow.Name}.");
                        // Re-sort the source table to trigger CollectionView updates
                        Log("Removing item from collection.");
                        changedTable.Rows.Remove(dataRow);
                        int insertIndex = 0;
                        for (int i = 0; i < changedTable.Rows.Count; i++)
                        {
                            if (changedTable.Rows[i].ID > newId)
                            {
                                break;
                            }
                            insertIndex++;
                        }
                        Log($"Re-inserting item at index {insertIndex}.");
                        changedTable.Rows.Insert(insertIndex, dataRow);

                        // Update foreign keys in other tables
                        UpdateForeignKeyReferences(changedTable.DataType, oldId, newId);

                        // Reselect the row to ensure the UI is consistent
                        Log("Reselecting row.");
                        SelectedRow = dataRow;
                    }
                    else
                    {
                        Log("OnIdChanged: Failed to find table or item.");
                    }
                }));
            }

            // Apply the default expansion setting
            foreach (var field in SelectedRowFields)
            {
                SetExpandedRecursively(field, _appSettings.ExpandNodesByDefault);
            }
        }

        private void LoadSampleData()
        {
            GameTables.Clear();

            // Items Table
            var itemsTable = new GameDataTable("Items", typeof(Item));
            var items = new List<Item>
            {
                new Item { ID = 1001, Name = "Health Potion", State = DataState.Active, Value = 50, Description = "Restores 50 HP." },
                new Item { ID = 1002, Name = "Mana Potion", State = DataState.Active, Value = 75, Description = "Restores 100 MP." },
                new Item { ID = 2001, Name = "Sword", State = DataState.Active, Damage = 12, Type = "Melee" }
            };
            foreach(var item in items.OrderBy(i => i.ID)) itemsTable.Rows.Add(item);
            GameTables.Add(itemsTable);

            // Monsters Table
            var monstersTable = new GameDataTable("Monsters", typeof(Monster));
            var monsters = new List<Monster>
            {
                new Monster { ID = 1, Name = "Slime", State = DataState.Active, HP = 50, Attack = 5, Experience = 10, BaseStats = new Stats { Strength = 5, Dexterity = 5, Intelligence = 1, ElementalResistances = new Resistances { Fire = 10, Ice = -5, Lightning = 0 } } },
                new Monster { ID = 2, Name = "Goblin", State = DataState.Active, HP = 100, Attack = 15, Experience = 25, BaseStats = new Stats { Strength = 10, Dexterity = 8, Intelligence = 2, ElementalResistances = new Resistances { Fire = 20, Ice = 0, Lightning = 5 } } },
                new Monster { ID = 3, Name = "Dragon", State = DataState.Active, HP = 5000, Attack = 120, Experience = 1000, BaseStats = new Stats { Strength = 100, Dexterity = 60, Intelligence = 80, ElementalResistances = new Resistances { Fire = 100, Ice = 75, Lightning = 50 } } }
            };
            foreach(var monster in monsters.OrderBy(m => m.ID)) monstersTable.Rows.Add(monster);
            GameTables.Add(monstersTable);

            // Quests Table
            var questsTable = new GameDataTable("Quests", typeof(Quest));
            var quests = new List<Quest>
            {
                new Quest { ID = 1, Name = "Main Quest 1", State = DataState.Active, Title = "A New Beginning", RequiredLevel = 1 },
                new Quest { ID = 2, Name = "Side Quest A", State = DataState.Active, Title = "Lost and Found", GiverNPC = 2 } // Goblin's ID is 2
            };
            foreach(var quest in quests.OrderBy(q => q.ID)) questsTable.Rows.Add(quest);
            GameTables.Add(questsTable);
        }
    }
}