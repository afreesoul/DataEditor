using GameDataEditor.Models.Utils;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameDataEditor.Utils
{
    /// <summary>
    /// 简化的FixedLengthArray JSON转换器
    /// </summary>
    public class SimpleFixedLengthArrayConverter<T> : JsonConverter<FixedLengthArray<T>>
    {
        public override FixedLengthArray<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // 读取JSON数组
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Expected array start token");
            }

            var items = new List<T>();
            
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                var item = JsonSerializer.Deserialize<T>(ref reader, options);
                if (item != null)
                {
                    items.Add(item);
                }
            }

            // 创建FixedLengthArray实例
            var fixedArray = new FixedLengthArray<T>(items.Count);
            for (int i = 0; i < items.Count && i < fixedArray.Length; i++)
            {
                fixedArray[i] = items[i];
            }
            
            return fixedArray;
        }

        public override void Write(Utf8JsonWriter writer, FixedLengthArray<T> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            
            for (int i = 0; i < value.Length; i++)
            {
                JsonSerializer.Serialize(writer, value[i], options);
            }
            
            writer.WriteEndArray();
        }
    }

    /// <summary>
    /// FixedLengthArray转换器工厂
    /// </summary>
    public class FixedLengthArrayConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsGenericType && 
                   typeToConvert.GetGenericTypeDefinition() == typeof(FixedLengthArray<>);
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            Type elementType = typeToConvert.GetGenericArguments()[0];
            Type converterType = typeof(SimpleFixedLengthArrayConverter<>).MakeGenericType(elementType);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }
    }
}