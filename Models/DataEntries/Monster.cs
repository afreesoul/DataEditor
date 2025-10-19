using GameDataEditor.Models.DataEntries.Complex;
using System.Text.Json.Serialization;

namespace GameDataEditor.Models.DataEntries
{
    public class Monster : BaseDataRow
    {
        public int HP { get; set; }
        public int Attack { get; set; }
        public int Experience { get; set; }
        public Stats? BaseStats { get; set; }
    }
}
