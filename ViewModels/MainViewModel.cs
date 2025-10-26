using GameDataEditor.Commands;
using GameDataEditor.Models;
using GameDataEditor.Models.DataEntries;
using GameDataEditor.Models.Settings;
using GameDataEditor.Models.Utils;
using GameDataEditor.Utils;
using GameDataEditor.Services;
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
        private TableCommentService? _commentService;
        private DirectoryStructureService? _directoryService;

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

        private ObservableCollection<IDataItem> _dataItems = new();
        public ObservableCollection<IDataItem> DataItems
        {
            get => _dataItems;
            set { _dataItems = value; OnPropertyChanged(nameof(DataItems)); }
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
        public ICommand AddTableCommentCommand { get; }
        public ICommand CreateDirectoryCommand { get; }
        public ICommand MoveTableCommand { get; }

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
            AddTableCommentCommand = new RelayCommand(() => AddTableComment());
            CreateDirectoryCommand = new RelayCommand(CreateDirectory);
            MoveTableCommand = new RelayCommand<MoveTableParameters>(MoveTable);

            LoadFromFolder();
            //LoadFromCsvFolder();
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

        public void AddTableComment(object? parameter = null)
        {
            GameDataTable? tableToComment = null;
            
            // 如果提供了参数，从参数获取表
            if (parameter is IDataItem dataItem && dataItem.ItemType == DataItemType.Table && dataItem is DataTableWrapper tableWrapper)
            {
                tableToComment = tableWrapper.Table;
            }
            // 否则使用当前选中的表
            else if (SelectedTable != null)
            {
                tableToComment = SelectedTable;
            }
            
            if (tableToComment == null)
            {
                MessageBox.Show("请先选择一个表", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var commentDialog = new CommentDialogWindow(tableToComment.Comment);
            commentDialog.Owner = Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive);
            
            if (commentDialog.ShowDialog() == true)
            {
                string newComment = commentDialog.CommentText?.Trim() ?? string.Empty;
                
                // 初始化注释服务（如果尚未初始化）
                if (_commentService == null && !string.IsNullOrEmpty(_appSettings.DataFolderPath))
                {
                    _commentService = new TableCommentService(_appSettings.DataFolderPath);
                }

                if (_commentService != null)
                {
                    // 保存注释到文件
                    _commentService.SetComment(tableToComment.Name, newComment);
                    
                    // 更新表格的注释属性
                    tableToComment.Comment = newComment;
                    
                    Log($"为表 '{tableToComment.Name}' {(string.IsNullOrEmpty(newComment) ? "清空" : "设置")}注释: {newComment}");
                }
                else
                {
                    // 如果无法初始化注释服务，至少更新UI显示
                    tableToComment.Comment = newComment;
                    Log($"为表 '{tableToComment.Name}' {(string.IsNullOrEmpty(newComment) ? "清空" : "设置")}注释（本地显示）: {newComment}");
                }
            }
        }

        public void CreateDirectory()
        {
            var dialog = new DirectoryDialogWindow();
            dialog.Owner = Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive);
            
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.DirectoryName))
            {
                var newDirectory = new DataDirectory(dialog.DirectoryName.Trim());
                DataItems.Add(newDirectory);
                SaveDirectoryStructure();
                Log($"创建目录: {newDirectory.Name}");
            }
        }

        public void SaveDirectoryStructure()
        {
            if (_directoryService != null)
            {
                _directoryService.SaveStructure(DataItems);
            }
        }

        public void ReloadDirectoryStructure()
        {
            if (_directoryService != null)
            {
                // 重新加载目录结构以刷新UI显示
                var newDataItems = new ObservableCollection<IDataItem>(_directoryService.LoadStructure(GameTables));
                DataItems.Clear();
                foreach (var item in newDataItems)
                {
                    DataItems.Add(item);
                }
                OnPropertyChanged(nameof(DataItems));
            }
        }

        public void MoveTable(MoveTableParameters parameters)
        {
            if (parameters?.Table == null) return;

            var tableWrapper = parameters.Table;
            
            if (parameters.MoveToOuter)
            {
                // 移动到外层
                if (tableWrapper.Parent is DataDirectory parentDirectory)
                {
                    parentDirectory.RemoveChild(tableWrapper);
                    DataItems.Add(tableWrapper);
                    tableWrapper.Parent = null;
                    
                    SaveDirectoryStructure();
                    Log($"将表 '{tableWrapper.Name}' 从目录 '{parentDirectory.Name}' 移动到外层");
                }
            }
            else if (parameters.TargetDirectory != null)
            {
                // 移动到指定目录
                if (tableWrapper.Parent != null)
                {
                    // 从原位置移除
                    if (tableWrapper.Parent is DataDirectory oldParent)
                    {
                        oldParent.RemoveChild(tableWrapper);
                    }
                    else
                    {
                        DataItems.Remove(tableWrapper);
                    }
                }

                // 添加到目标目录
                parameters.TargetDirectory.AddChild(tableWrapper);
                parameters.TargetDirectory.IsExpanded = true;
                
                SaveDirectoryStructure();
                Log($"将表 '{tableWrapper.Name}' 移动到目录 '{parameters.TargetDirectory.Name}'");
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
            }
        }

        /// <summary>
        /// 从CSV文件夹加载数据并初始化GameTables（模仿LoadFromFolder函数）
        /// </summary>
        private void LoadFromCsvFolder()
        {
            string? directory = _appSettings.CsvFolderPath;

            // 总是为所有定义的类型创建表，即使目录无效
            var loadedTables = new ObservableCollection<GameDataTable>();
            var csvService = new CsvService();
            
            foreach (var kvp in TableTypeMapping)
            {
                var table = new GameDataTable(kvp.Key, kvp.Value);
                
                // 只有在目录存在且有效时才尝试加载数据
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    string fileName = $"{kvp.Key}.csv";
                    string filePath = Path.Combine(directory, fileName);

                    if (File.Exists(filePath))
                    {
                        try
                        {
                            string csvContent = File.ReadAllText(filePath, Encoding.UTF8);
                            
                            // 解析CSV内容
                            var records = ParseCsvFromContent(csvContent);
                            if (records.Count > 0)
                            {
                                // 为每个记录创建新行
                                foreach (var record in records)
                                {
                                    if (record.TryGetValue("ID", out string? idString) && int.TryParse(idString, out int id))
                                    {
                                        // 创建新行
                                        var newRow = Activator.CreateInstance(kvp.Value) as BaseDataRow;
                                        if (newRow != null)
                                        {
                                            newRow.ID = id;
                                            
                                            // 使用CSV服务的方法填充数据
                                            UpdateRowFromCsvRecord(newRow, record);
                                            
                                            table.Rows.Add(newRow);
                                        }
                                    }
                                }
                                
                                // 按ID排序
                                var sortedRows = table.Rows.OrderBy(r => r.ID).ToList();
                                table.Rows.Clear();
                                foreach (var row in sortedRows)
                                {
                                    table.Rows.Add(row);
                                }
                            }
                            
                            Log($"Loaded {table.Rows.Count} rows from CSV {fileName}");
                        }
                        catch (Exception ex)
                        {
                            Log($"Error loading CSV {fileName}: {ex.Message}");
                        }
                    }
                    else
                    {
                        Log($"No CSV file found for {table.Name}. Creating empty table.");
                    }
                }
                else
                {
                    Log($"Creating empty table for {table.Name} (no valid CSV directory)");
                }
                
                loadedTables.Add(table);
            }

            GameTables = loadedTables;
            
            // 确保Monster数据的格式正确
            DataConverter.FixAllMonsterData(GameTables);
            
            SelectedTable = null;
            SelectedRow = null;
            
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Log($"Data loaded from CSV folder: {directory}.");
            }
            else
            {
                Log("No valid CSV directory set. All tables are empty.");
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
                Converters = { new JsonStringEnumConverter(), new ForeignKeyConverterFactory(), new FixedLengthArrayConverterFactory() },
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };

            // 初始化注释服务和目录服务
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                _commentService = new TableCommentService(directory);
                _directoryService = new DirectoryStructureService(directory);
            }

            foreach (var kvp in TableTypeMapping)
            {
                var table = new GameDataTable(kvp.Key, kvp.Value);
                
                // 加载注释
                if (_commentService != null)
                {
                    _commentService.LoadCommentsIntoTable(table);
                }
                
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
            
            // 加载目录结构
            if (_directoryService != null)
            {
                DataItems = new ObservableCollection<IDataItem>(_directoryService.LoadStructure(GameTables));
            }
            
            // 如果没有保存的目录结构，将所有表添加到DataItems中
            if (DataItems.Count == 0)
            {
                foreach (var table in GameTables)
                {
                    DataItems.Add(new DataTableWrapper(table));
                }
            }
            
            // 确保Monster数据的格式正确
            DataConverter.FixAllMonsterData(GameTables);
            
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
            
            // Get all properties that are List<T> or FixedLengthArray<T> types
            var properties = rowType.GetProperties()
                .Where(p => p.PropertyType.IsGenericType && 
                           (p.PropertyType.GetGenericTypeDefinition() == typeof(List<>) ||
                            p.PropertyType.GetGenericTypeDefinition() == typeof(FixedLengthArray<>)));
            
            foreach (var prop in properties)
            {
                // Get the current value from the data row
                var currentValue = prop.GetValue(dataRow);
                if (currentValue == null) continue;
                
                // Get the default value from the default instance
                var defaultValue = prop.GetValue(defaultInstance);
                if (defaultValue == null) continue;
                
                // Get the Count property from both lists/arrays
                var currentCount = (int)currentValue.GetType().GetProperty("Count")!.GetValue(currentValue)!;
                var defaultCount = (int)defaultValue.GetType().GetProperty("Count")!.GetValue(defaultValue)!;
                
                // If the current count is less than the default count, we need to fix it
                if (currentCount < defaultCount && prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
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
                // For FixedLengthArray, we just log the mismatch but don't change it
                else if (currentCount != defaultCount && prop.PropertyType.GetGenericTypeDefinition() == typeof(FixedLengthArray<>))
                {
                    Log($"Warning: FixedLengthArray '{prop.Name}' has mismatched length (current: {currentCount}, default: {defaultCount}) but cannot be resized");
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

            Log("Starting field correction by fixing CSV files and reloading...");
            
            try
            {
                // 第一步：先导出当前数据到CSV（包含修正后的数据）
                Log("Step 1: Exporting current data to CSV with field corrections...");
                ExportAllToCsvWithFixes();
                
                // 第二步：重新从CSV加载数据（使用新的LoadFromCsvFolder函数）
                Log("Step 2: Reloading data from corrected CSV files...");
                LoadFromCsvFolder();
                
                Log("Field correction completed by CSV fix and reload method.");
            }
            catch (Exception ex)
            {
                Log($"Error fixing fields: {ex.Message}");
            }
        }

        private void ExportAllToCsvWithFixes()
        {
            if (string.IsNullOrEmpty(_appSettings.CsvFolderPath))
            {
                Log("CSV folder path is not set. Please configure it in Settings.");
                return;
            }

            if (!Directory.Exists(_appSettings.CsvFolderPath))
            {
                Directory.CreateDirectory(_appSettings.CsvFolderPath);
                Log($"Created CSV folder: {_appSettings.CsvFolderPath}");
            }

            int totalTablesExported = 0;
            int totalRowsExported = 0;

            foreach (var table in GameTables)
            {
                if (table.Rows.Count == 0)
                {
                    Log($"Skipping empty table: {table.Name}");
                    continue;
                }

                Log($"Fixing and exporting table: {table.Name}");

                // 获取表的默认实例用于修正
                var defaultInstance = Activator.CreateInstance(table.DataType) as BaseDataRow;
                if (defaultInstance == null)
                {
                    Log($"Error: Failed to create default instance for {table.DataType.Name}");
                    continue;
                }
                if (defaultInstance != null)
                    InitializeFixedLengthArrays(defaultInstance);

                // 修正表中的所有行
                var fixedRows = new List<BaseDataRow>();
                foreach (var row in table.Rows)
                {
                    // 创建行的深拷贝以避免修改原始数据
                    var fixedRow = Activator.CreateInstance(table.DataType) as BaseDataRow;
                    if (fixedRow == null) continue;

                    // 复制基础属性
                    fixedRow.ID = row.ID;
                    fixedRow.Name = row.Name;
                    fixedRow.State = row.State;

                    // 递归修正所有属性
                    int fieldsFixed = 0, arraysFixed = 0;
                    if (fixedRow != null && row != null && defaultInstance != null)
                        FixObjectRecursivelyForExport(fixedRow, row, defaultInstance, ref fieldsFixed, ref arraysFixed);

                    // 确保fixedRow不为null再添加到列表
                    if (fixedRow != null)
                        fixedRows.Add(fixedRow);
                    
                    if ((fieldsFixed > 0 || arraysFixed > 0) && row != null)
                    {
                        Log($"Row {row.ID}: Fixed {fieldsFixed} fields and {arraysFixed} arrays for export");
                    }
                }

                // 导出修正后的数据到CSV
                var csvService = new CsvService();
                var fileName = Path.Combine(_appSettings.CsvFolderPath, $"{table.Name}.csv");
                
                try
                {
                    // 创建临时的GameDataTable用于导出
                    var tempTable = new GameDataTable(table.Name, table.DataType);
                    foreach (var row in fixedRows)
                    {
                        tempTable.Rows.Add(row);
                    }
                    
                    // 生成CSV内容并写入文件
                    var csvContent = csvService.GenerateCsv(tempTable);
                    File.WriteAllText(fileName, csvContent, Encoding.UTF8);
                    
                    totalTablesExported++;
                    totalRowsExported += fixedRows.Count;
                    Log($"Exported {fixedRows.Count} fixed rows to {fileName}");
                }
                catch (Exception ex)
                {
                    Log($"Error exporting table {table.Name}: {ex.Message}");
                }
            }

            Log($"Exported {totalRowsExported} fixed rows from {totalTablesExported} tables to CSV.");
        }

        private void FixObjectRecursivelyForExport(object targetObj, object sourceObj, object defaultObj, ref int fieldsFixed, ref int arraysFixed)
        {
            if (targetObj == null || sourceObj == null || defaultObj == null)
                return;

            var objType = targetObj.GetType();
            if (objType != sourceObj.GetType() || objType != defaultObj.GetType())
                return;

            // 获取所有可读写的属性
            var properties = objType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite);

            foreach (var prop in properties)
            {
                // 跳过基础属性
                if (prop.Name == "ID" || prop.Name == "Name" || prop.Name == "State")
                {
                    // 直接复制基础属性
                    var sourceValue = prop.GetValue(sourceObj);
                    prop.SetValue(targetObj, sourceValue);
                    continue;
                }

                var sourceValueForProp = prop.GetValue(sourceObj);
                var defaultValue = prop.GetValue(defaultObj);

                // 处理FixedLengthArray类型
                if (prop.PropertyType.IsGenericType && 
                    prop.PropertyType.GetGenericTypeDefinition() == typeof(FixedLengthArray<>))
                {
                    var sourceArray = sourceValueForProp as System.Collections.IList;
                    var defaultArray = defaultValue as System.Collections.IList;
                    
                    if (sourceArray != null && defaultArray != null)
                    {
                        // 创建新的FixedLengthArray实例
                        var fixedArray = Activator.CreateInstance(prop.PropertyType, defaultArray.Count) as System.Collections.IList;
                        
                        if (fixedArray != null)
                        {
                            // 复制并修正数组元素
                            for (int i = 0; i < defaultArray.Count; i++)
                            {
                                var sourceElement = SafeGetArrayElement(sourceArray, i);
                                var defaultElement = SafeGetArrayElement(defaultArray, i);
                                
                                // 修正逻辑：如果源数据缺失或为空，使用默认值
                                if (sourceElement == null || (sourceElement is string str && string.IsNullOrEmpty(str)))
                                {
                                    if (defaultElement != null)
                                    {
                                        fixedArray[i] = defaultElement;
                                        arraysFixed++;
                                    }
                                }
                                else
                                {
                                    // 保持源数据
                                    fixedArray[i] = sourceElement;
                                }
                            }
                            
                            prop.SetValue(targetObj, fixedArray);
                        }
                    }
                }
                else
                {
                    // 对于非数组属性，直接复制源数据
                    prop.SetValue(targetObj, sourceValueForProp);
                }
            }
        }

        private void FixObjectRecursively(object currentObj, object defaultObj, ref int fieldsFixed, ref int arraysFixed)
        {
            if (currentObj == null || defaultObj == null)
                return;

            var objType = currentObj.GetType();
            var defaultType = defaultObj.GetType();

            // 确保类型匹配
            if (objType != defaultType)
                return;

            // 获取所有可读写的属性
            var properties = objType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite);

            foreach (var prop in properties)
            {
                // 跳过ID、Name、State等基础属性
                if (prop.Name == "ID" || prop.Name == "Name" || prop.Name == "State")
                    continue;

                var currentValue = prop.GetValue(currentObj);
                var defaultValue = prop.GetValue(defaultObj);

                // 处理属性为null的情况
                if (currentValue == null && defaultValue != null)
                {
                    // 如果是复杂类型，递归创建新实例
                    if (defaultValue.GetType().IsClass && defaultValue.GetType() != typeof(string))
                    {
                        var newInstance = Activator.CreateInstance(defaultValue.GetType());
                        prop.SetValue(currentObj, newInstance);
                        fieldsFixed++;
                        
                        // 递归修正新创建的实例
                        if (newInstance != null && defaultValue != null)
                            FixObjectRecursively(newInstance, defaultValue, ref fieldsFixed, ref arraysFixed);
                    }
                    else
                    {
                        // 简单类型直接赋值
                        prop.SetValue(currentObj, defaultValue);
                        fieldsFixed++;
                    }
                }
                else if (currentValue != null && defaultValue != null)
                {
                    // 处理FixedLengthArray<T>类型
                    if (prop.PropertyType.IsGenericType && 
                        prop.PropertyType.GetGenericTypeDefinition() == typeof(FixedLengthArray<>))
                    {
                        var currentArray = currentValue as System.Collections.IList;
                        var defaultArray = defaultValue as System.Collections.IList;
                        
                        if (currentArray != null && defaultArray != null)
                        {
                            int currentCount = currentArray.Count;
                            int defaultCount = defaultArray.Count;
                            
                            // 添加详细的调试信息
                            Log($"FixedLengthArray '{prop.Name}' analysis:");
                            Log($"  Current array type: {currentArray.GetType()}");
                            Log($"  Default array type: {defaultArray.GetType()}");
                            Log($"  Current count: {currentCount}");
                            Log($"  Default count: {defaultCount}");
                            
                            // 输出实际的内容
                            Log($"  Current values: {string.Join(", ", Enumerable.Range(0, Math.Min(currentCount, 5)).Select(i => $"[{i}]={SafeGetArrayElement(currentArray, i) ?? "null"}"))}");
                            Log($"  Default values: {string.Join(", ", Enumerable.Range(0, Math.Min(defaultCount, 5)).Select(i => $"[{i}]={SafeGetArrayElement(defaultArray, i) ?? "null"}"))}");
                            
                            if (currentCount != defaultCount)
                            {
                                Log($"FixedLengthArray '{prop.Name}' length mismatch: current={currentCount}, default={defaultCount}");
                                
                                // 对于FixedLengthArray，我们专门处理这种情况
                                // FixedLengthArray的Count始终返回Capacity，所以我们需要检查实际内容
                                var elementType = prop.PropertyType.GetGenericArguments()[0];
                                
                                // 检查实际需要修正的元素
                                bool arrayModified = false;
                                int actualElementsFixed = 0;
                                
                                // 对于FixedLengthArray，我们需要处理所有默认索引
                                // 即使当前没有那么多实际存储的元素，也要修正
                                for (int i = 0; i < defaultCount; i++)
                                {
                                    // 安全地获取当前元素的实际值
                                    var currentElement = SafeGetArrayElement(currentArray, i);
                                    var defaultElement = SafeGetArrayElement(defaultArray, i);
                                    
                                    // 判断是否需要修正
                                    bool needsFix = false;
                                    
                                    // 首先检查默认值是否真的为null，还是空字符串
                                    string defaultElementStr = defaultElement?.ToString() ?? "null";
                                    string currentElementStr = currentElement?.ToString() ?? "null";
                                    
                                    if (currentElement == null && defaultElement != null)
                                    {
                                        // 当前元素为null，但默认元素不为null → 需要修正（填充缺失元素）
                                        needsFix = true;
                                        Log($"  Element [{i}]: current=null, default='{defaultElementStr}' → needs fix (filling missing element)");
                                    }
                                    else if (currentElement != null && defaultElement != null)
                                    {
                                        // 两者都不为null，但如果是字符串且当前值为空字符串，也需要修正
                                        if (currentElement is string currentStr && defaultElement is string defaultStr)
                                        {
                                            // 如果当前值是空字符串，即使默认值也是空字符串，也要确保修正
                                            if (string.IsNullOrEmpty(currentStr))
                                            {
                                                needsFix = true;
                                                Log($"  Element [{i}]: current='{currentStr}', default='{defaultStr}' → needs fix (ensuring empty string)");
                                            }
                                            else if (currentStr != defaultStr)
                                            {
                                                // 两者都不为空但值不同 → 暂时不修正
                                                needsFix = false;
                                                Log($"  Element [{i}]: current='{currentStr}', default='{defaultStr}' → NO fix needed (different values)");
                                            }
                                            else
                                            {
                                                Log($"  Element [{i}]: current='{currentStr}', default='{defaultStr}' → no fix needed");
                                            }
                                        }
                                        else
                                        {
                                            // 非字符串类型，只有当当前值为null时才修正
                                            if (currentElement == null)
                                            {
                                                needsFix = true;
                                                Log($"  Element [{i}]: current=null, default='{defaultElement}' → needs fix");
                                            }
                                            else
                                            {
                                                Log($"  Element [{i}]: current='{currentElement}', default='{defaultElement}' → no fix needed");
                                            }
                                        }
                                    }
                                    else if (currentElement != null && defaultElement != null)
                                    {
                                        // 两者都不为null，检查是否需要修正
                                        if (currentElement is string currentStr && defaultElement is string defaultStr)
                                        {
                                            // 对于字符串，特殊处理：只有当当前值为空且默认值不为空时才修正
                                            if (string.IsNullOrEmpty(currentStr) && !string.IsNullOrEmpty(defaultStr))
                                            {
                                                needsFix = true;
                                                Log($"  Element [{i}]: current='{currentStr}', default='{defaultStr}' → needs fix (empty to non-empty)");
                                            }
                                            else if (!string.IsNullOrEmpty(currentStr) && string.IsNullOrEmpty(defaultStr))
                                            {
                                                // 当前值不为空但默认值为空 → 不要修正！保持当前值
                                                needsFix = false;
                                                Log($"  Element [{i}]: current='{currentStr}', default='{defaultStr}' → NO fix needed (keep current value)");
                                            }
                                            else if (currentStr != defaultStr)
                                            {
                                                // 两者都不为空但值不同 → 暂时不修正
                                                needsFix = false;
                                                Log($"  Element [{i}]: current='{currentStr}', default='{defaultStr}' → NO fix needed (different values)");
                                            }
                                            else
                                            {
                                                Log($"  Element [{i}]: current='{currentStr}', default='{defaultStr}' → no fix needed");
                                            }
                                        }
                                        else
                                        {
                                            // 非字符串类型，只有当当前值为null时才修正
                                            if (currentElement == null)
                                            {
                                                needsFix = true;
                                                Log($"  Element [{i}]: current=null, default='{defaultElement}' → needs fix");
                                            }
                                            else
                                            {
                                                Log($"  Element [{i}]: current='{currentElement}', default='{defaultElement}' → no fix needed");
                                            }
                                        }
                                    }
                                    else if (currentElement != null && defaultElement == null)
                                    {
                                        // 当前元素不为null，但默认元素为null → 不要修正！保持当前值
                                        needsFix = false;
                                        Log($"  Element [{i}]: current='{currentElementStr}', default=null → NO fix needed (keep current value)");
                                    }
                                    else if (currentElement == null && defaultElement == null)
                                    {
                                        // 两者都为null，检查是否需要创建默认值
                                        var newDefaultValue = CreateDefaultValue(elementType);
                                        if (newDefaultValue != null)
                                        {
                                            needsFix = true;
                                            Log($"  Element [{i}]: current=null, default=null → needs fix (creating default value)");
                                        }
                                        else
                                        {
                                            Log($"  Element [{i}]: current=null, default=null → no fix needed");
                                        }
                                    }
                                    
                                    if (needsFix)
                                    {
                                        // 执行修正
                                        try
                                        {
                                            if (defaultElement != null)
                                            {
                                                currentArray[i] = defaultElement;
                                                Log($"Fixed FixedLengthArray '{prop.Name}'[{i}] from '{currentElement}' to '{defaultElement}'");
                                            }
                                            else
                                            {
                                                var newDefaultValue = CreateDefaultValue(elementType);
                                                if (newDefaultValue != null)
                                                {
                                                    currentArray[i] = newDefaultValue;
                                                    Log($"Fixed FixedLengthArray '{prop.Name}'[{i}] from '{currentElement}' to '{newDefaultValue}'");
                                                }
                                            }
                                            arrayModified = true;
                                            actualElementsFixed++;
                                        }
                                        catch (ArgumentOutOfRangeException ex)
                                        {
                                            Log($"Error fixing element [{i}] in FixedLengthArray '{prop.Name}': {ex.Message}");
                                        }
                                        catch (IndexOutOfRangeException ex)
                                        {
                                            Log($"Error fixing element [{i}] in FixedLengthArray '{prop.Name}': {ex.Message}");
                                        }
                                    }
                                    
                                    // 递归修正复杂类型
                                    if (currentElement != null && defaultElement != null && 
                                        currentElement.GetType().IsClass && currentElement.GetType() != typeof(string))
                                    {
                                        if (currentElement != null && defaultElement != null)
                                            FixObjectRecursively(currentElement, defaultElement, ref fieldsFixed, ref arraysFixed);
                                    }
                                }
                                
                                if (arrayModified)
                                {
                                    arraysFixed += actualElementsFixed;
                                    Log($"FixedLengthArray '{prop.Name}' corrected {actualElementsFixed} elements");
                                }
                                else
                                {
                                    Log($"FixedLengthArray '{prop.Name}' no elements needed correction (but length mismatch detected)");
                                }
                            }
                            else
                            {
                                // 长度相同，但仍需检查每个元素
                                var elementType = prop.PropertyType.GetGenericArguments()[0];
                                bool arrayModified = false;
                                int actualElementsFixed = 0;
                                
                                for (int i = 0; i < defaultCount; i++)
                                {
                                    var currentElement = SafeGetArrayElement(currentArray, i);
                                    var defaultElement = SafeGetArrayElement(defaultArray, i);
                                    
                                    if (currentElement != null && defaultElement != null && 
                                        currentElement.GetType().IsClass && currentElement.GetType() != typeof(string))
                                    {
                                        if (currentElement != null && defaultElement != null)
                                            FixObjectRecursively(currentElement, defaultElement, ref fieldsFixed, ref arraysFixed);
                                    }
                                }
                                
                                if (arrayModified)
                                {
                                    arraysFixed += actualElementsFixed;
                                    Log($"FixedLengthArray '{prop.Name}' corrected {actualElementsFixed} elements");
                                }
                            }
                        }
                    }
                    // 处理List<T>类型
                    else if (prop.PropertyType.IsGenericType && 
                             prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var currentList = currentValue as System.Collections.IList;
                        var defaultList = defaultValue as System.Collections.IList;
                        
                        if (currentList != null && defaultList != null)
                        {
                            int currentCount = currentList.Count;
                            int defaultCount = defaultList.Count;
                            
                            if (currentCount != defaultCount)
                            {
                                // 调整列表长度
                                var elementType = prop.PropertyType.GetGenericArguments()[0];
                                
                                if (currentCount < defaultCount)
                                {
                                    // 添加元素以达到默认长度
                                    for (int i = currentCount; i < defaultCount; i++)
                                    {
                                        object? newItem = CreateDefaultValue(elementType);
                                        currentList.Add(newItem);
                                        arraysFixed++;
                                    }
                                }
                                else if (currentCount > defaultCount)
                                {
                                    // 移除多余的元素
                                    for (int i = currentCount - 1; i >= defaultCount; i--)
                                    {
                                        currentList.RemoveAt(i);
                                        arraysFixed++;
                                    }
                                }
                            }
                            
                        // 修正列表中的每个元素
                        for (int i = 0; i < Math.Min(currentList.Count, defaultList.Count); i++)
                        {
                            var currentElement = currentList[i];
                            var defaultElement = defaultList[i];
                            
                            if (currentElement != null && defaultElement != null && 
                                currentElement.GetType().IsClass && currentElement.GetType() != typeof(string))
                            {
                                // 递归修正列表元素
                                FixObjectRecursively(currentElement, defaultElement, ref fieldsFixed, ref arraysFixed);
                            }
                            else if (currentElement == null && defaultElement != null)
                            {
                                // 如果当前元素为null但默认元素不为null，创建新实例
                                var newElement = CreateDefaultValue(defaultElement.GetType());
                                if (newElement != null)
                                {
                                    currentList[i] = newElement;
                                    arraysFixed++;
                                    FixObjectRecursively(newElement, defaultElement, ref fieldsFixed, ref arraysFixed);
                                }
                            }
                        }
                        }
                    }
                    // 处理复杂类型（类）的递归修正
                    else if (currentValue.GetType().IsClass && currentValue.GetType() != typeof(string) && 
                             defaultValue.GetType().IsClass && defaultValue.GetType() != typeof(string))
                    {
                        // 递归修正复杂对象
                        FixObjectRecursively(currentValue, defaultValue, ref fieldsFixed, ref arraysFixed);
                    }
                }
            }
        }

        private object? CreateDefaultValue(Type type)
        {
            if (type == typeof(string))
                return string.Empty;
            else if (type.IsValueType)
                return Activator.CreateInstance(type);
            else if (type.IsClass)
                return Activator.CreateInstance(type);
            
            return null;
        }

        private object? SafeGetArrayElement(System.Collections.IList array, int index)
        {
            if (array == null || index < 0 || index >= array.Count)
                return null;
            
            try
            {
                var value = array[index];
                // 如果是FixedLengthArray，对于超出实际元素数量的索引，会返回默认值而不是null
                // 我们需要确保正确处理这种情况
                return value;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }

        private void InitializeFixedLengthArrays(object instance)
        {
            if (instance == null) return;
            
            var properties = instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite);
            
            foreach (var prop in properties)
            {
                if (prop.PropertyType.IsGenericType && 
                    prop.PropertyType.GetGenericTypeDefinition() == typeof(FixedLengthArray<>))
                {
                    var arrayValue = prop.GetValue(instance) as System.Collections.IList;
                    if (arrayValue != null)
                    {
                        var elementType = prop.PropertyType.GetGenericArguments()[0];
                        
                        // 初始化FixedLengthArray中的每个元素
                        for (int i = 0; i < arrayValue.Count; i++)
                        {
                            var currentElement = arrayValue[i];
                            if (currentElement == null)
                            {
                                // 如果元素为null，设置为适当的默认值
                                if (elementType == typeof(string))
                                {
                                    arrayValue[i] = string.Empty;
                                }
                                else if (elementType.IsValueType)
                                {
                                    arrayValue[i] = Activator.CreateInstance(elementType);
                                }
                                else if (elementType.IsClass)
                                {
                                    var newInstance = Activator.CreateInstance(elementType);
                                    arrayValue[i] = newInstance;
                                    // 递归初始化嵌套的FixedLengthArray
                                    if (newInstance != null)
                                        InitializeFixedLengthArrays(newInstance);
                                }
                            }
                            else if (currentElement.GetType().IsClass && currentElement.GetType() != typeof(string))
                            {
                                // 递归初始化复杂对象
                                if (currentElement != null)
                                    InitializeFixedLengthArrays(currentElement);
                            }
                        }
                    }
                }
                else if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
                {
                    var value = prop.GetValue(instance);
                    if (value != null)
                    {
                        // 递归初始化复杂属性
                        if (value != null)
                            InitializeFixedLengthArrays(value);
                    }
                }
            }
        }

        /// <summary>
        /// 解析CSV内容为记录字典列表
        /// </summary>
        private List<Dictionary<string, string>> ParseCsvFromContent(string content)
        {
            var records = new List<Dictionary<string, string>>();
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return records;

            var headers = ParseCsvLine(lines[0]);

            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCsvLine(lines[i]);
                var record = new Dictionary<string, string>();
                for (int j = 0; j < headers.Count && j < values.Count; j++)
                {
                    record[headers[j]] = values[j];
                }
                records.Add(record);
            }

            return records;
        }

        /// <summary>
        /// 解析CSV单行
        /// </summary>
        private List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            currentField.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        fields.Add(currentField.ToString());
                        currentField.Clear();
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
            }
            fields.Add(currentField.ToString());
            return fields;
        }

        /// <summary>
        /// 从CSV记录更新行数据
        /// </summary>
        private void UpdateRowFromCsvRecord(BaseDataRow row, Dictionary<string, string> record)
        {
            var csvService = new CsvService();
            
            // 创建一个临时的表格，包含当前行
            var tempTable = new GameDataTable("TempTable", row.GetType());
            tempTable.Rows.Add(row);
            
            // 使用CSV服务的UpdateTableFromCsv方法
            // 由于我们只有一个行，并且ID匹配，这个方法会更新该行
            var tempRecords = new List<Dictionary<string, string>> { record };
            
            // 模拟UpdateTableFromCsv的逻辑
            foreach (var rec in tempRecords)
            {
                string? idString = null;
                if (rec.TryGetValue("ID", out idString) && int.TryParse(idString, out int id))
                {
                    if (row.ID == id)
                    {
                        // 使用反射调用CsvService的私有方法
                        var unflattenMethod = typeof(CsvService).GetMethod("UnflattenObject", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (unflattenMethod != null)
                        {
                            unflattenMethod.Invoke(csvService, new object[] { row, rec });
                        }
                    }
                }
            }
        }
    }
}