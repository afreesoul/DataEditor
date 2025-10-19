using System.Text.Json.Serialization;

using System.Text.Json.Serialization;

namespace GameDataEditor.Models.DataEntries
{
    public abstract class BaseDataRow
    {
        public int ID { get; set; }
        public string Name { get; set; } = string.Empty;
        [JsonIgnore]
        public string CompositeDisplayName => $"{ID} - {Name}";
        public DataState State { get; set; }
    }
}
