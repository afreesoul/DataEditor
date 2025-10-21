using GameDataEditor.Models;
using GameDataEditor.Models.DataEntries;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace GameDataEditor.Utils
{
    public class CsvService
    {
        private readonly Dictionary<Type, List<PropertyInfo>> _propertyOrderCache = new Dictionary<Type, List<PropertyInfo>> ();

        private List<PropertyInfo> GetOrderedProperties(Type type)
        {
            if (_propertyOrderCache.TryGetValue(type, out var cachedProps)) return cachedProps!;

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != "CompositeDisplayName")
                .GroupBy(p => p.DeclaringType)
                .OrderBy(g => g.Key == typeof(BaseDataRow) ? 0 : 1) // BaseDataRow first
                .SelectMany(g => g.OrderBy(p => p.MetadataToken))
                .ToList();

            _propertyOrderCache[type] = properties;
            return properties;
        }

        public string GenerateCsv(GameDataTable table)
        {
            if (table.Rows.Count == 0) return string.Empty;

            var allHeaders = new HashSet<string>();
            var allRowData = new List<Dictionary<string, object>>();

            foreach (var row in table.Rows)
            {
                var rowData = new Dictionary<string, object>();
                FlattenObject(row, rowData, "");
                allRowData.Add(rowData);
                foreach (var key in rowData.Keys) allHeaders.Add(key);
            }

            var sortedHeaders = allHeaders.ToList();
            sortedHeaders.Sort(new CsvHeaderComparer(table.DataType, GetOrderedProperties));

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", sortedHeaders.Select(EscapeCsvField)));

            foreach (var rowData in allRowData)
            {
                var line = sortedHeaders.Select(header =>
                    rowData.TryGetValue(header, out var value) ? EscapeCsvField(value?.ToString() ?? string.Empty) : string.Empty);
                sb.AppendLine(string.Join(",", line));
            }

            return sb.ToString();
        }

        private void FlattenObject(object? obj, Dictionary<string, object> dict, string prefix)
        {
            if (obj == null) return;

            var properties = GetOrderedProperties(obj.GetType());

            foreach (var prop in properties)
            {
                string key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                object? value = prop.GetValue(obj);

                if (value == null) continue;

                var propType = prop.PropertyType;

                if (propType.GetInterface(nameof(IList)) != null && propType != typeof(string))
                {
                    var list = (IList)value;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var item = list[i];
                        string itemKey = $"{key}.{i}";
                        if (item != null && IsSimpleType(item.GetType()))
                        {
                            dict[itemKey] = item;
                        }
                        else
                        {
                            FlattenObject(item, dict, itemKey);
                        }
                    }
                }
                else if (IsSimpleType(propType))
                {
                    if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(ForeignKey<>))
                    {
                        var idProp = value.GetType().GetProperty("ID");
                        dict[key] = idProp?.GetValue(value) ?? string.Empty;
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

        public void UpdateTableFromCsv(GameDataTable table, string csvContent)
        {
            var records = ParseCsv(csvContent);
            if (records.Count == 0) return;

            var tableRowsById = table.Rows.ToDictionary(r => r.ID);

            foreach (var record in records)
            {
                string? idString = null;
                if (!record.TryGetValue("ID", out idString) || !int.TryParse(idString, out int id))
                {
                    continue;
                }

                if (tableRowsById.TryGetValue(id, out BaseDataRow? rowToUpdate))
                {
                    if (rowToUpdate != null) UnflattenObject(rowToUpdate, record);
                }
            }
        }

        private void UnflattenObject(object targetObj, Dictionary<string, string> record)
        {
            var groupedProperties = record
                .Select(kvp => new { Match = Regex.Match(kvp.Key, @"^([^.]+)(\.?.*)"), kvp.Value })
                .Where(x => x.Match.Success)
                .GroupBy(x => x.Match.Groups[1].Value);

            foreach (var group in groupedProperties)
            {
                var propInfo = targetObj.GetType().GetProperty(group.Key);
                if (propInfo == null || !propInfo.CanWrite) continue;

                var propType = propInfo.PropertyType;

                if (propType.GetInterface(nameof(IList)) != null && propType != typeof(string))
                {
                    var list = (IList?)propInfo.GetValue(targetObj);
                    if (list == null) continue;
                    list.Clear();

                    var itemType = propType.GetGenericArguments()[0];
                    var itemsData = group
                        .Select(g => new { Match = Regex.Match(g.Match.Groups[2].Value, @"^\.(\d+)(.*)"), g.Value })
                        .Where(x => x.Match.Success)
                        .GroupBy(x => int.Parse(x.Match.Groups[1].Value))
                        .OrderBy(g => g.Key);

                    foreach (var itemGroup in itemsData)
                    {
                        var itemRecord = itemGroup.ToDictionary(ig => ig.Match.Groups[2].Value.TrimStart('.'), ig => ig.Value);
                        if (IsSimpleType(itemType))
                        {
                            var simpleValue = itemGroup.First().Value;
                            var convertedValue = Convert.ChangeType(simpleValue, itemType, CultureInfo.InvariantCulture);
                            list.Add(convertedValue);
                        }
                        else
                        {
                            var newItem = Activator.CreateInstance(itemType);
                            if (newItem != null) 
                            {
                                UnflattenObject(newItem, itemRecord);
                                list.Add(newItem);
                            }
                        }
                    }
                }
                else if (IsSimpleType(propType))
                {
                    var value = group.First().Value;
                    SetSimpleProperty(targetObj, propInfo, value);
                }
                else
                {
                    var nestedObject = propInfo.GetValue(targetObj) ?? Activator.CreateInstance(propType);
                    if (nestedObject == null) continue;
                    var nestedRecord = group.ToDictionary(g => g.Match.Groups[2].Value.TrimStart('.'), g => g.Value);
                    UnflattenObject(nestedObject, nestedRecord);
                    propInfo.SetValue(targetObj, nestedObject);
                }
            }
        }

        private void SetSimpleProperty(object targetObj, PropertyInfo propInfo, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            var propType = propInfo.PropertyType;
            object? convertedValue;

            if (propType.IsEnum)
            {
                convertedValue = Enum.Parse(propType, value);
            }
            else if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(ForeignKey<>))
            {
                if (!int.TryParse(value, out int id)) return;
                convertedValue = Activator.CreateInstance(propType);
                var idProp = propType.GetProperty("ID");
                idProp?.SetValue(convertedValue, id);
            }
            else
            {
                var underlyingType = Nullable.GetUnderlyingType(propType) ?? propType;
                convertedValue = Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
            }
            propInfo.SetValue(targetObj, convertedValue);
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

        private string EscapeCsvField(string field)
        {
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }
    }

    public class CsvHeaderComparer : IComparer<string>
    {
        private readonly Type _rootType;
        private readonly Func<Type, List<PropertyInfo>> _getPropertyOrderFunc;

        public CsvHeaderComparer(Type rootType, Func<Type, List<PropertyInfo>> getPropertyOrderFunc)
        {
            _rootType = rootType;
            _getPropertyOrderFunc = getPropertyOrderFunc;
        }

        public int Compare(string? x, string? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            string[] partsX = x.Split('.');
            string[] partsY = y.Split('.');

            int minParts = Math.Min(partsX.Length, partsY.Length);
            Type currentType = _rootType;

            for (int i = 0; i < minParts; i++)
            {
                bool isNumericX = int.TryParse(partsX[i], out int numX);
                bool isNumericY = int.TryParse(partsY[i], out int numY);

                if (isNumericX && isNumericY)
                {
                    int numCompare = numX.CompareTo(numY);
                    if (numCompare != 0) return numCompare;
                }
                else
                {
                    var orderedProps = _getPropertyOrderFunc(currentType);
                    int indexX = orderedProps.FindIndex(p => p.Name == partsX[i]);
                    int indexY = orderedProps.FindIndex(p => p.Name == partsY[i]);

                    int propCompare = indexX.CompareTo(indexY);
                    if (propCompare != 0) return propCompare;

                    var propInfo = orderedProps.FirstOrDefault(p => p.Name == partsX[i]);
                    if (propInfo != null)
                    {
                        var propType = propInfo.PropertyType;
                        if (propType.GetInterface(nameof(IList)) != null && propType != typeof(string))
                        {
                            currentType = propType.GetGenericArguments()[0];
                        }
                        else
                        {
                            currentType = propType;
                        }
                    }
                }
            }

            return partsX.Length.CompareTo(partsY.Length);
        }
    }
}
