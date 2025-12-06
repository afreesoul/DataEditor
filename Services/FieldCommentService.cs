using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GameDataEditor.Services
{
    public class FieldCommentService
    {
        private readonly string _commentFilePath;
        // 结构: tableName -> fieldName -> comment
        private Dictionary<string, Dictionary<string, string>> _fieldComments;

        public FieldCommentService(string dataFolderPath)
        {
            _commentFilePath = Path.Combine(dataFolderPath, "field_comments.json");
            _fieldComments = new Dictionary<string, Dictionary<string, string>>();
            LoadComments();
        }

        public void SetComment(string tableName, string fieldName, string comment)
        {
            if (!_fieldComments.ContainsKey(tableName))
            {
                _fieldComments[tableName] = new Dictionary<string, string>();
            }

            if (string.IsNullOrEmpty(comment))
            {
                _fieldComments[tableName].Remove(fieldName);
                // 如果表下没有任何字段注释了，也移除该表
                if (_fieldComments[tableName].Count == 0)
                {
                    _fieldComments.Remove(tableName);
                }
            }
            else
            {
                _fieldComments[tableName][fieldName] = comment;
            }
            SaveComments();
        }

        public string GetComment(string tableName, string fieldName)
        {
            if (_fieldComments.TryGetValue(tableName, out var tableComments))
            {
                if (tableComments.TryGetValue(fieldName, out var comment))
                {
                    return comment;
                }
            }
            return string.Empty;
        }

        public Dictionary<string, string>? GetTableComments(string tableName)
        {
            return _fieldComments.TryGetValue(tableName, out var comments) ? comments : null;
        }

        private void LoadComments()
        {
            try
            {
                if (File.Exists(_commentFilePath))
                {
                    var json = File.ReadAllText(_commentFilePath);
                    _fieldComments = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json) 
                                     ?? new Dictionary<string, Dictionary<string, string>>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load field comments: {ex.Message}");
                _fieldComments = new Dictionary<string, Dictionary<string, string>>();
            }
        }

        private void SaveComments()
        {
            try
            {
                var json = JsonSerializer.Serialize(_fieldComments, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_commentFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save field comments: {ex.Message}");
            }
        }
    }
}
