using GameDataEditor.Commands;
using GameDataEditor.Models;
using GameDataEditor.Models.DataEntries;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using GameDataEditor.Models.DataEntries.Complex;
using GameDataEditor.Utils;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace GameDataEditor.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
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
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand AddNewRowCommand { get; }

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
                    if (_selectedTable != null)
                    {
                        Log($"Selected table '{_selectedTable.Name}' contains {_selectedTable.Rows.Count} rows.");
                        if (_selectedTable.Rows.Count > 0)
                        {
                            var firstRow = _selectedTable.Rows[0];
                            Log($"First row details: ID={firstRow.ID}, Name='{firstRow.Name}'. Type is {firstRow.GetType().Name}");
                        }
                    }
                    OnPropertyChanged(nameof(SelectedTable));
                    SelectedRow = null;
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
            SaveCommand = new RelayCommand(SaveAllToFolder);
            LoadCommand = new RelayCommand(LoadFromFolder);
            ExpandAllCommand = new RelayCommand(ExpandAllFields);
            CollapseAllCommand = new RelayCommand(CollapseAllFields);
            ClearLogCommand = new RelayCommand(ClearLog);
            AddNewRowCommand = new RelayCommand(AddNewRow);
            LoadSampleData();
            Log("ViewModel initialized.");
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
        }

        private void CollapseAllFields()
        {
            foreach (var field in SelectedRowFields)
            {
                SetExpandedRecursively(field, false);
            }
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

            // 4. Add to the collection
            SelectedTable.Rows.Add(newRow);
        }

        private void LoadFromFolder()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select a Data Folder",
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false,
                FileName = "_Folder_Selection_"
            };

            if (openFileDialog.ShowDialog() != true) return;
            
            string? directory = Path.GetDirectoryName(openFileDialog.FileName);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;

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
                    foreach (var row in rows.Cast<BaseDataRow>())
                    {
                        table.Rows.Add(row);
                    }
                }
                loadedTables.Add(table);
            }

            GameTables = loadedTables;
            SelectedTable = null;
            SelectedRow = null;
        }

        private void SaveAllToFolder()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Title = "Select Folder to Save Files",
                FileName = "_Select_a_Folder_"
            };

            if (saveFileDialog.ShowDialog() != true) return;

            string? targetDirectory = Path.GetDirectoryName(saveFileDialog.FileName);
            if (string.IsNullOrEmpty(targetDirectory)) return;

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
        }

        private void UpdateFieldsDisplay()
        {
            SelectedRowFields.Clear();
            if (SelectedRow == null) return;

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
                SelectedRowFields.Add(new FieldViewModel(SelectedRow, prop, GameTables, Log));
            }
        }

        private void LoadSampleData()
        {
            GameTables.Clear();
            // Items Table
            var itemsTable = new GameDataTable("Items", typeof(Item));
            itemsTable.Rows.Add(new Item { ID = 1001, Name = "Health Potion", State = DataState.Active, Value = 50, Description = "Restores 50 HP." });
            itemsTable.Rows.Add(new Item { ID = 1002, Name = "Mana Potion", State = DataState.Active, Value = 75, Description = "Restores 100 MP." });
            itemsTable.Rows.Add(new Item { ID = 2001, Name = "Sword", State = DataState.Active, Damage = 12, Type = "Melee" });
            GameTables.Add(itemsTable);

            // Monsters Table
            var monstersTable = new GameDataTable("Monsters", typeof(Monster));
            monstersTable.Rows.Add(new Monster { ID = 1, Name = "Slime", State = DataState.Active, HP = 50, Attack = 5, Experience = 10, BaseStats = new Stats { Strength = 5, Dexterity = 5, Intelligence = 1, ElementalResistances = new Resistances { Fire = 10, Ice = -5, Lightning = 0 } } });
            monstersTable.Rows.Add(new Monster { ID = 2, Name = "Goblin", State = DataState.Active, HP = 100, Attack = 15, Experience = 25, BaseStats = new Stats { Strength = 10, Dexterity = 8, Intelligence = 2, ElementalResistances = new Resistances { Fire = 20, Ice = 0, Lightning = 5 } } });
            monstersTable.Rows.Add(new Monster { ID = 3, Name = "Dragon", State = DataState.Active, HP = 5000, Attack = 120, Experience = 1000, BaseStats = new Stats { Strength = 100, Dexterity = 60, Intelligence = 80, ElementalResistances = new Resistances { Fire = 100, Ice = 75, Lightning = 50 } } });
            GameTables.Add(monstersTable);

            // Quests Table
            var questsTable = new GameDataTable("Quests", typeof(Quest));
            questsTable.Rows.Add(new Quest { ID = 1, Name = "Main Quest 1", State = DataState.Active, Title = "A New Beginning", RequiredLevel = 1 });
            questsTable.Rows.Add(new Quest { ID = 2, Name = "Side Quest A", State = DataState.Active, Title = "Lost and Found", GiverNPC = 2 }); // Goblin's ID is 2
            GameTables.Add(questsTable);
        }
    }
}