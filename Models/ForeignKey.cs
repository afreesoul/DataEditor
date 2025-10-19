using GameDataEditor.Models.DataEntries;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameDataEditor.Models
{
    // The struct to provide strong typing for Foreign Keys
    public struct ForeignKey<T> where T : BaseDataRow
    {
        public int ID { get; set; }

        public static implicit operator int(ForeignKey<T> fk) => fk.ID;
        public static implicit operator ForeignKey<T>(int id) => new() { ID = id };
    }

    // The JsonConverter to serialize the ForeignKey<T> as a simple int
    public class ForeignKeyConverter<T> : JsonConverter<ForeignKey<T>> where T : BaseDataRow
    {
        public override ForeignKey<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.Number)
            {
                throw new JsonException($"Expected a number for a ForeignKey, but got {reader.TokenType}");
            }
            return reader.GetInt32();
        }

        public override void Write(Utf8JsonWriter writer, ForeignKey<T> value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value.ID);
        }
    }

    // The factory to create the generic converter at runtime
    public class ForeignKeyConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsGenericType &&
                   typeToConvert.GetGenericTypeDefinition() == typeof(ForeignKey<>);
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            Type modelType = typeToConvert.GetGenericArguments()[0];
            Type converterType = typeof(ForeignKeyConverter<>).MakeGenericType(modelType);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }
    }
}
