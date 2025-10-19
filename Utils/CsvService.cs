using GameDataEditor.Models;
using GameDataEditor.Models.DataEntries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace GameDataEditor.Utils
{
    public class CsvService
    {
        public string GenerateCsv(GameDataTable table)
        {
            if (table.Rows.Count == 0)
            {
                return string.Empty;
            }

            var headers = new List<string>();
            var rows = new List<Dictionary<string, object>>();

            // Generate headers in the correct order
            var declarationHeaders = GetHeaders(table.DataType);
            var baseHeaders = new List<string> { "ID", "Name", "State" };
            headers = declarationHeaders
                .OrderBy(h => {
                    int index = baseHeaders.IndexOf(h);
                    return index == -1 ? int.MaxValue : index;
                })
                .ThenBy(h => declarationHeaders.IndexOf(h))
                .ToList();

            // Process each row
            foreach (var row in table.Rows)
            {
                var rowData = new Dictionary<string, object>();
                FlattenObject(row, rowData, "");
                rows.Add(rowData);
            }

            // Build CSV string
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers.Select(h => EscapeCsvField(h))));

            foreach (var row in rows)
            {
                var line = new List<string>();
                foreach (var header in headers)
                {
                    if (row.TryGetValue(header, out object value))
                    {
                        line.Add(EscapeCsvField(value?.ToString() ?? string.Empty));
                    }
                    else
                    {
                        line.Add(string.Empty);
                    }
                }
                sb.AppendLine(string.Join(",", line));
            }

            return sb.ToString();
        }

        public void UpdateTableFromCsv(GameDataTable table, string csvContent)
        {
            var records = ParseCsv(csvContent);
            if (records.Count == 0) return;

            var headers = records[0].Keys.ToList();
            var tableRowsById = table.Rows.ToDictionary(r => r.ID);

            foreach (var record in records)
            {
                if (!record.TryGetValue("ID", out string idString) || !int.TryParse(idString, out int id))
                {
                    continue; // Skip rows without a valid ID
                }

                if (tableRowsById.TryGetValue(id, out BaseDataRow rowToUpdate))
                {
                    SetObjectPropertiesFromCsv(rowToUpdate, record);
                }
            }
        }

        private void SetObjectPropertiesFromCsv(object obj, Dictionary<string, string> record)
        {
            foreach (var kvp in record)
            {
                try
                {
                    string[] path = kvp.Key.Split('.');
                    object currentObject = obj;

                    for (int i = 0; i < path.Length - 1; i++)
                    {
                        var prop = currentObject.GetType().GetProperty(path[i]);
                        if (prop == null) {
                            currentObject = null;
                            break;
                        }

                        var nextObject = prop.GetValue(currentObject);
                        if (nextObject == null)
                        {
                            nextObject = Activator.CreateInstance(prop.PropertyType);
                            prop.SetValue(currentObject, nextObject);
                        }
                        currentObject = nextObject;
                    }

                    if (currentObject != null)
                    {
                        var finalProp = currentObject.GetType().GetProperty(path.Last());
                        if (finalProp != null && finalProp.CanWrite)
                        {
                            var value = kvp.Value;
                            var propType = finalProp.PropertyType;
                            object convertedValue;

                            if (string.IsNullOrEmpty(value)) continue;

                            if (propType.IsEnum)
                            {
                                convertedValue = Enum.Parse(propType, value);
                            }
                            else if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(ForeignKey<>))
                            {
                                int id = int.Parse(value);
                                convertedValue = Activator.CreateInstance(propType);
                                propType.GetProperty("ID").SetValue(convertedValue, id);
                            }
                            else
                            {
                                var underlyingType = Nullable.GetUnderlyingType(propType) ?? propType;
                                convertedValue = Convert.ChangeType(value, underlyingType);
                            }
                            
                            finalProp.SetValue(currentObject, convertedValue);
                        }
                    }
                }
                catch
                {
                    // Ignore errors on a per-field basis
                }
            }
        }

        private List<Dictionary<string, string>> ParseCsv(string content)
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
                    if (c == '\"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '\"')
                        {
                            currentField.Append('\"');
                            i++; // Skip next quote
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
                    if (c == '\"')
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

        private List<string> GetHeaders(Type type)
        {
            var headers = new List<string>();
            FlattenForHeaders(type, "", headers);
            return headers;
        }

        private void FlattenForHeaders(Type type, string prefix, List<string> headers)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                 .Where(p => p.Name != "CompositeDisplayName")
                                 .OrderBy(p => p.MetadataToken);

            foreach (var prop in properties)
            {
                string key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                var propType = prop.PropertyType;

                if (IsSimpleType(propType))
                {
                    headers.Add(key);
                }
                else
                {
                    FlattenForHeaders(propType, key, headers);
                }
            }
        }

        private void FlattenObject(object obj, Dictionary<string, object> dict, string prefix)
        {
            var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                    .Where(p => p.Name != "CompositeDisplayName")
                                    .OrderBy(p => p.MetadataToken);

            foreach (var prop in properties)
            {
                string key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                object value = prop.GetValue(obj);

                if (value == null)
                {
                    continue;
                }

                if (IsSimpleType(prop.PropertyType))
                {
                    if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(ForeignKey<>))
                    {
                        var idProp = value.GetType().GetProperty("ID");
                        dict[key] = idProp.GetValue(value);
                    }
                    else
                    {
                        dict[key] = value;
                    }
                }
                else
                {
                    FlattenObject(value, dict, key);
                }
            }
        }

        private bool IsSimpleType(Type type)
        {
            return type.IsPrimitive ||
                   type.IsEnum ||
                   type == typeof(string) ||
                   type == typeof(decimal) ||
                   (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) ||
                   (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ForeignKey<>));
        }

        private object GetDefaultValue(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return null;
        }

        private string EscapeCsvField(string field)
        {
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }
    }
}