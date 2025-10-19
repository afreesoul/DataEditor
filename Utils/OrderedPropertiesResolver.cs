using GameDataEditor.Models.DataEntries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace GameDataEditor.Utils
{
    public class OrderedPropertiesResolver : DefaultJsonTypeInfoResolver
    {
        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            JsonTypeInfo typeInfo = base.GetTypeInfo(type, options);

            if (typeInfo.Kind == JsonTypeInfoKind.Object && typeof(BaseDataRow).IsAssignableFrom(type))
            {
                // Exclude the 'CompositeDisplayName' property from serialization
                var compositeDisplayNameProp = typeInfo.Properties.FirstOrDefault(p => p.Name == "CompositeDisplayName");
                if (compositeDisplayNameProp != null)
                {
                    compositeDisplayNameProp.ShouldSerialize = (obj, val) => false;
                }

                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var baseOrder = new Dictionary<string, int> { { "ID", 0 }, { "Name", 1 }, { "State", 2 } };

                var orderedProps = props
                    .Where(p => p.Name != "CompositeDisplayName") // Also exclude here for consistency
                    .OrderBy(p => p.DeclaringType != typeof(BaseDataRow))
                    .ThenBy(p => baseOrder.TryGetValue(p.Name, out var order) ? order : int.MaxValue)
                    .ThenBy(p => p.MetadataToken)
                    .ToList();

                int orderCounter = 0;
                foreach (var propInfo in orderedProps)
                {
                    var jsonPropInfo = typeInfo.Properties.FirstOrDefault(p => p.Name == propInfo.Name);
                    if (jsonPropInfo != null)
                    {
                        jsonPropInfo.Order = orderCounter++;
                    }
                }
            }

            return typeInfo;
        }
    }
}
