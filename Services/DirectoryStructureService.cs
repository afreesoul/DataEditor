using GameDataEditor.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GameDataEditor.Services
{
    [Serializable]
    public class DirectoryStructure
    {
        public List<DirectoryItem> Items { get; set; } = new List<DirectoryItem>();
    }

    [Serializable]
    public class DirectoryItem
    {
        public string Name { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public bool IsExpanded { get; set; }
        public List<DirectoryItem> Children { get; set; } = new List<DirectoryItem>();
    }

    public class DirectoryStructureService
    {
        private readonly string _structureFilePath;

        public DirectoryStructureService(string dataFolderPath)
        {
            _structureFilePath = Path.Combine(dataFolderPath, "directory_structure.json");
        }

        public void SaveStructure(IEnumerable<IDataItem> rootItems)
        {
            try
            {
                var structure = new DirectoryStructure
                {
                    Items = rootItems.Select(ConvertToDirectoryItem).ToList()
                };

                var json = JsonSerializer.Serialize(structure, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_structureFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save directory structure: {ex.Message}");
            }
        }

        public List<IDataItem> LoadStructure(ObservableCollection<GameDataTable> gameTables)
        {
            try
            {
                if (!File.Exists(_structureFilePath))
                    return new List<IDataItem>();

                var json = File.ReadAllText(_structureFilePath);
                var structure = JsonSerializer.Deserialize<DirectoryStructure>(json);
                
                if (structure?.Items == null)
                    return new List<IDataItem>();

                return structure.Items.Select(item => ConvertFromDirectoryItem(item, gameTables)).Where(i => i != null).Cast<IDataItem>().ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load directory structure: {ex.Message}");
                return new List<IDataItem>();
            }
        }

        private DirectoryItem ConvertToDirectoryItem(IDataItem item)
        {
            var directoryItem = new DirectoryItem
            {
                Name = item.Name,
                IsDirectory = item.ItemType == DataItemType.Directory,
                IsExpanded = item.IsExpanded
            };

            if (item is DataDirectory directory)
            {
                directoryItem.Children = directory.Children.Select(ConvertToDirectoryItem).ToList();
            }

            return directoryItem;
        }

        private IDataItem? ConvertFromDirectoryItem(DirectoryItem item, ObservableCollection<GameDataTable> gameTables)
        {
            if (item.IsDirectory)
            {
                var directory = new DataDirectory(item.Name)
                {
                    IsExpanded = item.IsExpanded
                };

                foreach (var child in item.Children)
                {
                    var childItem = ConvertFromDirectoryItem(child, gameTables);
                    if (childItem != null)
                        directory.AddChild(childItem);
                }

                return directory;
            }
            else
            {
                // 查找对应的表
                var table = gameTables.FirstOrDefault(t => t.Name == item.Name);
                return table != null ? new DataTableWrapper(table) : null;
            }
        }
    }
}