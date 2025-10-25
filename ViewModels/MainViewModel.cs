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
using System.Text;
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

        // Mapping from table name to data type - dynamically populated via reflection
        private static readonly Dictionary<string, Type> TableTypeMapping = GetTableTypeMapping();

        private static Dictionary<string, Type> GetTableTypeMapping()
        {
            var mapping = new Dictionary<string, Type>();
            
            // Get the assembly containing BaseDataRow
            var assembly = typeof(BaseDataRow).Assembly;
            
            // Get all types that inherit from BaseDataRow and are not abstract
            var dataRowTypes = assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(BaseDataRow)) && !t.IsAbstract)
                .ToList();
            
            foreach (var type in dataRowTypes)
            {
                // Generate table name from type name (pluralize the name)
                string tableName = GenerateTableName(type.Name);
                mapping[tableName] = type;
            }
            
            return mapping;
        }

        private static string GenerateTableName(string typeName)
        {
            // Simple pluralization logic - add 's' for most cases
            if (typeName.EndsWith("s"))
                return typeName + "es";
            else if (typeName.EndsWith("y"))
                return typeName.Substring(0, typeName.Length - 1) + "ies";
            else
                return typeName + "s";
        }

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
        public ICommand ExportCsvCommand { get; }
        public ICommand ImportCsvCommand { get; }
        public ICommand FixFieldsCommand { get; }

        public bool IsRowSelected => SelectedRow != null;
        public bool IsTableSelected => SelectedTable != null;

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
                    OnPropertyChanged(nameof(IsTableSelected));

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
            ExportCsvCommand = new RelayCommand(ExportAllToCsv);
            ImportCsvCommand = new RelayCommand(ImportAllFromCsv);
            FixFieldsCommand = new RelayCommand(FixFields);

            LoadFromFolder();
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
                _appSettings.CsvFolderPath = settingsVm.CsvFolderPath;
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
            if (SelectedTable == null) return;

            // If there's no selected row, create a new empty row
            if (SelectedRow == null)
            {
                // Create a new empty instance of the table's data type
                var newRow = (BaseDataRow?)Activator.CreateInstance(SelectedTable.DataType);
                if (newRow == null)
                {
                    Log("Error: Failed to create a new empty row.");
                    return;
                }

                // Generate a new ID starting from 1
                int newId = 1;
                var existingIds = new HashSet<int>(SelectedTable.Rows.Select(r => r.ID));
                while (existingIds.Contains(newId))
                {
                    newId++;
                }

                // Set basic properties
                newRow.ID = newId;
                newRow.Name = "New Item";
                newRow.State = DataState.Active;

                // Add to the collection
                SelectedTable.Rows.Add(newRow);
                SelectedRow = newRow; // Auto-select the new row
                Log($"Created new empty row with ID {newId}");
                return;
            }

            // If there is a selected row, copy it (original logic)
            int copyId = SelectedRow.ID + 1;
            var copyExistingIds = new HashSet<int>(SelectedTable.Rows.Select(r => r.ID));
            while (copyExistingIds.Contains(copyId))
            {
                copyId++;
            }

            // Deep copy the selected row via serialization
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                TypeInfoResolver = new OrderedPropertiesResolver(),
                Converters = { new JsonStringEnumConverter(), new ForeignKeyConverterFactory() }
            };
            string json = JsonSerializer.Serialize(SelectedRow, SelectedRow.GetType(), options);
            var copiedRow = (BaseDataRow?)JsonSerializer.Deserialize(json, SelectedRow.GetType(), options);

            if (copiedRow == null)
            {
                Log("Error: Failed to create a copy of the selected row.");
                return;
            }

            // Update the ID and Name
            copiedRow.ID = copyId;
            copiedRow.Name += " (Copy)";

            // Add to the collection in sorted order
            int insertIndex = 0;
            for (int i = 0; i < SelectedTable.Rows.Count; i++)
            {
                if (SelectedTable.Rows[i].ID > copiedRow.ID)
                {
                    break;
                }
                insertIndex++;
            }
            SelectedTable.Rows.Insert(insertIndex, copiedRow);
        }

        private void DeleteRow()
        {
            if (SelectedRow == null || SelectedTable == null) return;

            var rowToDelete = SelectedRow; // Keep a reference for logging
            SelectedTable.Rows.Remove(SelectedRow);
            Log($"Row {rowToDelete.ID} - {rowToDelete.Name} deleted.");
            SelectedRow = null;
        }

        private void ExportAllToCsv()
        {
            string? targetDirectory = _appSettings.CsvFolderPath;

            if (string.IsNullOrEmpty(targetDirectory) || !Directory.Exists(targetDirectory))
            {
                Log("CSV folder not set or invalid. Please configure it in Settings.");
                var saveFileDialog = new SaveFileDialog
                {
                    Title = "Select Folder to Export CSV Files",
                    FileName = "_Select_a_Folder_"
                };

                if (saveFileDialog.ShowDialog() != true) return;
                targetDirectory = Path.GetDirectoryName(saveFileDialog.FileName);
            }

            if (string.IsNullOrEmpty(targetDirectory)) return;

            var csvService = new CsvService();

            try
            {
                foreach (var table in GameTables)
                {
                    Log($"Exporting {table.Name} to CSV...");
                    string csvContent = csvService.GenerateCsv(table);
                    if (!string.IsNullOrEmpty(csvContent))
                    {
                        string filePath = Path.Combine(targetDirectory, $"{table.Name}.csv");
                        File.WriteAllText(filePath, csvContent, Encoding.UTF8);
                    }
                }
                Log($"All tables exported to CSV in {targetDirectory}.");
            }
            catch (Exception ex)
            {
                Log($"Error exporting to CSV: {ex.Message}");
                MessageBox.Show($"An error occurred while exporting to CSV:\n{ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportAllFromCsv()
        {
            string? sourceDirectory = _appSettings.CsvFolderPath;

            if (string.IsNullOrEmpty(sourceDirectory) || !Directory.Exists(sourceDirectory))
            {
                Log("CSV folder not set or invalid. Please configure it in Settings.");
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Select Folder with CSV Files",
                    CheckFileExists = false,
                    CheckPathExists = true,
                    ValidateNames = false,
                    FileName = "_Folder_Selection_"
                };

                if (openFileDialog.ShowDialog() != true) return;
                sourceDirectory = Path.GetDirectoryName(openFileDialog.FileName);
            }

            if (string.IsNullOrEmpty(sourceDirectory)) return;

            var csvService = new CsvService();

            try
            {
                foreach (var table in GameTables)
                {
                    string filePath = Path.Combine(sourceDirectory, $"{table.Name}.csv");
                    if (File.Exists(filePath))
                    {
                        Log($"Importing data for {table.Name} from CSV...");
                        string csvContent = File.ReadAllText(filePath, Encoding.UTF8);
                        csvService.UpdateTableFromCsv(table, csvContent);
                    }
                }
                Log("Finished importing from CSV.");
                // Optionally, refresh the UI
                UpdateFieldsDisplay();
            }
            catch (Exception ex)
            {
                Log($"Error importing from CSV: {ex.Message}");
                MessageBox.Show($"An error occurred while importing from CSV:\n{ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadFromFolder()
        {
            string? directory = _appSettings.DataFolderPath;

            // Always create tables for all defined types, even if directory is invalid
            var loadedTables = new ObservableCollection<GameDataTable>();
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(), new ForeignKeyConverterFactory() },
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };

            foreach (var kvp in TableTypeMapping)
            {
                var table = new GameDataTable(kvp.Key, kvp.Value);
                
                // Only try to load data if directory exists and is valid
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    string fileName = $"{kvp.Key}.json";
                    string filePath = Path.Combine(directory, fileName);

                    if (File.Exists(filePath))
                    {
                        try
                        {
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
                            Log($"Loaded {table.Rows.Count} rows from {fileName}");
                        }
                        catch (Exception ex)
                        {
                            Log($"Error loading {fileName}: {ex.Message}");
                        }
                    }
                    else
                    {
                        Log($"No data file found for {table.Name}. Creating empty table.");
                    }
                }
                else
                {
                    Log($"Creating empty table for {table.Name} (no valid data directory)");
                }
                
                loadedTables.Add(table);
            }

            GameTables = loadedTables;
            SelectedTable = null;
            SelectedRow = null;
            
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Log($"Data loaded from {directory}.");
            }
            else
            {
                Log("No valid data directory set. All tables are empty.");
            }
        }

        private void SaveAllToFolder()
        {
            string? targetDirectory = _appSettings.DataFolderPath;
            bool needToUpdateSettings = false;
            
            // Check if directory is empty or doesn't exist
            if (string.IsNullOrEmpty(targetDirectory) || !Directory.Exists(targetDirectory))
            {
                Log("Data folder not set or invalid. Please select a folder to save files.");
                var saveFileDialog = new SaveFileDialog
                {
                    Title = "Select Folder to Save Files",
                    FileName = "_Select_a_Folder_",
                    InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
                };

                if (saveFileDialog.ShowDialog() != true) return;

                targetDirectory = Path.GetDirectoryName(saveFileDialog.FileName);
                needToUpdateSettings = true;
            }

            if (string.IsNullOrEmpty(targetDirectory))
            {
                Log("Invalid save directory.");
                return;
            }

            // Ensure the directory exists
            if (!Directory.Exists(targetDirectory))
            {
                try
                {
                    Directory.CreateDirectory(targetDirectory);
                    Log($"Created directory: {targetDirectory}");
                }
                catch (Exception ex)
                {
                    Log($"Error creating directory: {ex.Message}");
                    MessageBox.Show($"Error creating directory: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            var options = new JsonSerializerOptions 
            {
                WriteIndented = true,
                TypeInfoResolver = new OrderedPropertiesResolver(),
                Converters = { new JsonStringEnumConverter(), new ForeignKeyConverterFactory() }
            };

            try
            {
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
                
                // Update settings if needed
                if (needToUpdateSettings)
                {
                    _appSettings.DataFolderPath = targetDirectory;
                    _settingsService.SaveSettings(_appSettings);
                    Log($"Saved data folder path to settings: {targetDirectory}");
                }
                
                Log($"All data saved to {targetDirectory}.");
            }
            catch (Exception ex)
            {
                Log($"Error saving data: {ex.Message}");
                MessageBox.Show($"Error saving data: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

                                PropertyInfo? idProp = fkValue.GetType().GetProperty("ID");
                                if (idProp == null) continue;

                                int currentId = Convert.ToInt32(idProp.GetValue(fkValue));

                                if (currentId == oldId)
                                {
                                    Log($"Found match in table '{table.Name}', row '{row.ID} - {row.Name}', property '{prop.Name}'. Updating value to {newId}.");
                                    var newFkInstance = Activator.CreateInstance(prop.PropertyType);
                                    prop.PropertyType.GetProperty("ID")?.SetValue(newFkInstance, newId);
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
                }));
            }

            // Apply the default expansion setting
            foreach (var field in SelectedRowFields)
            {
                SetExpandedRecursively(field, _appSettings.ExpandNodesByDefault);
            }
        }

        /// <summary>
        /// Fixes array lengths for data objects to match model definitions
        /// </summary>
        /// <param name="dataRow">The data row object to fix</param>
        private void FixArrayLengths(BaseDataRow dataRow)
        {
            // Get the type of the data row
            var rowType = dataRow.GetType();
            
            // Create a default instance of the same type to get the default array lengths
            var defaultInstance = Activator.CreateInstance(rowType) as BaseDataRow;
            if (defaultInstance == null) return;
            
            // Get all properties that are List<T> types
            var properties = rowType.GetProperties()
                .Where(p => p.PropertyType.IsGenericType && 
                           p.PropertyType.GetGenericTypeDefinition() == typeof(List<>));
            
            foreach (var prop in properties)
            {
                // Get the current value from the data row
                var currentValue = prop.GetValue(dataRow);
                if (currentValue == null) continue;
                
                // Get the default value from the default instance
                var defaultValue = prop.GetValue(defaultInstance);
                if (defaultValue == null) continue;
                
                // Get the Count property from both lists
                var currentCount = (int)currentValue.GetType().GetProperty("Count")!.GetValue(currentValue)!;
                var defaultCount = (int)defaultValue.GetType().GetProperty("Count")!.GetValue(defaultValue)!;
                
                // If the current count is less than the default count, we need to fix it
                if (currentCount < defaultCount)
                {
                    var listType = prop.PropertyType.GetGenericArguments()[0];
                    
                    // Get the actual list so we can add items to it
                    var list = currentValue as System.Collections.IList;
                    if (list == null) continue;
                    
                    // Add items to reach the default count
                    while (list.Count < defaultCount)
                    {
                        // Create default value based on the list type
                        object? newItem = listType switch
                        {
                            Type t when t == typeof(string) => string.Empty,
                            Type t when t.IsValueType => Activator.CreateInstance(t),
                            _ => Activator.CreateInstance(listType)
                        };
                        
                        if (newItem != null)
                        {
                            list.Add(newItem);
                        }
                    }
                }
            }
        }

        private void FixFields()
        {
            if (GameTables == null || GameTables.Count == 0)
            {
                Log("No tables available to fix fields.");
                return;
            }

            Log("Starting field correction for all tables...");
            
            int totalTablesFixed = 0;
            int totalRowsFixed = 0;
            int totalFieldsFixed = 0;
            int totalArraysFixed = 0;

            try
            {
                foreach (var table in GameTables)
                {
                    if (table.Rows.Count == 0)
                    {
                        Log($"Skipping empty table: {table.Name}");
                        continue;
                    }

                    Log($"Fixing fields for table: {table.Name}");
                    int tableRowsFixed = 0;
                    int tableFieldsFixed = 0;
                    int tableArraysFixed = 0;

                    // Get the data type for this table
                    var rowType = table.DataType;
                    
                    // Create a default instance to compare against
                    var defaultInstance = Activator.CreateInstance(rowType) as BaseDataRow;
                    if (defaultInstance == null)
                    {
                        Log($"Error: Failed to create default instance for {rowType.Name}");
                        continue;
                    }

                    // Get all properties from the default instance
                    var properties = rowType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => p.CanRead && p.CanWrite);

                    foreach (var row in table.Rows)
                    {
                        int rowFieldsFixed = 0;
                        int rowArraysFixed = 0;

                        foreach (var prop in properties)
                        {
                            // Skip ID, Name, State properties that are handled by base class
                            if (prop.Name == "ID" || prop.Name == "Name" || prop.Name == "State")
                                continue;

                            var currentValue = prop.GetValue(row);
                            var defaultValue = prop.GetValue(defaultInstance);

                            // Check if property is missing (null) and should have a default value
                            if (currentValue == null && defaultValue != null)
                            {
                                // Set the default value
                                prop.SetValue(row, defaultValue);
                                rowFieldsFixed++;
                            }
                            // Check if property is a List<T> and needs length adjustment
                            else if (prop.PropertyType.IsGenericType && 
                                     prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>) &&
                                     currentValue != null && defaultValue != null)
                            {
                                var currentList = currentValue as System.Collections.IList;
                                var defaultList = defaultValue as System.Collections.IList;
                                
                                if (currentList != null && defaultList != null)
                                {
                                    int currentCount = currentList.Count;
                                    int defaultCount = defaultList.Count;
                                    
                                    if (currentCount != defaultCount)
                                    {
                                        // Adjust list length
                                        if (currentCount < defaultCount)
                                        {
                                            // Add items to reach default count
                                            var itemType = prop.PropertyType.GetGenericArguments()[0];
                                            for (int i = currentCount; i < defaultCount; i++)
                                            {
                                                object? newItem = itemType switch
                                                {
                                                    Type t when t == typeof(string) => string.Empty,
                                                    Type t when t.IsValueType => Activator.CreateInstance(t),
                                                    _ => Activator.CreateInstance(itemType)
                                                };
                                                currentList.Add(newItem);
                                            }
                                        }
                                        else if (currentCount > defaultCount)
                                        {
                                            // Remove excess items
                                            for (int i = currentCount - 1; i >= defaultCount; i--)
                                            {
                                                currentList.RemoveAt(i);
                                            }
                                        }
                                        rowArraysFixed++;
                                    }
                                }
                            }
                        }

                        if (rowFieldsFixed > 0 || rowArraysFixed > 0)
                        {
                            tableRowsFixed++;
                            tableFieldsFixed += rowFieldsFixed;
                            tableArraysFixed += rowArraysFixed;
                        }
                    }

                    if (tableRowsFixed > 0)
                    {
                        totalTablesFixed++;
                        totalRowsFixed += tableRowsFixed;
                        totalFieldsFixed += tableFieldsFixed;
                        totalArraysFixed += tableArraysFixed;
                        Log($"Table {table.Name}: Fixed {tableFieldsFixed} fields and {tableArraysFixed} arrays in {tableRowsFixed} rows");
                    }
                    else
                    {
                        Log($"Table {table.Name}: No corrections needed");
                    }
                }

                Log($"Field correction completed. Fixed {totalFieldsFixed} fields and {totalArraysFixed} arrays in {totalRowsFixed} rows across {totalTablesFixed} tables.");
                
                // Refresh the fields display to show the changes if a row is selected
                if (SelectedRow != null)
                {
                    UpdateFieldsDisplay();
                }
            }
            catch (Exception ex)
            {
                Log($"Error fixing fields: {ex.Message}");
            }
        }
    }
}