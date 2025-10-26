using GameDataEditor.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GameDataEditor.Services
{
    public class TableCommentService
    {
        private readonly string _commentFilePath;
        private Dictionary<string, string> _tableComments;

        public TableCommentService(string dataFolderPath)
        {
            _commentFilePath = Path.Combine(dataFolderPath, "table_comments.json");
            _tableComments = new Dictionary<string, string>();
            LoadComments();
        }

        public void SetComment(string tableName, string comment)
        {
            if (string.IsNullOrEmpty(comment))
            {
                _tableComments.Remove(tableName);
            }
            else
            {
                _tableComments[tableName] = comment;
            }
            SaveComments();
        }

        public string GetComment(string tableName)
        {
            return _tableComments.TryGetValue(tableName, out string? comment) ? comment : string.Empty;
        }

        private void LoadComments()
        {
            try
            {
                if (File.Exists(_commentFilePath))
                {
                    var json = File.ReadAllText(_commentFilePath);
                    _tableComments = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load table comments: {ex.Message}");
                _tableComments = new Dictionary<string, string>();
            }
        }

        private void SaveComments()
        {
            try
            {
                var json = JsonSerializer.Serialize(_tableComments, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_commentFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save table comments: {ex.Message}");
            }
        }

        public void LoadCommentsIntoTable(GameDataTable table)
        {
            table.Comment = GetComment(table.Name);
        }
    }
}